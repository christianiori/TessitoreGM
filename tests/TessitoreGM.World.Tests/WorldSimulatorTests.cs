using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldSimulatorTests
{
    [Fact]
    public void Advance_MultipleProposals_AppliesChronologicallyNearestEvent()
    {
        var entityId = new EntityId("traveler");
        var locationId = new LocationId("village");
        var firstAt = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var secondAt = firstAt.AddHours(1);
        IWorldRule[] rules =
        {
            new FixedRule(new EntityEnteredLocation(entityId, locationId, firstAt)),
            new FixedRule(new EntityLeftLocation(entityId, locationId, secondAt))
        };

        var result = new WorldSimulator(rules).Advance(WorldSnapshot.Empty, secondAt);

        Assert.IsType<EntityEnteredLocation>(Assert.Single(result.ProducedEvents));
        Assert.Equal(locationId, result.World.GetLocation(entityId));
        Assert.Equal(firstAt, result.World.CurrentTime);
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

    [Fact]
    public void Advance_ProposalBeyondLimit_Throws()
    {
        var limit = new DateTimeOffset(
            2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var proposedEvent = new WorldTimeAdvanced(limit.AddMinutes(1));
        var simulator = new WorldSimulator(
            new IWorldRule[] { new FixedRule(proposedEvent) });

        Assert.Throws<InvalidOperationException>(() =>
            simulator.Advance(WorldSnapshot.Empty, limit));
    }

    [Fact]
    public void Advance_EqualTimeProposals_PreservesRuleOrder()
    {
        var entityId = new EntityId("traveler");
        var firstLocationId = new LocationId("market");
        var secondLocationId = new LocationId("village");
        var at = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var simulator = new WorldSimulator(new IWorldRule[]
        {
            new FixedRule(new EntityEnteredLocation(entityId, firstLocationId, at)),
            new FixedRule(new EntityEnteredLocation(entityId, secondLocationId, at))
        });

        var result = simulator.Advance(WorldSnapshot.Empty, at);

        Assert.Equal(firstLocationId, result.World.GetLocation(entityId));
    }

    private sealed class FixedRule : IWorldRule
    {
        private readonly IWorldEvent? _worldEvent;

        public FixedRule(IWorldEvent? worldEvent)
        {
            _worldEvent = worldEvent;
        }

        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) => _worldEvent;
    }
}
