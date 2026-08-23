using TessitoreGM.AiGm;
using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class AiGmFoundationTests
{
    private readonly EntityId _playerId = new("player");
    private readonly EntityId _npcId = new("innkeeper");
    private readonly LocationId _innId = new("inn");
    private readonly LocationId _squareId = new("square");
    private readonly ResourceId _breadId = new("bread");

    [Fact]
    public void Build_ReconstructsCanonicalAndSeparateActorMemories()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Chiedo all'oste che cosa ricorda della chiave.",
            At(9, 5));
        var secret = new FactId("secret-key");
        var knownRumor = new FactId("known-rumor");
        var eventLog = CreateEventLog(
            action,
            new FactRevealed(_npcId, secret, At(9, 0)),
            new FactRevealed(_playerId, knownRumor, At(9, 1)));

        var context = new AiGmContextBuilder().Build(eventLog, action);

        Assert.Equal(action.Id, context.PlayerActionId);
        Assert.Equal(action.Description, context.PlayerAction);
        Assert.Contains(AiGmInvariants.HumanActionRule, context.Rules);
        Assert.Contains(context.Memory.CanonicalChronicle, remembered =>
            remembered.EventType == nameof(FactRevealed));

        var npcMemory = Assert.Single(context.Memory.ActorMemories,
            memory => memory.EntityId == _npcId);
        Assert.Contains(secret, npcMemory.KnownFacts);

        var playerMemory = Assert.Single(context.Memory.ActorMemories,
            memory => memory.EntityId == _playerId);
        Assert.DoesNotContain(secret, playerMemory.KnownFacts);

        Assert.Equal(_playerId, context.AuthorizedPerspective.Player.EntityId);
        Assert.Equal(10, context.AuthorizedPerspective.Player.Coins);
        Assert.Contains(
            context.AuthorizedPerspective.Player.Resources,
            resource =>
                resource.ResourceId == _breadId &&
                resource.Quantity == 1);
        Assert.Equal(_innId, context.AuthorizedPerspective.Scene.LocationId);
        Assert.Contains(
            context.AuthorizedPerspective.Scene.VisibleCharacters,
            character => character.EntityId == _npcId);
        Assert.DoesNotContain(
            secret,
            context.AuthorizedPerspective.KnownFacts);
        Assert.Contains(
            knownRumor,
            context.AuthorizedPerspective.KnownFacts);
        Assert.Single(
            context.AuthorizedPerspective.ObservedEvents,
            remembered => remembered.EventType == nameof(FactRevealed));

        var catalog = Assert.IsType<AiGmCampaignCatalog>(context.Catalog);
        Assert.Contains(catalog.Characters,
            character => character.EntityId == _npcId);
        Assert.Contains(catalog.Locations,
            location => location.LocationId == _squareId);
        Assert.Contains(catalog.Resources,
            resource => resource.ResourceId == _breadId);
        Assert.Contains(secret, catalog.Facts);
    }

    [Fact]
    public void Build_RejectsActionThatWasNotPersistedByAPlayer()
    {
        var persisted = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Entro nella locanda.",
            At(9, 0));
        var invented = persisted with
        {
            Id = Guid.NewGuid(),
            Description = "L'AI decide che il giocatore attacca."
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AiGmContextBuilder().Build(CreateEventLog(persisted), invented));

        Assert.Contains("persisted human player action", exception.Message);
    }

    [Fact]
    public void Build_IncludesOnlyActorsInThePlayersCurrentScene()
    {
        var outsideNpcId = new EntityId("smith");
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Parlo con l'oste.",
            At(9, 5));
        var baseLog = CreateEventLog(action);
        var simulation = baseLog.Simulation!;
        var eventLog = baseLog with
        {
            Events = baseLog.Events
                .Append(new EntityEnteredLocation(
                    outsideNpcId,
                    _squareId,
                    At(8, 3)))
                .ToArray(),
            Simulation = simulation with
            {
                Npcs = simulation.Npcs
                    .Append(new NpcSimulationDefinition(
                        outsideNpcId,
                        "Fabbro",
                        [],
                        []))
                    .ToArray(),
                Entities = simulation.Entities!
                    .Append(new EntityPresentationDefinition(
                        outsideNpcId,
                        "Fabbro"))
                    .ToArray()
            }
        };

        var context = new AiGmContextBuilder().Build(eventLog, action);

        Assert.DoesNotContain(
            context.World.Actors,
            actor => actor.EntityId == outsideNpcId);
        Assert.DoesNotContain(
            context.Memory.ActorMemories,
            memory => memory.EntityId == outsideNpcId);
        Assert.Contains(
            context.Catalog!.Characters,
            character => character.EntityId == outsideNpcId);
        Assert.DoesNotContain(
            context.AuthorizedPerspective.Scene.VisibleCharacters,
            character => character.EntityId == outsideNpcId);
    }

    [Fact]
    public void Build_ContinuesCompletedNarrationFromTheSameScene()
    {
        var previousAction = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Chiedo all'oste se ha visto la chiave.",
            At(9, 0),
            PlayerActionStatus.Approved,
            "Risolta dal Game Master AI.",
            At(9, 0));
        var currentAction = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Gli domando chi altro potrebbe averla presa.",
            At(9, 5));
        var baseLog = CreateEventLog(currentAction);
        var eventLog = baseLog with
        {
            PlayerActions = [previousAction, currentAction],
            AiGmTurns =
            [
                new AiGmTurnRecord(
                    Guid.NewGuid(),
                    previousAction.Id,
                    _playerId,
                    "L'oste abbassa la voce: la chiave era sul bancone.",
                    At(9, 0),
                    AiGmTurnStatus.Completed,
                    [],
                    _innId,
                    [_playerId, _npcId])
            ]
        };

        var context = new AiGmContextBuilder().Build(
            eventLog,
            currentAction);

        var exchange = Assert.Single(context.Memory.SceneHistory!);
        Assert.Equal(previousAction.Description, exchange.PlayerAction);
        Assert.Contains("sul bancone", exchange.Narration);
        Assert.Equal(
            context.Memory.SceneHistory,
            context.AuthorizedPerspective.SceneHistory);
    }

    [Fact]
    public void Policy_PlayerLossRequiresConfirmation_ButNpcMovementIsRoutine()
    {
        var policy = new AiGmConsequencePolicy([_playerId]);

        var loss = policy.Assess(new LoseResourceProposal(
            _playerId,
            _breadId,
            1,
            "Il pane cade nel fiume."));
        var movement = policy.Assess(new MoveEntityProposal(
            _npcId,
            _squareId,
            "L'oste va al mercato."));

        Assert.Equal(
            AiGmConsequenceImportance.RequiresConfirmation,
            loss.Importance);
        Assert.Equal(AiGmConsequenceImportance.Routine, movement.Importance);
    }

    [Fact]
    public void Validator_RejectsImpossibleTransfer()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Compro del pane.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var world = Replay(eventLog);
        var validator = new AiGmProposalValidator(eventLog, world);

        var result = validator.Validate(new TransferCoinsProposal(
            _playerId,
            _npcId,
            99,
            "Pagamento."));

        Assert.False(result.IsValid);
        Assert.Contains("abbastanza monete", result.Error);
    }

    [Fact]
    public void TurnPlanValidator_RejectsPlanForDifferentPlayerAction()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Parlo con l'oste.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));
        var plan = new AiGmTurnPlan(
            Guid.NewGuid(),
            "L'oste ti ascolta.",
            null,
            []);

        var result = validator.Validate(context, plan);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("azione"));
    }

    private WorldEventLog CreateEventLog(
        PlayerActionProposal action,
        params IWorldEvent[] additionalEvents)
    {
        IWorldEvent[] events =
        [
            new PlayerCharacterRegistered(_playerId, "Ada", At(8, 0)),
            new EntityEnteredLocation(_playerId, _innId, At(8, 1)),
            new EntityEnteredLocation(_npcId, _innId, At(8, 2)),
            .. additionalEvents
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

        return new WorldEventLog(
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
