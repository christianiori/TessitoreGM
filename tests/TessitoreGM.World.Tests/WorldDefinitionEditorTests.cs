using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldDefinitionEditorTests
{
    [Fact]
    public void AddLocation_PreservesExistingWorldAndAddsPresentation()
    {
        var existing = new LocationPresentationDefinition(
            new LocationId("market"),
            "Mercato");
        var log = CreateLog(new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>(),
            Locations: new[] { existing }));

        var updated = new WorldDefinitionEditor().AddLocation(
            log,
            "  north-tower ",
            " Torre Nord ");

        Assert.Equal(log.Events, updated.Events);
        Assert.Equal(log.InitialWorld, updated.InitialWorld);
        Assert.Collection(
            updated.Simulation!.Locations!,
            location => Assert.Equal(existing, location),
            location =>
            {
                Assert.Equal(new LocationId("north-tower"), location.LocationId);
                Assert.Equal("Torre Nord", location.Name);
            });
    }

    [Fact]
    public void AddResource_WithoutSimulation_CreatesMinimalDefinition()
    {
        var log = CreateLog();

        var updated = new WorldDefinitionEditor().AddResource(
            log,
            "wood",
            "Legname");

        Assert.NotNull(updated.Simulation);
        Assert.Empty(updated.Simulation!.Npcs);
        var resource = Assert.Single(updated.Simulation.Resources!);
        Assert.Equal(new ResourceId("wood"), resource.ResourceId);
        Assert.Equal("Legname", resource.Name);
    }

    [Fact]
    public void AddLocation_DuplicateNameOrId_IsRejected()
    {
        var log = CreateLog(new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>(),
            Locations: new[]
            {
                new LocationPresentationDefinition(
                    new LocationId("market"),
                    "Mercato")
            }));
        var editor = new WorldDefinitionEditor();

        Assert.Throws<InvalidOperationException>(() =>
            editor.AddLocation(log, "MARKET", "Altro mercato"));
        Assert.Throws<InvalidOperationException>(() =>
            editor.AddLocation(log, "other-market", "mercato"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("spazio vietato")]
    [InlineData("città")]
    [InlineData("path/segment")]
    public void AddResource_InvalidIdentifier_IsRejected(string id)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new WorldDefinitionEditor().AddResource(
                CreateLog(),
                id,
                "Risorsa"));

        Assert.Contains("identificatore", exception.Message);
    }

    [Fact]
    public void AddNpc_AddsPresentationAgentAndInitialArrivalEvent()
    {
        var now = new DateTimeOffset(
            2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
        var locationId = new LocationId("north-tower");
        var log = CreateLog(new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>(),
            Locations: new[]
            {
                new LocationPresentationDefinition(locationId, "Torre Nord")
            }));

        var updated = new WorldDefinitionEditor().AddNpc(
            log,
            "alda",
            "Alda la guardia",
            "guardia",
            "north-tower",
            now);

        var entity = Assert.Single(updated.Simulation!.Entities!);
        Assert.Equal(new EntityId("alda"), entity.EntityId);
        Assert.Equal("Alda la guardia", entity.Name);
        var npc = Assert.Single(updated.Simulation.Npcs);
        Assert.Equal(entity.EntityId, npc.EntityId);
        Assert.Equal("guardia", npc.Role);
        Assert.Empty(npc.ScheduledArrivals);
        var arrival = Assert.IsType<EntityEnteredLocation>(
            Assert.Single(updated.Events));
        Assert.Equal(entity.EntityId, arrival.EntityId);
        Assert.Equal(locationId, arrival.LocationId);
        Assert.Equal(now, arrival.OccurredAt);
    }

    [Fact]
    public void AddNpc_UnknownInitialLocation_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new WorldDefinitionEditor().AddNpc(
                CreateLog(new WorldSimulationDefinition(
                    Array.Empty<NpcSimulationDefinition>())),
                "alda",
                "Alda",
                "guardia",
                "missing",
                DateTimeOffset.UtcNow));

        Assert.Contains("non esiste", exception.Message);
    }

    [Fact]
    public void AddNpc_IdAlreadyUsedByWorldState_IsRejected()
    {
        var now = new DateTimeOffset(
            2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
        var location = new LocationPresentationDefinition(
            new LocationId("market"),
            "Mercato");
        var log = new WorldEventLog(
            new WorldInitialState(
                now,
                new[] { new EntityBalance(new EntityId("alda"), 10) }),
            Array.Empty<IWorldEvent>(),
            new WorldSimulationDefinition(
                Array.Empty<NpcSimulationDefinition>(),
                Locations: new[] { location }));

        Assert.Throws<InvalidOperationException>(() =>
            new WorldDefinitionEditor().AddNpc(
                log,
                "alda",
                "Alda",
                "guardia",
                "market",
                now));
    }

    [Fact]
    public void AddNpcDailyRoutine_AddsSortedDeterministicBehavior()
    {
        var npcId = new EntityId("alda");
        var market = new LocationPresentationDefinition(
            new LocationId("market"),
            "Mercato");
        var home = new LocationPresentationDefinition(
            new LocationId("home"),
            "Casa");
        var log = CreateLog(new WorldSimulationDefinition(
            new[]
            {
                new NpcSimulationDefinition(
                    npcId,
                    "guardia",
                    Array.Empty<ScheduledArrivalDefinition>(),
                    Array.Empty<OrderProductionDefinition>())
            },
            Locations: new[] { market, home }));
        var editor = new WorldDefinitionEditor();

        var evening = editor.AddNpcDailyRoutine(
            log,
            "alda",
            "home",
            TimeSpan.FromHours(18));
        var complete = editor.AddNpcDailyRoutine(
            evening,
            "alda",
            "market",
            TimeSpan.FromHours(8));

        var routines = Assert.Single(complete.Simulation!.Npcs)
            .DailyLocationRoutines!;
        Assert.Collection(
            routines,
            routine =>
            {
                Assert.Equal(TimeSpan.FromHours(8), routine.TimeOfDay);
                Assert.Equal(market.LocationId, routine.DestinationId);
            },
            routine =>
            {
                Assert.Equal(TimeSpan.FromHours(18), routine.TimeOfDay);
                Assert.Equal(home.LocationId, routine.DestinationId);
            });
    }

    [Fact]
    public void AddNpcDailyRoutine_SameTimeTwice_IsRejected()
    {
        var npcId = new EntityId("alda");
        var market = new LocationPresentationDefinition(
            new LocationId("market"),
            "Mercato");
        var log = CreateLog(new WorldSimulationDefinition(
            new[]
            {
                new NpcSimulationDefinition(
                    npcId,
                    "guardia",
                    Array.Empty<ScheduledArrivalDefinition>(),
                    Array.Empty<OrderProductionDefinition>())
            },
            Locations: new[] { market }));
        var editor = new WorldDefinitionEditor();
        var once = editor.AddNpcDailyRoutine(
            log,
            "alda",
            "market",
            TimeSpan.FromHours(8));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            editor.AddNpcDailyRoutine(
                once,
                "alda",
                "market",
                TimeSpan.FromHours(8)));

        Assert.Contains("questo orario", exception.Message);
    }

    [Fact]
    public void AddNeed_AddsUniquePresentation()
    {
        var updated = new WorldDefinitionEditor().AddNeed(
            CreateLog(), "hunger", "Fame");

        var need = Assert.Single(updated.Simulation!.Needs!);
        Assert.Equal(new NeedId("hunger"), need.NeedId);
        Assert.Equal("Fame", need.Name);
        Assert.Throws<InvalidOperationException>(() =>
            new WorldDefinitionEditor().AddNeed(updated, "HUNGER", "Altro"));
    }

    [Fact]
    public void ConfigureNpcInitialState_SetsCoinsStockAndNeedWithoutRewritingHistory()
    {
        var now = new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
        var npcId = new EntityId("alda");
        var existingEvent = new EntityEnteredLocation(
            npcId, new LocationId("tower"), now);
        var log = new WorldEventLog(
            new WorldInitialState(now, Array.Empty<EntityBalance>()),
            new IWorldEvent[] { existingEvent },
            new WorldSimulationDefinition(
                new[] { new NpcSimulationDefinition(npcId, "guardia", Array.Empty<ScheduledArrivalDefinition>(), Array.Empty<OrderProductionDefinition>()) },
                Resources: new[] { new ResourcePresentationDefinition(new ResourceId("bread"), "Pane") },
                Needs: new[] { new NeedPresentationDefinition(new NeedId("hunger"), "Fame") }));

        var updated = new WorldDefinitionEditor().ConfigureNpcInitialState(
            log, "alda", 12, "bread", 3, "hunger", 25, now);

        Assert.Equal(12, Assert.Single(updated.InitialWorld.Balances).Amount);
        Assert.Equal(3, Assert.Single(updated.InitialWorld.ResourceStocks!).Quantity);
        Assert.Same(existingEvent, updated.Events[0]);
        var need = Assert.IsType<NeedIncreased>(updated.Events[1]);
        Assert.Equal(25, need.Amount);
    }

    [Fact]
    public void ConfigureNpcInitialState_AfterEconomicHistory_IsRejected()
    {
        var now = new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
        var npcId = new EntityId("alda");
        var log = new WorldEventLog(
            new WorldInitialState(now, Array.Empty<EntityBalance>()),
            new IWorldEvent[]
            {
                new ResourceAcquired(
                    npcId, new ResourceId("bread"), 1, "acquisto", now)
            },
            new WorldSimulationDefinition(
                new[] { new NpcSimulationDefinition(npcId, "guardia", Array.Empty<ScheduledArrivalDefinition>(), Array.Empty<OrderProductionDefinition>()) },
                Resources: new[] { new ResourcePresentationDefinition(new ResourceId("bread"), "Pane") }));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new WorldDefinitionEditor().ConfigureNpcInitialState(
                log, "alda", 10, "bread", 2, null, 0, now));

        Assert.Contains("cronologia", exception.Message);
    }

    [Fact]
    public void AddKnowledgeAndUpdateProfile_PreserveNpcIdentity()
    {
        var npcId = new EntityId("alda");
        var log = CreateLog(new WorldSimulationDefinition(
            new[] { new NpcSimulationDefinition(npcId, "guardia", Array.Empty<ScheduledArrivalDefinition>(), Array.Empty<OrderProductionDefinition>()) },
            Entities: new[] { new EntityPresentationDefinition(npcId, "Alda") }));
        var editor = new WorldDefinitionEditor();

        var learned = editor.AddNpcInitialKnowledge(log, "alda", "gate-open");
        var updated = editor.UpdateNpcProfile(
            learned, "alda", "Alda la sentinella", "sentinella");

        Assert.Equal(npcId, Assert.Single(updated.Simulation!.Npcs).EntityId);
        Assert.Equal("sentinella", Assert.Single(updated.Simulation.Npcs).Role);
        Assert.Contains(new FactId("gate-open"), Assert.Single(updated.Simulation.Npcs).InitialKnownFacts!);
        Assert.Equal("Alda la sentinella", Assert.Single(updated.Simulation.Entities!).Name);
    }

    [Fact]
    public void UpdateNpcDailyRoutine_ReplacesSelectedRoutineAndSorts()
    {
        var npcId = new EntityId("alda");
        var log = CreateLog(new WorldSimulationDefinition(
            new[]
            {
                new NpcSimulationDefinition(
                    npcId, "guardia", Array.Empty<ScheduledArrivalDefinition>(),
                    Array.Empty<OrderProductionDefinition>(),
                    new[] { new DailyLocationRoutineDefinition(new LocationId("home"), TimeSpan.FromHours(18)) })
            },
            Locations: new[]
            {
                new LocationPresentationDefinition(new LocationId("home"), "Casa"),
                new LocationPresentationDefinition(new LocationId("gate"), "Porta")
            }));

        var updated = new WorldDefinitionEditor().UpdateNpcDailyRoutine(
            log, "alda", TimeSpan.FromHours(18), "gate", TimeSpan.FromHours(7));

        var routine = Assert.Single(Assert.Single(updated.Simulation!.Npcs).DailyLocationRoutines!);
        Assert.Equal(new LocationId("gate"), routine.DestinationId);
        Assert.Equal(TimeSpan.FromHours(7), routine.TimeOfDay);
    }

    private static WorldEventLog CreateLog(
        WorldSimulationDefinition? simulation = null) => new(
            new WorldInitialState(
                new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            simulation);
}
