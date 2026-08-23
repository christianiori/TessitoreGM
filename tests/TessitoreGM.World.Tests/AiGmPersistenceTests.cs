using TessitoreGM.AiGm;
using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class AiGmPersistenceTests
{
    private readonly EntityId _playerId = new("player");
    private readonly EntityId _npcId = new("innkeeper");
    private readonly LocationId _innId = new("inn");
    private readonly LocationId _squareId = new("square");
    private readonly ResourceId _breadId = new("bread");

    [Fact]
    public void RecordPlan_RoutineNpcMovementIsAppliedAndActionCompletes()
    {
        var (eventLog, action, context) = CreateTurn();
        var plan = new AiGmTurnPlan(
            action.Id,
            "L'oste annuisce e raggiunge la piazza.",
            null,
            [new MoveEntityProposal(
                _npcId,
                _squareId,
                "L'oste apre il banco del mercato.")]);

        var updated = new AiGmTurnCoordinator().RecordPlan(
            eventLog,
            context,
            plan);

        Assert.Equal(_squareId, Replay(updated).GetLocation(_npcId));
        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(updated.PlayerActions!).Status);
        var turn = Assert.Single(updated.AiGmTurns!);
        Assert.Equal(AiGmTurnStatus.Completed, turn.Status);
        Assert.Equal(_innId, turn.SceneLocationId);
        Assert.Contains(_playerId, turn.SceneActorIds!);
        Assert.Contains(_npcId, turn.SceneActorIds!);
        Assert.Equal(
            AiGmConsequenceStatus.Applied,
            Assert.Single(turn.Consequences).Status);
    }

    [Fact]
    public void RecordPlan_ImportantPlayerLossSurvivesSerializationWithoutApplying()
    {
        var (eventLog, action, context) = CreateTurn();
        var plan = new AiGmTurnPlan(
            action.Id,
            "Il pane scivola verso il bordo del tavolo.",
            null,
            [new LoseResourceProposal(
                _playerId,
                _breadId,
                1,
                "Il pane rischia di cadere nel fuoco.")]);
        var coordinator = new AiGmTurnCoordinator();

        var proposed = coordinator.RecordPlan(eventLog, context, plan);
        var reloaded = new WorldEventJsonSerializer().Deserialize(
            new WorldEventJsonSerializer().Serialize(proposed));

        var reloadedTurn = Assert.Single(reloaded.AiGmTurns!);
        Assert.Equal(_innId, reloadedTurn.SceneLocationId);
        Assert.Contains(_npcId, reloadedTurn.SceneActorIds!);
        Assert.Equal(1, Replay(reloaded).GetResourceQuantity(
            _playerId,
            _breadId));
        Assert.Equal(
            PlayerActionStatus.Pending,
            Assert.Single(reloaded.PlayerActions!).Status);
        var pending = Assert.Single(coordinator.PendingConsequences(reloaded));
        Assert.Equal(
            AiGmConsequenceKind.LoseResource,
            pending.Consequence.Kind);
        Assert.Equal(
            AiGmConsequenceStatus.PendingConfirmation,
            pending.Consequence.Status);
    }

    [Fact]
    public void ResolveConsequence_ApprovalAppliesEventAndCompletesAction()
    {
        var (eventLog, action, context) = CreateTurn();
        var coordinator = new AiGmTurnCoordinator();
        var proposed = coordinator.RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "Il pane cade nel fuoco.",
                null,
                [new LoseResourceProposal(
                    _playerId,
                    _breadId,
                    1,
                    "Il pane brucia.")]));
        var consequenceId = Assert.Single(
            coordinator.PendingConsequences(proposed)).Consequence.Id;

        var approved = coordinator.ResolveConsequence(
            proposed,
            consequenceId,
            approve: true,
            "Conseguenza confermata dal GM.");

        Assert.Equal(0, Replay(approved).GetResourceQuantity(
            _playerId,
            _breadId));
        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(approved.PlayerActions!).Status);
        Assert.Empty(coordinator.PendingConsequences(approved));
        var consequence = Assert.Single(
            Assert.Single(approved.AiGmTurns!).Consequences);
        Assert.Equal(AiGmConsequenceStatus.Applied, consequence.Status);
        Assert.Equal(
            "Conseguenza confermata dal GM.",
            consequence.Resolution);
    }

    [Fact]
    public void ResolveConsequence_RejectionPreservesWorldAndRecordsDecision()
    {
        var (eventLog, action, context) = CreateTurn();
        var coordinator = new AiGmTurnCoordinator();
        var proposed = coordinator.RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "Il pane vacilla sul tavolo.",
                null,
                [new LoseResourceProposal(
                    _playerId,
                    _breadId,
                    1,
                    "Il pane potrebbe cadere.")]));
        var consequenceId = Assert.Single(
            coordinator.PendingConsequences(proposed)).Consequence.Id;

        var rejected = coordinator.ResolveConsequence(
            proposed,
            consequenceId,
            approve: false,
            "Il tavolo è stabile.");

        Assert.Equal(1, Replay(rejected).GetResourceQuantity(
            _playerId,
            _breadId));
        var consequence = Assert.Single(
            Assert.Single(rejected.AiGmTurns!).Consequences);
        Assert.Equal(AiGmConsequenceStatus.Rejected, consequence.Status);
        Assert.Equal("Il tavolo è stabile.", consequence.Resolution);
    }

    private (WorldEventLog EventLog, PlayerActionProposal Action,
        AiGmTurnContext Context) CreateTurn()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Chiedo all'oste di accompagnarmi in piazza.",
            At(9, 0));
        IWorldEvent[] events =
        [
            new PlayerCharacterRegistered(_playerId, "Ada", At(8, 0)),
            new EntityEnteredLocation(_playerId, _innId, At(8, 1)),
            new EntityEnteredLocation(_npcId, _innId, At(8, 2))
        ];
        var simulation = new WorldSimulationDefinition(
            [new NpcSimulationDefinition(_npcId, "Oste", [], [])],
            [
                new EntityPresentationDefinition(_playerId, "Ada"),
                new EntityPresentationDefinition(_npcId, "Oste")
            ],
            [
                new LocationPresentationDefinition(_innId, "Locanda"),
                new LocationPresentationDefinition(_squareId, "Piazza")
            ],
            [new ResourcePresentationDefinition(_breadId, "Pane")]);
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(8, 0),
                [
                    new EntityBalance(_playerId, 10),
                    new EntityBalance(_npcId, 20)
                ],
                [new EntityResourceStock(_playerId, _breadId, 1)]),
            events,
            simulation,
            [action]);

        return (
            eventLog,
            action,
            new AiGmContextBuilder().Build(eventLog, action));
    }

    private static WorldSnapshot Replay(WorldEventLog eventLog) =>
        new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                eventLog.InitialWorld.CurrentTime,
                eventLog.InitialWorld.Balances.ToDictionary(
                    balance => balance.EntityId,
                    balance => balance.Amount),
                eventLog.InitialWorld.ResourceStocks ?? [],
                eventLog.InitialWorld.Weather),
            eventLog.Events);

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 23, hour, minute, 0, TimeSpan.Zero);
}
