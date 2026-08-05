using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldCampaignTemplateTests
{
    [Fact]
    public void CreateFresh_AdvancedCampaign_KeepsOnlyInitialStateAndPlan()
    {
        var npcId = new EntityId("innkeeper");
        var homeId = new LocationId("home");
        var innId = new LocationId("inn");
        var initialTime = At(6);
        var simulation = new WorldSimulationDefinition(
            new[]
            {
                new NpcSimulationDefinition(
                    npcId,
                    "innkeeper",
                    Array.Empty<ScheduledArrivalDefinition>(),
                    Array.Empty<OrderProductionDefinition>())
            });
        var source = new WorldEventLog(
            new WorldInitialState(
                initialTime,
                new[] { new EntityBalance(npcId, 10) }),
            new IWorldEvent[]
            {
                new EntityEnteredLocation(npcId, homeId, initialTime),
                new EntityEnteredLocation(npcId, innId, initialTime),
                new EntityEnteredLocation(npcId, innId, At(13)),
                new WorldTimeAdvanced(At(18))
            },
            simulation);

        var fresh = new WorldCampaignTemplate().CreateFresh(source);

        Assert.Same(source.InitialWorld, fresh.InitialWorld);
        Assert.Same(simulation, fresh.Simulation);
        var arrival = Assert.IsType<EntityEnteredLocation>(
            Assert.Single(fresh.Events));
        Assert.Equal(homeId, arrival.LocationId);
        Assert.Equal(initialTime, arrival.OccurredAt);
    }

    private static DateTimeOffset At(int hour) =>
        new(2026, 8, 3, hour, 0, 0, TimeSpan.Zero);
}
