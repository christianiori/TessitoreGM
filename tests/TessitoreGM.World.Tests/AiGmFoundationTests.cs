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
        var frame = Assert.IsType<AiGmActionFrame>(context.ActionFrame);
        Assert.Equal(AiGmActionKind.Social, frame.Kind);
        var target = Assert.Single(frame.Targets);
        Assert.Equal(_npcId, target.EntityId);
        Assert.True(target.IsVisible);
    }

    [Fact]
    public void ActionAnalyzer_RecognizesNamedTargetOutsideCurrentScene()
    {
        var targetId = new EntityId("farmer");
        var frame = new AiGmActionAnalyzer().Analyze(
            "Faccio una battuta a Brina.",
            new AiGmPerspectiveScene(_innId, "Locanda", []),
            new AiGmCampaignCatalog(
                [new AiGmCatalogCharacter(
                    targetId,
                    "Brina la contadina",
                    false,
                    "Contadina",
                    new LocationId("farm"),
                    "Cascina")],
                [],
                [],
                [],
                []),
            _playerId);

        Assert.Equal(AiGmActionKind.Social, frame.Kind);
        var target = Assert.Single(frame.Targets);
        Assert.Equal(targetId, target.EntityId);
        Assert.False(target.IsVisible);
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
    public void Build_ExposesOnlyThePersistedTessitoreRollResult()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Provo ad aprire la porta bloccata.",
            At(9, 0),
            PlayerActionStatus.Rolled,
            Roll: new D20Roll(
                2,
                14,
                true,
                D20RollMode.Normal,
                At(9, 0),
                [11],
                11,
                13,
                At(9, 1),
                "Aprire la porta.",
                "La serratura è corrosa."));

        var context = new AiGmContextBuilder().Build(
            CreateEventLog(action),
            action);

        var result = Assert.IsType<AiGmResolvedRoll>(context.RollResult);
        Assert.Equal(new[] { 11 }, result.Dice);
        Assert.Equal(13, result.Total);
        Assert.Equal(14, result.Difficulty);
        Assert.False(result.Succeeded);
        Assert.Contains("corrosa", result.PromptNarration);
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

    [Theory]
    [InlineData("Ada si avvicina all'oste e inizia a imitarlo.")]
    [InlineData("Ada decide di seguire l'oste fuori dalla locanda.")]
    [InlineData("Ada pensa che l'oste stia mentendo.")]
    [InlineData("Ti avvicini all'oste e pensi a cosa domandargli.")]
    public void TurnPlanValidator_RejectsNarrationThatActsForThePlayer(
        string narration)
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Rimango fermo e osservo la locanda.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(action.Id, narration, null, []));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("giocatore umano"));
    }

    [Fact]
    public void TurnPlanValidator_AcceptsExternalWorldReaction()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Rimango fermo e osservo la locanda.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(
                action.Id,
                "L'oste alza lo sguardo verso di te e continua a pulire il bancone.",
                null,
                []));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("L'oste osserva Ada con crescente sospetto.")]
    [InlineData("Brina si allontana da Ada e stringe il sacco al petto.")]
    [InlineData("Il rumore alle spalle di Ada si interrompe di colpo.")]
    public void TurnPlanValidator_AcceptsPlayerNameWhenNotSentenceSubject(
        string narration)
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Rimango fermo e osservo la locanda.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(action.Id, narration, null, []));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TurnPlanValidator_ReportsTheAgencyFragment()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Rimango fermo e osservo la locanda.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(
                action.Id,
                "Ada decide di uscire dalla locanda.",
                null,
                []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("Ada decide", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TurnPlanValidator_RejectsPlayerResourceRewardWithoutMaterialIntent()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Cerco di distrarre l'oste.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(
                action.Id,
                "L'oste si volta verso il rumore.",
                null,
                [new AcquireResourceProposal(
                    _playerId,
                    new ResourceId("grain"),
                    5,
                    "L'oste consegna del grano.")]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("intento economico", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TurnPlanValidator_AcceptsPlayerResourceChangeWithMaterialIntent()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Cerco di comprare del pane dall'oste.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(
                action.Id,
                "L'oste posa il pane sul bancone.",
                null,
                [new AcquireResourceProposal(
                    _playerId,
                    new ResourceId("bread"),
                    1,
                    "Il giocatore riceve il pane acquistato.")]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TurnPlanValidator_RejectsRollOutsideConfiguredBounds()
    {
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            _playerId,
            "Forzo la porta.",
            At(9, 0));
        var eventLog = CreateEventLog(action);
        var context = new AiGmContextBuilder().Build(eventLog, action);
        var validator = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, Replay(eventLog)));

        var result = validator.Validate(
            context,
            new AiGmTurnPlan(
                action.Id,
                "La porta oppone una forte resistenza.",
                new AiGmRollRequest(
                    30,
                    99,
                    true,
                    D20RollMode.Normal,
                    "Forzare la porta."),
                []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("modificatore"));
        Assert.Contains(result.Errors, error => error.Contains("difficoltà"));
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
