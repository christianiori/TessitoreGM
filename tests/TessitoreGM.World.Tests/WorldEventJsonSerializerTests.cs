using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldEventJsonSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_AllEventTypes_PreservesEvents()
    {
        var events = CreateEvents();
        var serializer = new WorldEventJsonSerializer();

        var json = serializer.Serialize(events);
        var restoredEvents = serializer.Deserialize(json);

        Assert.Equal(events.Length, restoredEvents.Count);

        for (var index = 0; index < events.Length; index++)
        {
            Assert.Equal(events[index].GetType(), restoredEvents[index].GetType());
            Assert.Equal(events[index], restoredEvents[index]);
        }
    }

    [Fact]
    public void DeserializeThenReplay_PersistedEvents_ReconstructsWorld()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId("hammer-1");
        var initialWorld = WorldSnapshot.Create(
            At(3, 8, 0),
            new Dictionary<EntityId, int>
            {
                [customerId] = 14,
                [blacksmithId] = 25
            });
        var serializer = new WorldEventJsonSerializer();
        var persistedEvents = serializer.Deserialize(
            serializer.Serialize(CreateEvents("hammer", 14, 4)));

        var finalWorld = new WorldEventProcessor().Replay(
            initialWorld,
            persistedEvents);

        Assert.Equal(OrderStatus.Delivered, finalWorld.GetOrder(orderId)?.Status);
        Assert.Equal(14, finalWorld.GetOrder(orderId)?.AmountPaid);
        Assert.Equal(0, finalWorld.GetBalance(customerId));
        Assert.Equal(39, finalWorld.GetBalance(blacksmithId));
        Assert.Equal("hammer", finalWorld.GetItem(itemId)?.Name);
        Assert.Equal(customerId, finalWorld.GetItem(itemId)?.OwnerId);
        Assert.Equal(At(4, 9, 3), finalWorld.CurrentTime);
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_Throws()
    {
        var serializer = new WorldEventJsonSerializer();
        var json = serializer.Serialize(CreateEvents())
            .Replace("\"version\": 1", "\"version\": 2");

        var exception = Assert.Throws<InvalidDataException>(
            () => serializer.Deserialize(json));

        Assert.Contains("version '2'", exception.Message);
    }

    private static IWorldEvent[] CreateEvents(
        string itemName = "sword",
        int totalPrice = 10,
        int deposit = 5)
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId($"{itemName}-1");

        return new IWorldEvent[]
        {
            new EntityEnteredLocation(customerId, forgeId, At(3, 8, 10)),
            new OrderRequested(
                orderId,
                customerId,
                blacksmithId,
                itemName,
                At(3, 8, 11)),
            new OrderAccepted(orderId, totalPrice, At(3, 8, 12)),
            new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                deposit,
                At(3, 8, 13)),
            new EntityLeftLocation(customerId, forgeId, At(3, 8, 14)),
            new OrderWorkStarted(orderId, At(3, 8, 30)),
            new OrderCompleted(orderId, itemId, At(3, 12, 30)),
            new EntityEnteredLocation(customerId, forgeId, At(4, 9, 0)),
            new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                totalPrice - deposit,
                At(4, 9, 1)),
            new OrderDelivered(orderId, At(4, 9, 2)),
            new EntityLeftLocation(customerId, forgeId, At(4, 9, 3))
        };
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);
}
