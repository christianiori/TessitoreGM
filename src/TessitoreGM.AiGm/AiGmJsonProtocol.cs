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
            "chiusa di ciò che il giocatore può già percepire o ricordare. " +
            "actionFrame identifica categoria, luogo e bersagli dell'azione: " +
            "la risposta deve restare centrata su questi dati.\n" +
            rules;

        return new AiGmProviderPrompt(
            systemInstructions,
            JsonSerializer.Serialize(context, JsonOptions),
            ResponseInstructions);
    }

    public AiGmTurnPlan DeserializePlan(
        string json,
        Guid playerActionId)
    {
        if (playerActionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The player action id cannot be empty.",
                nameof(playerActionId));
        }

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

        if (string.IsNullOrWhiteSpace(document.Narration))
        {
            throw new InvalidDataException(
                "La risposta del Game Master AI non contiene una narrazione.");
        }

        var consequences = document.Consequences ?? [];
        if (consequences.Count > MaximumConsequences)
        {
            throw new InvalidDataException(
                "Il Game Master AI ha proposto troppe conseguenze in un turno.");
        }

        return new AiGmTurnPlan(
            playerActionId,
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
  "narration": "esito narrato senza decidere nuove azioni del giocatore",
  "roll": null,
  "consequences": [
    {
      "kind": "uno dei tipi consentiti",
      "campi": "soltanto quelli previsti per quel tipo"
    }
  ]
}
Usa soltanto identificatori presenti nel dossier. Usa null per i campi non necessari.
Le forme ammesse per una conseguenza sono esattamente queste:
- moveEntity: kind, entityId, locationId, reason.
- transferCoins: kind, entityId (pagatore), otherEntityId (destinatario), amount, reason.
- acquireResource o loseResource: kind, entityId, resourceId, amount, reason.
- transferResource: kind, entityId (sorgente), otherEntityId (destinatario), resourceId, amount, reason.
- revealFact: kind, entityId (chi apprende), factId, reason.
- changeTrust: kind, entityId (chi cambia opinione), otherEntityId, amount, reason.
Non aggiungere gli altri campi alla conseguenza. Se nessuna conseguenza canonica è
necessaria, restituisci consequences come array vuoto.
Non modificare monete o risorse del personaggio giocante se la sua azione non esprime
un intento economico o materiale, come comprare, vendere, pagare, prendere, cedere,
raccogliere o chiedere una ricompensa. Una normale azione sociale non crea oggetti,
denaro o premi: in quel caso narra soltanto la reazione e lascia consequences vuoto.
Se actionFrame contiene un bersaglio, centra la risposta su quel personaggio. Se il
bersaglio ha isVisible falso, non inventare un incontro o una reazione a distanza:
spiega semplicemente che non è presente nella scena corrente.
Se rollResult è null, non richiedere tiri per azioni ordinarie, sicure o prive di una
conseguenza significativa in caso di fallimento. Richiedi un tiro soltanto quando
l'azione è davvero difficile, rischiosa o pericolosa e il suo esito è incerto. In tal
caso sostituisci roll con un oggetto contenente esattamente modifier,
difficulty, difficultyVisible, mode e reason; usa modificatore da -10 a 10,
difficoltà da 5 a 30 e mode Normal, Advantage o Disadvantage. consequences deve
essere vuoto e narration introduce soltanto la situazione incerta.
Se rollResult è presente, il tiro è già stato generato e verificato da Tessitore:
usa succeeded e total per risolvere l'azione, imposta roll a null e non inventare o
modificare dadi, difficoltà o risultato.
Scrivi narration dalla prospettiva authorizedPerspective. Non trasformare informazioni
presenti soltanto nel catalogo, nella cronaca canonica o nelle memorie di altri attori
in conoscenza del giocatore. Un nuovo fatto canonico comunicato nel turno richiede
anche revealFact verso il personaggio giocante.
Non usare mai il nome del personaggio giocante come soggetto della narrazione e non
iniziare frasi con "Tu" o "Ti". Descrivi ciò che fanno i PNG e ciò che il PG può
percepire, senza aggiungere movimenti, parole, pensieri, intenzioni o decisioni del PG.
""";

    public const string ResponseSchemaJson = """
{
  "type": "object",
  "properties": {
    "narration": { "type": "string" },
    "roll": {
      "type": ["object", "null"],
      "properties": {
        "modifier": { "type": "integer", "minimum": -10, "maximum": 10 },
        "difficulty": { "type": "integer", "minimum": 5, "maximum": 30 },
        "difficultyVisible": { "type": "boolean" },
        "mode": {
          "type": "string",
          "enum": ["Normal", "Advantage", "Disadvantage"]
        },
        "reason": { "type": "string" }
      },
      "required": [
        "modifier",
        "difficulty",
        "difficultyVisible",
        "mode",
        "reason"
      ],
      "additionalProperties": false
    },
    "consequences": {
      "type": "array",
      "maxItems": 20,
      "items": {
        "oneOf": [
          {
            "type": "object",
            "properties": {
              "kind": { "const": "moveEntity" },
              "entityId": { "type": "string" },
              "locationId": { "type": "string" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "locationId", "reason"],
            "additionalProperties": false
          },
          {
            "type": "object",
            "properties": {
              "kind": { "const": "transferCoins" },
              "entityId": { "type": "string" },
              "otherEntityId": { "type": "string" },
              "amount": { "type": "integer" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "otherEntityId", "amount", "reason"],
            "additionalProperties": false
          },
          {
            "type": "object",
            "properties": {
              "kind": { "enum": ["acquireResource", "loseResource"] },
              "entityId": { "type": "string" },
              "resourceId": { "type": "string" },
              "amount": { "type": "integer" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "resourceId", "amount", "reason"],
            "additionalProperties": false
          },
          {
            "type": "object",
            "properties": {
              "kind": { "const": "transferResource" },
              "entityId": { "type": "string" },
              "otherEntityId": { "type": "string" },
              "resourceId": { "type": "string" },
              "amount": { "type": "integer" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "otherEntityId", "resourceId", "amount", "reason"],
            "additionalProperties": false
          },
          {
            "type": "object",
            "properties": {
              "kind": { "const": "revealFact" },
              "entityId": { "type": "string" },
              "factId": { "type": "string" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "factId", "reason"],
            "additionalProperties": false
          },
          {
            "type": "object",
            "properties": {
              "kind": { "const": "changeTrust" },
              "entityId": { "type": "string" },
              "otherEntityId": { "type": "string" },
              "amount": { "type": "integer" },
              "reason": { "type": "string" }
            },
            "required": ["kind", "entityId", "otherEntityId", "amount", "reason"],
            "additionalProperties": false
          }
        ]
      }
    }
  },
  "required": ["narration", "roll", "consequences"],
  "additionalProperties": false
}
""";

    private sealed record PlanDocument(
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
