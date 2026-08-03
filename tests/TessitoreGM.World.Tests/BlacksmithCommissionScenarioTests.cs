using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class BlacksmithCommissionScenarioTests
{
    [Fact]
    public void Replay_BlacksmithCommission_ProducesExpectedWorld()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var initialWorld = WorldSnapshot.Create(new Dictionary<EntityId, int>
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
            new EntityLeftLocation(customerId, forgeId, At(8, 14))
        };

        var finalWorld = new WorldEventProcessor().Replay(initialWorld, events);

        Assert.Null(finalWorld.GetLocation(customerId));
        Assert.Equal(OrderStatus.Accepted, finalWorld.GetOrder(orderId)?.Status);
        Assert.Equal(5, finalWorld.GetBalance(customerId));
        Assert.Equal(30, finalWorld.GetBalance(blacksmithId));

        Assert.Null(initialWorld.GetLocation(customerId));
        Assert.Null(initialWorld.GetOrder(orderId));
        Assert.Equal(10, initialWorld.GetBalance(customerId));
        Assert.Equal(25, initialWorld.GetBalance(blacksmithId));
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
