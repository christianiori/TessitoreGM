using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class DailyLocationRoutineBehaviorTests
{
    [Fact]
    public void Advance_ThreeSimultaneousRoutines_MoveThreeNpcsDeterministically()
    {
        var bakerId = new EntityId("baker");
        var farmerId = new EntityId("farmer");
        var innkeeperId = new EntityId("innkeeper");
        var bakeryId = new LocationId("bakery");
        var fieldsId = new LocationId("fields");
        var innId = new LocationId("inn");
        var world = WorldSnapshot.Create(
            At(3, 8, 0),
            new Dictionary<EntityId, int>());
        IWorldRule[] rules =
        {
            CreateAgent(bakerId, "baker", bakeryId, AtTime(9, 0)),
            CreateAgent(farmerId, "farmer", fieldsId, AtTime(9, 0)),
            CreateAgent(innkeeperId, "innkeeper", innId, AtTime(9, 0))
        };

        var result = new WorldSimulator(rules).Advance(world, At(3, 10, 0));

        Assert.Collection(
            result.ProducedEvents,
            worldEvent => Assert.Equal(
                bakerId,
                Assert.IsType<EntityEnteredLocation>(worldEvent).EntityId),
            worldEvent => Assert.Equal(
                farmerId,
                Assert.IsType<EntityEnteredLocation>(worldEvent).EntityId),
            worldEvent => Assert.Equal(
                innkeeperId,
                Assert.IsType<EntityEnteredLocation>(worldEvent).EntityId),
            worldEvent => Assert.IsType<WorldTimeAdvanced>(worldEvent));
        Assert.Equal(bakeryId, result.World.GetLocation(bakerId));
        Assert.Equal(fieldsId, result.World.GetLocation(farmerId));
        Assert.Equal(innId, result.World.GetLocation(innkeeperId));
    }

    [Fact]
    public void Advance_RoutineRepeatsOnFollowingDayAfterNpcLeavesDestination()
    {
        var bakerId = new EntityId("baker");
        var bakeryId = new LocationId("bakery");
        var homeId = new LocationId("baker-home");
        var baker = new NpcAgent(
            bakerId,
            "baker",
            NpcMemory.Empty(bakerId),
            new INpcBehavior[]
            {
                new DailyLocationRoutineBehavior(bakeryId, AtTime(9, 0)),
                new DailyLocationRoutineBehavior(homeId, AtTime(18, 0))
            });
        var simulator = new WorldSimulator(new IWorldRule[] { baker });
        var world = WorldSnapshot.Create(
            At(3, 8, 0),
            new Dictionary<EntityId, int>());

        var firstDay = simulator.Advance(world, At(3, 20, 0));
        var secondDay = simulator.Advance(firstDay.World, At(4, 10, 0));

        Assert.Equal(homeId, firstDay.World.GetLocation(bakerId));
        Assert.Equal(bakeryId, secondDay.World.GetLocation(bakerId));
        Assert.Contains(
            secondDay.ProducedEvents,
            worldEvent =>
                worldEvent is EntityEnteredLocation arrival &&
                arrival.EntityId == bakerId &&
                arrival.LocationId == bakeryId &&
                arrival.OccurredAt == At(4, 9, 0));
    }

    private static NpcAgent CreateAgent(
        EntityId id,
        string role,
        LocationId destinationId,
        TimeSpan timeOfDay) =>
        new(
            id,
            role,
            NpcMemory.Empty(id),
            new INpcBehavior[]
            {
                new DailyLocationRoutineBehavior(destinationId, timeOfDay)
            });

    private static TimeSpan AtTime(int hour, int minute) =>
        new(hour, minute, 0);

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);
}
