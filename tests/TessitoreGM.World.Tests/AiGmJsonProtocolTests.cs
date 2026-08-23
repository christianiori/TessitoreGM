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
            AiGmInvariants.Rules);

        var prompt = new AiGmJsonProtocol().CreatePrompt(context);

        Assert.Contains(AiGmInvariants.HumanActionRule, prompt.SystemInstructions);
        Assert.Contains(actionId.ToString(), prompt.ContextJson);
        Assert.Contains("sola lettura", prompt.SystemInstructions);
        Assert.Contains("moveEntity", prompt.ResponseInstructions);
    }

    [Fact]
    public void DeserializePlan_MapsOnlyTypedConsequences()
    {
        var actionId = Guid.NewGuid();
        var json = $$"""
        {
          "playerActionId": "{{actionId}}",
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

        var plan = new AiGmJsonProtocol().DeserializePlan(json);

        Assert.Equal(actionId, plan.PlayerActionId);
        var movement = Assert.IsType<MoveEntityProposal>(
            Assert.Single(plan.Consequences));
        Assert.Equal(new EntityId("innkeeper"), movement.EntityId);
        Assert.Equal(new LocationId("square"), movement.DestinationId);
    }

    [Fact]
    public void DeserializePlan_RejectsUnknownCommands()
    {
        var json = $$"""
        {
          "playerActionId": "{{Guid.NewGuid()}}",
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
            new AiGmJsonProtocol().DeserializePlan(json));

        Assert.Contains("non è consentita", exception.Message);
    }

    [Fact]
    public void DeserializePlan_RejectsUnexpectedFields()
    {
        var json = $$"""
        {
          "playerActionId": "{{Guid.NewGuid()}}",
          "narration": "L'oste risponde.",
          "roll": null,
          "consequences": [],
          "writeFile": "village.json"
        }
        """;

        Assert.Throws<InvalidDataException>(() =>
            new AiGmJsonProtocol().DeserializePlan(json));
    }
}
