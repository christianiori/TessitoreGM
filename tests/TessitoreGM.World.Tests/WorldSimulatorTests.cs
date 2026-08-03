using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldSimulatorTests
{
    [Fact]
    public void Advance_AppliesEventsProposedByDifferentRules()
    {
        var entityId = new EntityId("traveler");
        var locationId = new LocationId("village");
        var at = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        IWorldRule[] rules =
        {
            new FixedRule(new EntityEnteredLocation(entityId, locationId, at)),
            new FixedRule(new EntityLeftLocation(entityId, locationId, at))
        };

        var result = new WorldSimulator(rules).Advance(WorldSnapshot.Empty, at);

        Assert.Equal(2, result.ProducedEvents.Count);
        Assert.IsType<EntityEnteredLocation>(result.ProducedEvents[0]);
        Assert.IsType<EntityLeftLocation>(result.ProducedEvents[1]);
        Assert.Null(result.World.GetLocation(entityId));
        Assert.Equal(at, result.World.CurrentTime);
    }

    [Fact]
    public void Advance_RuleWithNoProposal_LeavesWorldUnchanged()
    {
        var world = WorldSnapshot.Empty;
        var at = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var simulator = new WorldSimulator(new IWorldRule[] { new FixedRule(null) });

        var result = simulator.Advance(world, at);

        Assert.Same(world, result.World);
        Assert.Empty(result.ProducedEvents);
    }

    [Fact]
    public void Advance_TimeBeforeWorldTime_Throws()
    {
        var currentTime = new DateTimeOffset(
            2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var world = WorldSnapshot.Create(
            currentTime,
            new Dictionary<EntityId, int>());
        var simulator = new WorldSimulator(Array.Empty<IWorldRule>());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            simulator.Advance(world, currentTime.AddMinutes(-1)));
    }

    private sealed class FixedRule : IWorldRule
    {
        private readonly IWorldEvent? _worldEvent;

        public FixedRule(IWorldEvent? worldEvent)
        {
            _worldEvent = worldEvent;
        }

        public IWorldEvent? Evaluate(
            WorldSnapshot world,
            DateTimeOffset currentTime) => _worldEvent;
    }
}
