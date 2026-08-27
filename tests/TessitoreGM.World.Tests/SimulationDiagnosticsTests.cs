using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Npcs;
using TessitoreGM.World;

namespace TessitoreGM.World.Tests;

public sealed class SimulationDiagnosticsTests
{
    [Fact]
    public void Analyze_UpcomingDay_ReportsRulesBehaviorsAndProposedEvents()
    {
        var now = At(3, 8);
        var npcId = new EntityId("baker");
        var bakeryId = new LocationId("bakery");
        var initial = new WorldInitialState(
            now,
            Array.Empty<EntityBalance>());
        var simulation = new WorldSimulationDefinition(
            new[]
            {
                new NpcSimulationDefinition(
                    npcId,
                    "baker",
                    new[] { new ScheduledArrivalDefinition(bakeryId, At(3, 9)) },
                    Array.Empty<OrderProductionDefinition>())
            });
        var log = new WorldEventLog(
            initial,
            Array.Empty<IWorldEvent>(),
            simulation);
        var world = WorldSnapshot.Create(now, new Dictionary<EntityId, int>());

        var report = new SimulationDiagnostics().Analyze(
            log,
            world,
            TimeSpan.FromHours(24));

        Assert.Equal(1, report.RuleCount);
        Assert.Equal(1, report.BehaviorCount);
        Assert.Contains(report.ProposedEvents, worldEvent =>
            worldEvent is EntityEnteredLocation entered &&
            entered.EntityId == npcId &&
            entered.LocationId == bakeryId);
        Assert.Equal(now.AddHours(24), report.Until);
        Assert.Equal(now, world.CurrentTime);
    }

    [Fact]
    public void Analyze_WithoutSimulation_ExplainsConfigurationIsMissing()
    {
        var now = At(3, 8);
        var log = new WorldEventLog(
            new WorldInitialState(now, Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>());
        var world = WorldSnapshot.Create(now, new Dictionary<EntityId, int>());

        var exception = Assert.Throws<InvalidDataException>(() =>
            new SimulationDiagnostics().Analyze(
                log,
                world,
                TimeSpan.FromHours(24)));

        Assert.Contains("configurazione della simulazione", exception.Message);
    }

    [Fact]
    public void Analyze_WithNonPositiveHorizon_RejectsRequest()
    {
        var now = At(3, 8);
        var log = new WorldEventLog(
            new WorldInitialState(now, Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>());
        var world = WorldSnapshot.Create(now, new Dictionary<EntityId, int>());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SimulationDiagnostics().Analyze(log, world, TimeSpan.Zero));
    }

    [Fact]
    public void Analyze_ExternalRule_IsIncludedInActiveRuleCount()
    {
        var now = At(3, 8);
        var log = new WorldEventLog(
            new WorldInitialState(now, Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            new WorldSimulationDefinition(
                Array.Empty<NpcSimulationDefinition>()));
        var world = WorldSnapshot.Create(now, new Dictionary<EntityId, int>());
        var externalRules = new[]
        {
            new WorldRuleRegistration("tests:external", new IdleRule())
        };

        var report = new SimulationDiagnostics().Analyze(
            log,
            world,
            TimeSpan.FromHours(24),
            externalRules);

        Assert.Equal(1, report.RuleCount);
    }

    private sealed class IdleRule : IWorldRule
    {
        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) => null;
    }

    private static DateTimeOffset At(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, TimeSpan.Zero);
}
