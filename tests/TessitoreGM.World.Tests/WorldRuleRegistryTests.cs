using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.World.Tests;

public sealed class WorldRuleRegistryTests
{
    [Fact]
    public void ExternalRule_RegisteredThroughPublicApi_ProducesValidatedEvent()
    {
        var now = At(3, 8);
        var entityId = new EntityId("visitor");
        var locationId = new LocationId("market");
        var registry = new WorldRuleRegistry()
            .Register(
                "example:market-arrival",
                new ArrivalRule(entityId, locationId, now.AddHours(1)));

        var result = registry.CreateSimulator().Advance(
            EmptyWorld(now),
            now.AddHours(2));

        var entered = Assert.IsType<EntityEnteredLocation>(
            result.ProducedEvents[0]);
        Assert.Equal(entityId, entered.EntityId);
        Assert.Equal(locationId, result.World.GetLocation(entityId));
    }

    [Fact]
    public void Register_DuplicateIdentifier_IsRejectedIgnoringCase()
    {
        var registry = new WorldRuleRegistry()
            .Register("example:arrival", new EmptyRule());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register("EXAMPLE:ARRIVAL", new EmptyRule()));

        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void RulesWithSameTime_RunInRegistrationOrder()
    {
        var now = At(3, 8);
        var occurredAt = now.AddHours(1);
        var first = new EntityId("first");
        var second = new EntityId("second");
        var market = new LocationId("market");
        var registry = new WorldRuleRegistry()
            .Register("example:first", new ArrivalRule(first, market, occurredAt))
            .Register("example:second", new ArrivalRule(second, market, occurredAt));

        var result = registry.CreateSimulator().Advance(
            EmptyWorld(now),
            now.AddHours(2));

        var arrivals = result.ProducedEvents
            .OfType<EntityEnteredLocation>()
            .ToArray();
        Assert.Equal(first, arrivals[0].EntityId);
        Assert.Equal(second, arrivals[1].EntityId);
    }

    [Fact]
    public void ExternalRule_UnsupportedEvent_IsRejectedByOfficialProcessor()
    {
        var now = At(3, 8);
        var registry = new WorldRuleRegistry()
            .Register("example:unsupported", new UnsupportedRule(now.AddHours(1)));

        var exception = Assert.Throws<NotSupportedException>(() =>
            registry.CreateSimulator().Advance(
                EmptyWorld(now),
                now.AddHours(2)));

        Assert.Contains(nameof(ExternalUnsupportedEvent), exception.Message);
    }

    private static WorldSnapshot EmptyWorld(DateTimeOffset now) =>
        WorldSnapshot.Create(now, new Dictionary<EntityId, int>());

    private static DateTimeOffset At(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, TimeSpan.Zero);

    private sealed class ArrivalRule : IWorldRule
    {
        private readonly EntityId _entityId;
        private readonly LocationId _locationId;
        private readonly DateTimeOffset _occurredAt;

        public ArrivalRule(
            EntityId entityId,
            LocationId locationId,
            DateTimeOffset occurredAt)
        {
            _entityId = entityId;
            _locationId = locationId;
            _occurredAt = occurredAt;
        }

        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) =>
            world.GetLocation(_entityId) is null && _occurredAt <= until
                ? new EntityEnteredLocation(
                    _entityId,
                    _locationId,
                    _occurredAt)
                : null;
    }

    private sealed class EmptyRule : IWorldRule
    {
        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) => null;
    }

    private sealed class UnsupportedRule : IWorldRule
    {
        private readonly DateTimeOffset _occurredAt;

        public UnsupportedRule(DateTimeOffset occurredAt) =>
            _occurredAt = occurredAt;

        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) =>
            new ExternalUnsupportedEvent(_occurredAt);
    }

    private sealed record ExternalUnsupportedEvent(
        DateTimeOffset OccurredAt) : IWorldEvent;
}
