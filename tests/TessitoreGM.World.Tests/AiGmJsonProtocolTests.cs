using TessitoreGM.AiGm;
using TessitoreGM.Core;

namespace TessitoreGM.World.Tests;

public sealed class AiGmJsonProtocolTests
{
    [Fact]
    public void CreatePrompt_ContainsCanonicalRulesAndReadOnlyContext()
    {
        var actionId = Guid.NewGuid();
        var context = new AiGmTurnContext(
            actionId,
            new EntityId("player"),
            "Ada",
            "Parlo con l'oste.",
            new AiGmWorldState(
                DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
                WeatherCondition.Clear,
                []),
            new AiGmMemoryDossier([], []),
            new AiGmAuthorizedPerspective(
                DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
                WeatherCondition.Clear,
                new AiGmPerspectivePlayer(
                    new EntityId("player"),
                    "Ada",
                    10,
                    []),
                new AiGmPerspectiveScene(
                    new LocationId("inn"),
                    "Locanda",
                    []),
                [],
                [],
                []),
            AiGmInvariants.Rules,
            new AiGmCampaignCatalog(
                [],
                [new AiGmCatalogLocation(
                    new LocationId("inn"),
                    "Locanda")],
                [],
                [],
                []));

        var prompt = new AiGmJsonProtocol().CreatePrompt(context);

        Assert.Contains(AiGmInvariants.HumanActionRule, prompt.SystemInstructions);
        Assert.Contains(actionId.ToString(), prompt.ContextJson);
        Assert.Contains("\"name\": \"Locanda\"", prompt.ContextJson);
        Assert.Contains("\"authorizedPerspective\"", prompt.ContextJson);
        Assert.Contains(
            "lista chiusa",
            prompt.SystemInstructions);
        Assert.Contains("sola lettura", prompt.SystemInstructions);
        Assert.Contains("moveEntity", prompt.ResponseInstructions);
        Assert.Contains("revealFact", prompt.ResponseInstructions);
        Assert.Contains(
            "prospettiva authorizedPerspective",
            prompt.ResponseInstructions);
    }

    [Fact]
    public void DeserializePlan_MapsOnlyTypedConsequences()
    {
        var actionId = Guid.NewGuid();
        const string json = """
        {
          "narration": "L'oste apre la porta.",
          "roll": null,
          "consequences": [
            {
              "kind": "moveEntity",
              "entityId": "innkeeper",
              "otherEntityId": null,
              "locationId": "square",
              "resourceId": null,
              "factId": null,
              "amount": null,
              "reason": "Inizia il mercato."
            }
          ]
        }
        """;

        var plan = new AiGmJsonProtocol().DeserializePlan(json, actionId);

        Assert.Equal(actionId, plan.PlayerActionId);
        var movement = Assert.IsType<MoveEntityProposal>(
            Assert.Single(plan.Consequences));
        Assert.Equal(new EntityId("innkeeper"), movement.EntityId);
        Assert.Equal(new LocationId("square"), movement.DestinationId);
    }

    [Fact]
    public void DeserializePlan_RejectsUnknownCommands()
    {
        const string json = """
        {
          "narration": "Il giocatore obbedisce.",
          "roll": null,
          "consequences": [
            {
              "kind": "decidePlayerAction",
              "entityId": "player",
              "otherEntityId": null,
              "locationId": null,
              "resourceId": null,
              "factId": null,
              "amount": null,
              "reason": "Scelta automatica."
            }
          ]
        }
        """;

        var exception = Assert.Throws<InvalidDataException>(() =>
            new AiGmJsonProtocol().DeserializePlan(json, Guid.NewGuid()));

        Assert.Contains("non è consentita", exception.Message);
    }

    [Fact]
    public void DeserializePlan_RejectsUnexpectedFields()
    {
        const string json = """
        {
          "narration": "L'oste risponde.",
          "roll": null,
          "consequences": [],
          "writeFile": "village.json"
        }
        """;

        Assert.Throws<InvalidDataException>(() =>
            new AiGmJsonProtocol().DeserializePlan(json, Guid.NewGuid()));
    }

    [Fact]
    public void DeserializePlan_ReportsTheInvalidField()
    {
        const string json = """
        {
          "narration": "L'oste risponde.",
          "roll": null,
          "consequences": "non-un-elenco"
        }
        """;

        var exception = Assert.Throws<InvalidDataException>(() =>
            new AiGmJsonProtocol().DeserializePlan(json, Guid.NewGuid()));

        Assert.Contains("consequences", exception.Message);
    }
}
