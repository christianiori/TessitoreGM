using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class ScheduledEntityArrivalRuleTests
{
    private readonly EntityId _travelerId = new("traveler");
    private readonly LocationId _villageId = new("village");

    [Fact]
    public void Advance_BeforeScheduledTime_DoesNotMoveEntity()
    {
        var simulator = CreateSimulator(At(10, 0));

        var result = simulator.Advance(WorldSnapshot.Empty, At(9, 59));

        Assert.IsType<WorldTimeAdvanced>(Assert.Single(result.ProducedEvents));
        Assert.Null(result.World.GetLocation(_travelerId));
        Assert.Equal(At(9, 59), result.World.CurrentTime);
    }

    [Fact]
    public void Advance_AtScheduledTime_MovesEntityToDestination()
    {
        var simulator = CreateSimulator(At(10, 0));

        var result = simulator.Advance(WorldSnapshot.Empty, At(10, 0));

        var arrival = Assert.IsType<EntityEnteredLocation>(
            Assert.Single(result.ProducedEvents));
        Assert.Equal(_travelerId, arrival.EntityId);
        Assert.Equal(_villageId, arrival.LocationId);
        Assert.Equal(_villageId, result.World.GetLocation(_travelerId));
    }

    [Fact]
    public void Advance_WhenAlreadyAtDestination_DoesNotRepeatArrival()
    {
        var simulator = CreateSimulator(At(10, 0));
        var firstResult = simulator.Advance(WorldSnapshot.Empty, At(10, 0));

        var secondResult = simulator.Advance(firstResult.World, At(11, 0));

        Assert.IsType<WorldTimeAdvanced>(
            Assert.Single(secondResult.ProducedEvents));
        Assert.Equal(At(11, 0), secondResult.World.CurrentTime);
    }

    private WorldSimulator CreateSimulator(DateTimeOffset scheduledAt) =>
        new(new IWorldRule[]
        {
            new ScheduledEntityArrivalRule(
                _travelerId,
                _villageId,
                scheduledAt)
        });

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
