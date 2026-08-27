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

    private static WorldEventLog CreateLog(
        WorldSimulationDefinition? simulation = null) => new(
            new WorldInitialState(
                new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            simulation);
}
