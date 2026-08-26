using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace TessitoreGM.AiGm;

/// <summary>
/// Local-only Ollama adapter. The endpoint is deliberately restricted to the
/// loopback interface so a campaign cannot be sent to a remote server by
/// changing a configuration file.
/// </summary>
public sealed class OllamaAiGameMaster : IAiGameMaster
{
    public static readonly Uri DefaultEndpoint =
        new("http://127.0.0.1:11434/");

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly Uri _chatEndpoint;
    private readonly AiGmJsonProtocol _protocol;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OllamaAiGameMaster(
        HttpClient httpClient,
        string model,
        Uri? endpoint = null,
        AiGmJsonProtocol? protocol = null)
    {
        _httpClient = httpClient ??
            throw new ArgumentNullException(nameof(httpClient));
        _model = RequiredModel(model);
        var baseEndpoint = endpoint ?? DefaultEndpoint;
        if (!baseEndpoint.IsAbsoluteUri ||
            !baseEndpoint.IsLoopback ||
            baseEndpoint.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException(
                "Ollama must use a local HTTP endpoint.",
                nameof(endpoint));
        }

        _chatEndpoint = new Uri(baseEndpoint, "api/chat");
        _protocol = protocol ?? new AiGmJsonProtocol();
    }

    public async Task<AiGmTurnPlan> PlanTurnAsync(
        AiGmTurnContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prompt = _protocol.CreatePrompt(context);
        var userInstructions = prompt.ContextJson + "\n\n" +
            prompt.ResponseInstructions;
        var plan = await RequestPlanAsync(
            context,
            prompt.SystemInstructions,
            userInstructions,
            cancellationToken);

        var repairFeedback = RepairFeedback(context, plan);
        if (repairFeedback.Count == 0)
        {
            return plan;
        }

        return await RequestPlanAsync(
            context,
            prompt.SystemInstructions,
            userInstructions + "\n\nCORREZIONE OBBLIGATORIA DEL PRECEDENTE " +
            "TENTATIVO:\n- " + string.Join("\n- ", repairFeedback) +
            "\nGenera un nuovo oggetto JSON completo che corregga tutti questi errori.",
            cancellationToken);
    }

    private async Task<AiGmTurnPlan> RequestPlanAsync(
        AiGmTurnContext context,
        string systemInstructions,
        string userInstructions,
        CancellationToken cancellationToken)
    {
        using var schema = JsonDocument.Parse(
            AiGmJsonProtocol.ResponseSchemaJson);
        var request = new OllamaChatRequest(
            _model,
            [
                new OllamaMessage("system", systemInstructions),
                new OllamaMessage("user", userInstructions)
            ],
            Stream: false,
            schema.RootElement.Clone());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                _chatEndpoint,
                request,
                JsonOptions,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiGmProviderUnavailableException(
                "Ollama non ha risposto in tempo. L'azione resta al GM umano.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiGmProviderUnavailableException(
                "Ollama non è raggiungibile sul computer. Avvialo e riprova; " +
                "l'azione resta al GM umano.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiGmProviderUnavailableException(
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? $"Il modello Ollama '{_model}' non è installato. " +
                          $"Esegui 'ollama pull {_model}'."
                        : "Ollama ha rifiutato la richiesta. L'azione resta al GM umano.");
            }

            OllamaChatResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<
                    OllamaChatResponse>(
                    JsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "Ollama ha restituito una risposta illeggibile.",
                    exception);
            }

            var content = payload?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidDataException(
                    "Ollama non ha restituito un piano narrativo.");
            }

            return _protocol.DeserializePlan(
                content,
                context.PlayerActionId);
        }
    }

    private static IReadOnlyList<string> RepairFeedback(
        AiGmTurnContext context,
        AiGmTurnPlan plan)
    {
        var feedback = new List<string>();
        var withoutAgency = AiGmTurnPlanValidator.RemovePlayerAgencySentences(
            context,
            plan.Narration);
        if (!string.Equals(
                withoutAgency,
                plan.Narration,
                StringComparison.Ordinal))
        {
            feedback.Add(
                "La narrazione decide o descrive azioni del personaggio giocante. " +
                "Descrivi soltanto ambiente e reazioni dei PNG.");
        }

        var observable = AiGmTurnPlanValidator
            .RemoveUnobservableMentalStateSentences(withoutAgency);
        if (!string.Equals(observable, withoutAgency, StringComparison.Ordinal))
        {
            feedback.Add(
                "La narrazione afferma pensieri, speranze o intenzioni interne di un PNG. " +
                "Riscrivila usando soltanto parole, gesti e comportamenti osservabili.");
        }

        var relevant = AiGmTurnPlanValidator.EnsureActionRelevance(
            context,
            observable);
        if (!string.Equals(relevant, observable, StringComparison.Ordinal))
        {
            var target = context.ActionFrame?.Targets.FirstOrDefault();
            feedback.Add(target is null
                ? "La narrazione usa un luogo estraneo alla scena corrente."
                : target.IsVisible
                    ? $"La narrazione deve mostrare una reazione pertinente di {target.Name}."
                    : $"{target.Name} non è presente nella scena e non può reagire direttamente.");
        }

        if ((plan.Consequences ?? []).Any(proposal =>
            AiGmTurnPlanValidator.IsUngroundedPlayerEconomicConsequence(
                context,
                proposal)))
        {
            feedback.Add(
                "Non proporre modifiche a monete o risorse: l'azione del giocatore " +
                "non ha un intento economico o materiale.");
        }


        var sentences = Regex.Split(
                plan.Narration.Trim(),
                @"(?<=[.!?])\s+")
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToArray();
        var wordCount = Regex.Matches(plan.Narration, @"\b[\p{L}\p{N}']+\b")
            .Count;
        var repeats = sentences
            .Select(sentence => sentence.Trim().ToUpperInvariant())
            .Distinct()
            .Count() != sentences.Length;
        if (sentences.Length > 3 || wordCount > 90 || repeats)
        {
            feedback.Add(
                "La narrazione deve essere concisa, non ripetitiva e composta da " +
                "non più di 3 frasi e 90 parole.");
        }

        return feedback;
    }

    private static string RequiredModel(string model)
    {
        var normalized = model?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > 100 ||
            normalized.Any(character =>
                char.IsWhiteSpace(character) ||
                char.IsControl(character)))
        {
            throw new ArgumentException(
                "The Ollama model name is invalid.",
                nameof(model));
        }

        return normalized;
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        JsonElement Format);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaMessage? Message);
}
