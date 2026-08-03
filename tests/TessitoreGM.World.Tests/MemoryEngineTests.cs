using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;

namespace TessitoreGM.World.Tests;

public sealed class MemoryEngineTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _blacksmithId = new("blacksmith");
    private readonly EntityId _strangerId = new("stranger");
    private readonly OrderId _orderId = new("order-1");

    [Fact]
    public void Replay_OrderParticipant_RemembersOrderEvents()
    {
        var memory = new MemoryEngine().Replay(
            _blacksmithId,
            WorldSnapshot.Empty,
            CreateOrderEvents());

        Assert.True(memory.KnowsOrder(_orderId));
        Assert.Equal(2, memory.ObservedEvents.Count);
    }

    [Fact]
    public void Replay_UninvolvedEntity_DoesNotLearnAboutOrder()
    {
        var memory = new MemoryEngine().Replay(
            _strangerId,
            WorldSnapshot.Empty,
            CreateOrderEvents());

        Assert.False(memory.KnowsOrder(_orderId));
        Assert.Empty(memory.ObservedEvents);
    }

    [Fact]
    public void Replay_EntityAtLocation_ObservesArrival()
    {
        var forgeId = new LocationId("forge");
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(_blacksmithId, forgeId, At(8, 0)),
            new EntityEnteredLocation(_customerId, forgeId, At(8, 10))
        };

        var memory = new MemoryEngine().Replay(
            _blacksmithId,
            WorldSnapshot.Empty,
            events);

        Assert.Equal(2, memory.ObservedEvents.Count);
        Assert.Contains(memory.ObservedEvents, worldEvent =>
            worldEvent is EntityEnteredLocation entered &&
            entered.EntityId == _customerId);
    }

    private IWorldEvent[] CreateOrderEvents() =>
        new IWorldEvent[]
        {
            new OrderRequested(
                _orderId,
                _customerId,
                _blacksmithId,
                "hammer",
                At(8, 11)),
            new OrderAccepted(_orderId, 14, At(8, 12))
        };

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
