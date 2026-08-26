using TessitoreGM.AiGm;
using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class AiGmTurnExecutorTests
{
    private readonly EntityId _playerId = new("player");
    private readonly EntityId _npcId = new("innkeeper");
    private readonly LocationId _innId = new("inn");
    private readonly LocationId _squareId = new("square");

    [Fact]
    public async Task ExecuteAsync_ValidPlanPassesThroughCoordinator()
    {
        var (eventLog, action) = CreateTurn();
        var provider = new StubGameMaster(context => new AiGmTurnPlan(
            context.PlayerActionId,
            "L'oste annuisce e raggiunge la piazza.",
            null,
            [new MoveEntityProposal(
                _npcId,
                _squareId,
                "L'oste apre il banco del mercato.")]));

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, action);

        Assert.Equal(AiGmTurnExecutionStatus.Completed, result.Status);
        Assert.True(result.WorldChanged);
        Assert.Single(result.EventLog.AiGmTurns!);
        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(result.EventLog.PlayerActions!).Status);
    }

    [Fact]
    public async Task ExecuteAsync_RollRequestWaitsForTessitoreD20()
    {
        var (eventLog, action) = CreateTurn();
        var provider = new StubGameMaster(context => new AiGmTurnPlan(
            context.PlayerActionId,
            "La porta chiusa oppone resistenza.",
            new AiGmRollRequest(
                1,
                13,
                true,
                D20RollMode.Advantage,
                "Aprire la porta sotto pressione."),
            []));

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, action);

        Assert.Equal(AiGmTurnExecutionStatus.RollRequested, result.Status);
        Assert.True(result.WorldChanged);
        Assert.Equal(
            PlayerActionStatus.RollRequested,
            Assert.Single(result.EventLog.PlayerActions!).Status);
        Assert.Null(result.EventLog.AiGmTurns);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvedTessitoreRollCompletesAiTurn()
    {
        var (baseLog, action) = CreateTurn();
        var rolledAction = action with
        {
            Status = PlayerActionStatus.Rolled,
            Roll = new D20Roll(
                1,
                13,
                true,
                D20RollMode.Normal,
                At(9, 0),
                [16],
                16,
                17,
                At(9, 1),
                "Aprire la porta.",
                "La porta oppone resistenza.")
        };
        var eventLog = baseLog with { PlayerActions = [rolledAction] };
        var provider = new StubGameMaster(context =>
        {
            var rollResult = Assert.IsType<AiGmResolvedRoll>(
                context.RollResult);
            Assert.True(rollResult.Succeeded);
            Assert.Equal(17, rollResult.Total);
            return new AiGmTurnPlan(
                context.PlayerActionId,
                "La serratura scatta e la porta si apre.",
                null,
                []);
        });

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, rolledAction);

        Assert.Equal(AiGmTurnExecutionStatus.Completed, result.Status);
        Assert.Equal(
            PlayerActionStatus.Approved,
            Assert.Single(result.EventLog.PlayerActions!).Status);
        Assert.Single(result.EventLog.AiGmTurns!);
    }

    [Fact]
    public async Task ExecuteAsync_UnavailableProviderLeavesActionForHumanGm()
    {
        var (eventLog, action) = CreateTurn();
        var provider = new StubGameMaster(_ =>
            throw new AiGmProviderUnavailableException(
                "Servizio AI temporaneamente non disponibile."));

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, action);

        Assert.Equal(
            AiGmTurnExecutionStatus.ProviderUnavailable,
            result.Status);
        Assert.False(result.WorldChanged);
        Assert.Same(eventLog, result.EventLog);
        Assert.Null(result.EventLog.AiGmTurns);
        Assert.Equal(
            PlayerActionStatus.Pending,
            Assert.Single(result.EventLog.PlayerActions!).Status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPlanLeavesActionForHumanGm()
    {
        var (eventLog, action) = CreateTurn();
        var provider = new StubGameMaster(_ => new AiGmTurnPlan(
            Guid.NewGuid(),
            "Decido al posto del giocatore.",
            null,
            []));

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, action);

        Assert.Equal(AiGmTurnExecutionStatus.InvalidPlan, result.Status);
        Assert.False(result.WorldChanged);
        Assert.Same(eventLog, result.EventLog);
        Assert.Null(result.EventLog.AiGmTurns);
        Assert.Equal(
            PlayerActionStatus.Pending,
            Assert.Single(result.EventLog.PlayerActions!).Status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidProviderResponseLeavesActionForHumanGm()
    {
        var (eventLog, action) = CreateTurn();
        var provider = new StubGameMaster(_ =>
            throw new InvalidDataException("JSON non valido."));

        var result = await new AiGmTurnExecutor(provider)
            .ExecuteAsync(eventLog, action);

        Assert.Equal(AiGmTurnExecutionStatus.InvalidPlan, result.Status);
        Assert.False(result.WorldChanged);
        Assert.Same(eventLog, result.EventLog);
        Assert.Null(result.EventLog.AiGmTurns);
        Assert.Contains("JSON non valido", result.Message);
    }

    private (WorldEventLog EventLog, PlayerActionProposal Action) CreateTurn()
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
            ]);
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(8, 0),
                [
                    new EntityBalance(_playerId, 10),
                    new EntityBalance(_npcId, 20)
                ]),
            events,
            simulation,
            [action]);

        return (eventLog, action);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 23, hour, minute, 0, TimeSpan.Zero);

    private sealed class StubGameMaster(
        Func<AiGmTurnContext, AiGmTurnPlan> plan) : IAiGameMaster
    {
        public Task<AiGmTurnPlan> PlanTurnAsync(
            AiGmTurnContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(plan(context));
    }
}
