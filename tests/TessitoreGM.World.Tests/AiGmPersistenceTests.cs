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
    public void RecordPlan_RollRequestPersistsWithoutApplyingConsequences()
    {
        var (eventLog, action, context) = CreateTurn(
            "Forzo la serratura corrosa.");
        var plan = new AiGmTurnPlan(
            action.Id,
            "La serratura è corrosa e oppone resistenza.",
            new AiGmRollRequest(
                2,
                14,
                true,
                D20RollMode.Normal,
                "Forzare la serratura senza spezzare la chiave."),
            []);

        var updated = new AiGmTurnCoordinator().RecordPlan(
            eventLog,
            context,
            plan);
        var restored = new WorldEventJsonSerializer().Deserialize(
            new WorldEventJsonSerializer().Serialize(updated));

        var persistedAction = Assert.Single(restored.PlayerActions!);
        Assert.Equal(PlayerActionStatus.RollRequested, persistedAction.Status);
        Assert.Equal(14, persistedAction.Roll?.Difficulty);
        Assert.Equal(2, persistedAction.Roll?.Modifier);
        Assert.Contains("serratura", persistedAction.Roll?.Reason);
        Assert.Contains("corrosa", persistedAction.Roll?.PromptNarration);
        Assert.Null(restored.AiGmTurns);
        Assert.Equal(_innId, Replay(restored).GetLocation(_npcId));
    }

    [Fact]
    public void RecordPlan_RollRequestCannotApplyEarlyConsequences()
    {
        var (eventLog, action, context) = CreateTurn(
            "Provo ad aprire la porta bloccata.");
        var plan = new AiGmTurnPlan(
            action.Id,
            "La porta resiste mentre l'oste osserva.",
            new AiGmRollRequest(
                0,
                12,
                false,
                D20RollMode.Normal,
                "Aprire la porta bloccata."),
            [new MoveEntityProposal(
                _npcId,
                _squareId,
                "L'oste esce prima del risultato.")]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AiGmTurnCoordinator().RecordPlan(
                eventLog,
                context,
                plan));

        Assert.Contains("prima del risultato", exception.Message);
    }

    [Fact]
    public void RecordPlan_ImportantPlayerLossSurvivesSerializationWithoutApplying()
    {
        var (eventLog, action, context) = CreateTurn(
            "Cerco di salvare il pane che sta cadendo nel fuoco.");
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
    public void RecordPlan_DiscardsUngroundedPlayerEconomyWithoutBlockingNarration()
    {
        var (eventLog, action, context) = CreateTurn(
            "Cerco di distrarre l'oste.");
        var coordinator = new AiGmTurnCoordinator();

        var updated = coordinator.RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "L'oste si volta verso il rumore.",
                null,
                [new TransferResourceProposal(
                    _npcId,
                    _playerId,
                    _breadId,
                    5,
                    "L'oste consegna un premio non richiesto.")]));

        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(updated.PlayerActions!).Status);
        var turn = Assert.Single(updated.AiGmTurns!);
        Assert.Equal(AiGmTurnStatus.Completed, turn.Status);
        Assert.Equal("L'oste si volta verso il rumore.", turn.Narration);
        Assert.Empty(turn.Consequences);
        Assert.Equal(1, Replay(updated).GetResourceQuantity(
            _playerId,
            _breadId));
    }

    [Fact]
    public void RecordPlan_RemovesPlayerAgencySentenceWithoutBlockingNpcReaction()
    {
        var (eventLog, action, context) = CreateTurn(
            "Faccio una battuta all'oste.");
        var coordinator = new AiGmTurnCoordinator();

        var updated = coordinator.RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "Ada entra nella locanda. L'oste scoppia a ridere e posa lo straccio sul bancone.",
                null,
                []));

        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(updated.PlayerActions!).Status);
        var turn = Assert.Single(updated.AiGmTurns!);
        Assert.Equal(
            "L'oste scoppia a ridere e posa lo straccio sul bancone.",
            turn.Narration);
        Assert.DoesNotContain("Ada entra", turn.Narration);
    }

    [Fact]
    public void RecordPlan_ReplacesEntirelyUnsafeNarrationWithNeutralFallback()
    {
        var (eventLog, action, context) = CreateTurn(
            "Faccio una battuta all'oste.");

        var updated = new AiGmTurnCoordinator().RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "Ada decide di entrare nella locanda.",
                null,
                []));

        var narration = Assert.Single(updated.AiGmTurns!).Narration;
        Assert.Contains("Oste", narration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reagisce", narration);
        Assert.DoesNotContain("Ada decide", narration);
    }

    [Fact]
    public void RecordPlan_ReplacesNarrationThatIgnoresNamedVisibleTarget()
    {
        var (eventLog, action, context) = CreateTurn(
            "Faccio una battuta all'oste.");

        var updated = new AiGmTurnCoordinator().RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "Alcuni utensili sono accatastati in un angolo.",
                null,
                []));

        var narration = Assert.Single(updated.AiGmTurns!).Narration;
        Assert.Contains("Oste", narration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reagisce", narration);
        Assert.DoesNotContain("utensili", narration);
    }

    [Fact]
    public void RecordPlan_RemovesUnobservableNpcThoughts()
    {
        var (eventLog, action, context) = CreateTurn(
            "Faccio una battuta all'oste.");

        var updated = new AiGmTurnCoordinator().RecordPlan(
            eventLog,
            context,
            new AiGmTurnPlan(
                action.Id,
                "L'oste sorride e posa lo straccio. L'oste spera di farti ridere.",
                null,
                []));

        var narration = Assert.Single(updated.AiGmTurns!).Narration;
        Assert.Equal("L'oste sorride e posa lo straccio.", narration);
        Assert.DoesNotContain("spera", narration);
    }

    [Fact]
    public void ResolveConsequence_ApprovalAppliesEventAndCompletesAction()
    {
        var (eventLog, action, context) = CreateTurn(
            "Cerco di salvare il pane che sta cadendo nel fuoco.");
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
        var (eventLog, action, context) = CreateTurn(
            "Cerco di salvare il pane che sta cadendo dal tavolo.");
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
        AiGmTurnContext Context) CreateTurn(string? actionDescription = null)
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            actionDescription ?? "Chiedo all'oste di accompagnarmi in piazza.",
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
