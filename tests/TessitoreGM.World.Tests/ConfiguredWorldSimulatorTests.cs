using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class ConfiguredWorldSimulatorTests
{
    [Fact]
    public void Advance_ConfiguredDailyNeed_ProducesEventAndUpdatesWorld()
    {
        var npcId = new EntityId("innkeeper");
        var hungerId = new NeedId("hunger");
        var initialTime = At(6);
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                initialTime,
                new[] { new EntityBalance(npcId, 10) }),
            Array.Empty<IWorldEvent>(),
            new WorldSimulationDefinition(
                new[]
                {
                    new NpcSimulationDefinition(
                        npcId,
                        "innkeeper",
                        Array.Empty<ScheduledArrivalDefinition>(),
                        Array.Empty<OrderProductionDefinition>(),
                        DailyNeedIncreases: new[]
                        {
                            new DailyNeedIncreaseDefinition(
                                hungerId,
                                TimeSpan.FromHours(8),
                                15)
                        })
                }));
        var world = WorldSnapshot.Create(
            initialTime,
            new Dictionary<EntityId, int> { [npcId] = 10 });

        var result = new ConfiguredWorldSimulator().Advance(
            eventLog,
            world,
            At(9));

        var increased = Assert.IsType<NeedIncreased>(
            Assert.Single(result.ProducedEvents.OfType<NeedIncreased>()));
        Assert.Equal(npcId, increased.EntityId);
        Assert.Equal(hungerId, increased.NeedId);
        Assert.Equal(15, result.World.GetNeedLevel(npcId, hungerId));
        Assert.Equal(At(9), result.World.CurrentTime);
    }

    private static DateTimeOffset At(int hour) =>
        new(2026, 8, 3, hour, 0, 0, TimeSpan.Zero);
}
