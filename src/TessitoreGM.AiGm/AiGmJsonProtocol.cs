using System.Text.Json;
using System.Text.Json.Serialization;
using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.AiGm;

public sealed record AiGmProviderPrompt(
    string SystemInstructions,
    string ContextJson,
    string ResponseInstructions);

/// <summary>
/// Provider-neutral JSON boundary. The provider receives a read-only dossier
/// and may return only the closed set of typed consequences understood here.
/// </summary>
public sealed class AiGmJsonProtocol
{
    private const int MaximumResponseCharacters = 64_000;
    private const int MaximumConsequences = 20;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public AiGmProviderPrompt CreatePrompt(AiGmTurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rules = string.Join("\n", context.Rules.Select(
            (rule, index) => $"{index + 1}. {rule}"));
        var systemInstructions =
            "Sei il Game Master di TessitoreGM. Rispondi soltanto all'azione " +
            "umana già registrata nel dossier. Lo stato fornito è in sola lettura. " +
            "Usa world, memory e catalog come spazio di lavoro privato del GM; " +
            "per il testo narration considera authorizedPerspective una lista " +
            "chiusa di ciò che il giocatore può già percepire o ricordare.\n" +
            rules;

        return new AiGmProviderPrompt(
            systemInstructions,
            JsonSerializer.Serialize(context, JsonOptions),
            ResponseInstructions);
    }

    public AiGmTurnPlan DeserializePlan(string json)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            json.Length > MaximumResponseCharacters)
        {
            throw new InvalidDataException(
                "La risposta del Game Master AI è vuota o troppo lunga.");
        }

        PlanDocument document;
        try
        {
            document = JsonSerializer.Deserialize<PlanDocument>(json, JsonOptions)
                ?? throw new InvalidDataException(
                    "La risposta del Game Master AI è vuota.");
        }
        catch (JsonException exception)
        {
            var field = string.IsNullOrWhiteSpace(exception.Path)
                ? string.Empty
                : $" Campo: {exception.Path}.";
            throw new InvalidDataException(
                "La risposta del Game Master AI non rispetta il protocollo JSON." +
                field,
                exception);
        }

        if (document.PlayerActionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(document.Narration))
        {
            throw new InvalidDataException(
                "La risposta del Game Master AI non identifica l'azione o la narrazione.");
        }

        var consequences = document.Consequences ?? [];
        if (consequences.Count > MaximumConsequences)
        {
            throw new InvalidDataException(
                "Il Game Master AI ha proposto troppe conseguenze in un turno.");
        }

        return new AiGmTurnPlan(
            document.PlayerActionId,
            document.Narration.Trim(),
            ToRoll(document.Roll),
            consequences.Select(ToProposal).ToArray());
    }

    private static AiGmRollRequest? ToRoll(RollDocument? roll)
    {
        if (roll is null)
        {
            return null;
        }

        if (!Enum.TryParse<D20RollMode>(
            roll.Mode,
            ignoreCase: true,
            out var mode))
        {
            throw new InvalidDataException(
                "La modalità del tiro proposta dall'AI non è valida.");
        }

        return new AiGmRollRequest(
            roll.Modifier,
            roll.Difficulty,
            roll.DifficultyVisible,
            mode,
            Required(roll.Reason, "reason"));
    }

    private static AiGmCommandProposal ToProposal(
        ConsequenceDocument consequence)
    {
        ArgumentNullException.ThrowIfNull(consequence);

        var reason = Required(consequence.Reason, "reason");
        return consequence.Kind switch
        {
            "moveEntity" => new MoveEntityProposal(
                Entity(consequence.EntityId, "entityId"),
                Location(consequence.LocationId, "locationId"),
                reason),
            "transferCoins" => new TransferCoinsProposal(
                Entity(consequence.EntityId, "entityId"),
                Entity(consequence.OtherEntityId, "otherEntityId"),
                Amount(consequence.Amount),
                reason),
            "acquireResource" => new AcquireResourceProposal(
                Entity(consequence.EntityId, "entityId"),
                Resource(consequence.ResourceId, "resourceId"),
                Amount(consequence.Amount),
                reason),
            "loseResource" => new LoseResourceProposal(
                Entity(consequence.EntityId, "entityId"),
                Resource(consequence.ResourceId, "resourceId"),
                Amount(consequence.Amount),
                reason),
            "transferResource" => new TransferResourceProposal(
                Entity(consequence.EntityId, "entityId"),
                Entity(consequence.OtherEntityId, "otherEntityId"),
                Resource(consequence.ResourceId, "resourceId"),
                Amount(consequence.Amount),
                reason),
            "revealFact" => new RevealFactProposal(
                Entity(consequence.EntityId, "entityId"),
                new FactId(Required(consequence.FactId, "factId")),
                reason),
            "changeTrust" => new ChangeTrustProposal(
                Entity(consequence.EntityId, "entityId"),
                Entity(consequence.OtherEntityId, "otherEntityId"),
                Amount(consequence.Amount),
                reason),
            _ => throw new InvalidDataException(
                $"La conseguenza AI '{consequence.Kind}' non è consentita.")
        };
    }

    private static EntityId Entity(string? value, string field) =>
        new(Required(value, field));

    private static LocationId Location(string? value, string field) =>
        new(Required(value, field));

    private static ResourceId Resource(string? value, string field) =>
        new(Required(value, field));

    private static int Amount(int? value) =>
        value ?? throw new InvalidDataException(
            "La quantità della conseguenza AI è obbligatoria.");

    private static string Required(string? value, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidDataException(
                $"Il campo AI '{field}' è obbligatorio.");
        }

        return normalized;
    }

    public const string ResponseInstructions = """
Restituisci un solo oggetto JSON, senza markdown o testo esterno:
{
  "playerActionId": "guid dell'azione ricevuta",
  "narration": "esito narrato senza decidere nuove azioni del giocatore",
  "roll": null,
  "consequences": [
    {
      "kind": "moveEntity | transferCoins | acquireResource | loseResource | transferResource | revealFact | changeTrust",
      "entityId": "id principale",
      "otherEntityId": "id secondario quando richiesto",
      "locationId": "luogo quando richiesto",
      "resourceId": "risorsa quando richiesta",
      "factId": "fatto quando richiesto",
      "amount": 1,
      "reason": "motivazione aderente a stato e memoria"
    }
  ]
}
Usa soltanto identificatori presenti nel dossier. Usa null per i campi non necessari.
Scrivi narration dalla prospettiva authorizedPerspective. Non trasformare informazioni
presenti soltanto nel catalogo, nella cronaca canonica o nelle memorie di altri attori
in conoscenza del giocatore. Un nuovo fatto canonico comunicato nel turno richiede
anche revealFact verso il personaggio giocante.
""";

    public const string ResponseSchemaJson = """
{
  "type": "object",
  "properties": {
    "playerActionId": { "type": "string" },
    "narration": { "type": "string" },
    "roll": { "type": "null" },
    "consequences": {
      "type": "array",
      "maxItems": 20,
      "items": {
        "type": "object",
        "properties": {
          "kind": {
            "type": "string",
            "enum": [
              "moveEntity",
              "transferCoins",
              "acquireResource",
              "loseResource",
              "transferResource",
              "revealFact",
              "changeTrust"
            ]
          },
          "entityId": { "type": ["string", "null"] },
          "otherEntityId": { "type": ["string", "null"] },
          "locationId": { "type": ["string", "null"] },
          "resourceId": { "type": ["string", "null"] },
          "factId": { "type": ["string", "null"] },
          "amount": { "type": ["integer", "null"] },
          "reason": { "type": "string" }
        },
        "required": [
          "kind",
          "entityId",
          "otherEntityId",
          "locationId",
          "resourceId",
          "factId",
          "amount",
          "reason"
        ],
        "additionalProperties": false
      }
    }
  },
  "required": ["playerActionId", "narration", "roll", "consequences"],
  "additionalProperties": false
}
""";

    private sealed record PlanDocument(
        Guid PlayerActionId,
        string? Narration,
        RollDocument? Roll,
        IReadOnlyList<ConsequenceDocument>? Consequences);

    private sealed record RollDocument(
        int Modifier,
        int? Difficulty,
        bool DifficultyVisible,
        string? Mode,
        string? Reason);

    private sealed record ConsequenceDocument(
        string? Kind,
        string? EntityId,
        string? OtherEntityId,
        string? LocationId,
        string? ResourceId,
        string? FactId,
        int? Amount,
        string? Reason);
}
