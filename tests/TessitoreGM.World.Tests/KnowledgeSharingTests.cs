using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;

namespace TessitoreGM.World.Tests;

public sealed class KnowledgeSharingTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _blacksmithId = new("blacksmith");
    private readonly EntityId _strangerId = new("stranger");
    private readonly LocationId _forgeId = new("forge");
    private readonly OrderId _orderId = new("order-1");

    [Fact]
    public void Share_KnownFactWithColocatedListener_TransfersKnowledgeOnReplay()
    {
        var events = CreateEventsWithColocatedStranger();
        var processor = new WorldEventProcessor();
        var world = processor.Replay(WorldSnapshot.Empty, events);
        var customerMemory = new MemoryEngine().Replay(
            _customerId,
            WorldSnapshot.Empty,
            events);
        var shared = new KnowledgeSharingService().Share(
            world,
            customerMemory,
            _strangerId,
            FactId.ForOrder(_orderId),
            At(9, 0));
        var completeHistory = events.Append<IWorldEvent>(shared).ToArray();

        var strangerMemory = new MemoryEngine().Replay(
            _strangerId,
            WorldSnapshot.Empty,
            completeHistory);

        Assert.True(strangerMemory.KnowsOrder(_orderId));
        Assert.Contains(shared, strangerMemory.ObservedEvents);
    }

    [Fact]
    public void Share_UnknownFact_Throws()
    {
        var world = CreateColocatedWorld();
        var emptyMemory = NpcMemory.Empty(_customerId);

        Assert.Throws<InvalidOperationException>(() =>
            new KnowledgeSharingService().Share(
                world,
                emptyMemory,
                _strangerId,
                FactId.ForOrder(_orderId),
                At(9, 0)));
    }

    [Fact]
    public void Share_WhenEntitiesAreApart_Throws()
    {
        var factId = FactId.ForOrder(_orderId);
        var customerMemory = NpcMemory.Empty(_customerId).Remember(
            new OrderRequested(
                _orderId,
                _customerId,
                _blacksmithId,
                "hammer",
                At(8, 11)));

        Assert.Throws<InvalidOperationException>(() =>
            new KnowledgeSharingService().Share(
                WorldSnapshot.Empty,
                customerMemory,
                _strangerId,
                factId,
                At(9, 0)));
    }

    [Fact]
    public void SerializeThenDeserialize_FactShared_PreservesEvent()
    {
        var shared = new FactShared(
            _customerId,
            _strangerId,
            FactId.ForOrder(_orderId),
            At(9, 0));
        var log = new WorldEventLog(
            new WorldInitialState(
                At(8, 0),
                Array.Empty<EntityBalance>()),
            new IWorldEvent[] { shared });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));

        Assert.Equal(shared, Assert.IsType<FactShared>(Assert.Single(restored.Events)));
    }

    private IWorldEvent[] CreateEventsWithColocatedStranger() =>
        new IWorldEvent[]
        {
            new EntityEnteredLocation(_customerId, _forgeId, At(8, 10)),
            new OrderRequested(
                _orderId,
                _customerId,
                _blacksmithId,
                "hammer",
                At(8, 11)),
            new OrderAccepted(_orderId, 14, At(8, 12)),
            new EntityEnteredLocation(_strangerId, _forgeId, At(8, 30))
        };

    private WorldSnapshot CreateColocatedWorld()
    {
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(_customerId, _forgeId, At(8, 10)),
            new EntityEnteredLocation(_strangerId, _forgeId, At(8, 30))
        };

        return new WorldEventProcessor().Replay(WorldSnapshot.Empty, events);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
