using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldSimulatorTests
{
    [Fact]
    public void Advance_MultipleProposals_AppliesAllEventsInChronologicalOrder()
    {
        var entityId = new EntityId("traveler");
        var locationId = new LocationId("village");
        var firstAt = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var secondAt = firstAt.AddHours(1);
        IWorldRule[] rules =
        {
            new FutureEventRule(new EntityEnteredLocation(entityId, locationId, firstAt)),
            new FutureEventRule(new EntityLeftLocation(entityId, locationId, secondAt))
        };

        var result = new WorldSimulator(rules).Advance(WorldSnapshot.Empty, secondAt);

        Assert.Collection(
            result.ProducedEvents,
            worldEvent => Assert.IsType<EntityEnteredLocation>(worldEvent),
            worldEvent => Assert.IsType<EntityLeftLocation>(worldEvent));
        Assert.Null(result.World.GetLocation(entityId));
        Assert.Equal(secondAt, result.World.CurrentTime);
    }

    [Fact]
    public void Advance_RuleWithNoProposal_AdvancesWorldClock()
    {
        var world = WorldSnapshot.Empty;
        var at = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var simulator = new WorldSimulator(Array.Empty<IWorldRule>());

        var result = simulator.Advance(world, at);

        var timeAdvanced = Assert.IsType<WorldTimeAdvanced>(
            Assert.Single(result.ProducedEvents));
        Assert.Equal(at, timeAdvanced.OccurredAt);
        Assert.Equal(at, result.World.CurrentTime);
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
            new IWorldRule[] { new AlwaysProposingRule(proposedEvent) });

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
            new FutureEventRule(new EntityEnteredLocation(entityId, firstLocationId, at)),
            new FutureEventRule(new EntityEnteredLocation(entityId, secondLocationId, at))
        });

        var result = simulator.Advance(WorldSnapshot.Empty, at);

        Assert.Equal(firstLocationId, result.World.GetLocation(entityId));
    }

    [Fact]
    public void Advance_TooManyEvents_ThrowsWithoutChangingOriginalWorld()
    {
        var currentTime = new DateTimeOffset(
            2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var world = WorldSnapshot.Create(
            currentTime,
            new Dictionary<EntityId, int>());
        var simulator = new WorldSimulator(
            new IWorldRule[] { new IncrementingTimeRule() },
            maxEvents: 3);

        Assert.Throws<InvalidOperationException>(() =>
            simulator.Advance(world, currentTime.AddMinutes(1)));
        Assert.Equal(currentTime, world.CurrentTime);
    }

    private sealed class FutureEventRule : IWorldRule
    {
        private readonly IWorldEvent _worldEvent;

        public FutureEventRule(IWorldEvent worldEvent)
        {
            _worldEvent = worldEvent;
        }

        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) =>
            _worldEvent.OccurredAt > world.CurrentTime ? _worldEvent : null;
    }

    private sealed class AlwaysProposingRule : IWorldRule
    {
        private readonly IWorldEvent _worldEvent;

        public AlwaysProposingRule(IWorldEvent worldEvent)
        {
            _worldEvent = worldEvent;
        }

        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) => _worldEvent;
    }

    private sealed class IncrementingTimeRule : IWorldRule
    {
        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) =>
            new WorldTimeAdvanced(world.CurrentTime.AddTicks(1));
    }
}
