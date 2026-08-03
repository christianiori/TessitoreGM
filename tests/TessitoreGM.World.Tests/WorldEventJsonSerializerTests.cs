using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldEventJsonSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_WorldAndEvents_PreservesLog()
    {
        var eventLog = CreateEventLog();
        var serializer = new WorldEventJsonSerializer();

        var json = serializer.Serialize(eventLog);
        var restoredLog = serializer.Deserialize(json);

        Assert.Equal(
            eventLog.InitialWorld.CurrentTime,
            restoredLog.InitialWorld.CurrentTime);
        Assert.Equal(
            eventLog.InitialWorld.Balances,
            restoredLog.InitialWorld.Balances);
        Assert.Equal(eventLog.Events.Count, restoredLog.Events.Count);

        for (var index = 0; index < eventLog.Events.Count; index++)
        {
            Assert.Equal(
                eventLog.Events[index].GetType(),
                restoredLog.Events[index].GetType());
            Assert.Equal(eventLog.Events[index], restoredLog.Events[index]);
        }
    }

    [Fact]
    public void DeserializeThenReplay_SelfContainedLog_ReconstructsWorld()
    {
        var serializer = new WorldEventJsonSerializer();
        var restoredLog = serializer.Deserialize(
            serializer.Serialize(CreateEventLog("hammer", 14, 4)));
        var balances = restoredLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            restoredLog.InitialWorld.CurrentTime,
            balances);

        var finalWorld = new WorldEventProcessor().Replay(
            initialWorld,
            restoredLog.Events);

        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId("hammer-1");
        Assert.Equal(OrderStatus.Delivered, finalWorld.GetOrder(orderId)?.Status);
        Assert.Equal(14, finalWorld.GetOrder(orderId)?.AmountPaid);
        Assert.Equal(0, finalWorld.GetBalance(customerId));
        Assert.Equal(39, finalWorld.GetBalance(blacksmithId));
        Assert.Equal("hammer", finalWorld.GetItem(itemId)?.Name);
        Assert.Equal(customerId, finalWorld.GetItem(itemId)?.OwnerId);
        Assert.Equal(At(4, 9, 3), finalWorld.CurrentTime);
    }

    [Fact]
    public void Deserialize_OldVersion_Throws()
    {
        var serializer = new WorldEventJsonSerializer();
        var json = serializer.Serialize(CreateEventLog())
            .Replace("\"version\": 2", "\"version\": 1");

        var exception = Assert.Throws<InvalidDataException>(
            () => serializer.Deserialize(json));

        Assert.Contains("version '1'", exception.Message);
    }

    private static WorldEventLog CreateEventLog(
        string itemName = "sword",
        int totalPrice = 10,
        int deposit = 5)
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId($"{itemName}-1");
        IWorldEvent[] events =
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

        return new WorldEventLog(
            new WorldInitialState(
                At(3, 8, 0),
                new EntityBalance[]
                {
                    new(customerId, totalPrice),
                    new(blacksmithId, 25)
                }),
            events);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);
}
