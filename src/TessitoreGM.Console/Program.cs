using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

var customerId = new EntityId("customer");
var blacksmithId = new EntityId("blacksmith");
var forgeId = new LocationId("forge");
var orderId = new OrderId("order-1");
var swordId = new ItemId("sword-1");
var initialWorld = WorldSnapshot.Create(
    At(8, 0),
    new Dictionary<EntityId, int>
    {
        [customerId] = 10,
        [blacksmithId] = 25
    });
IWorldEvent[] events =
{
    new EntityEnteredLocation(customerId, forgeId, At(8, 10)),
    new OrderRequested(
        orderId,
        customerId,
        blacksmithId,
        "sword",
        At(8, 11)),
    new OrderAccepted(orderId, At(8, 12)),
    new PaymentTransferred(
        orderId,
        customerId,
        blacksmithId,
        Amount: 5,
        At(8, 13)),
    new EntityLeftLocation(customerId, forgeId, At(8, 14)),
    new OrderWorkStarted(orderId, At(8, 30)),
    new OrderCompleted(orderId, swordId, At(12, 30))
};

var finalWorld = new WorldEventProcessor().Replay(initialWorld, events);
var order = finalWorld.GetOrder(orderId);

Console.WriteLine($"Replayed {events.Length} world events.");
Console.WriteLine($"World time: {finalWorld.CurrentTime:HH:mm}.");
Console.WriteLine($"Customer location: {finalWorld.GetLocation(customerId)?.ToString() ?? "outside"}.");
Console.WriteLine($"Order status: {order?.Status}.");
Console.WriteLine($"Customer balance: {finalWorld.GetBalance(customerId)} coins.");
Console.WriteLine($"Blacksmith balance: {finalWorld.GetBalance(blacksmithId)} coins.");
Console.WriteLine($"Produced item: {finalWorld.GetItem(swordId)?.Name}, owned by {blacksmithId}.");

static DateTimeOffset At(int hour, int minute) =>
    new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
