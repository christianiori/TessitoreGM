using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldSnapshotTests
{
    [Fact]
    public void Apply_EntityEnteredLocation_MovesEntityToLocation()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        var initialWorld = WorldSnapshot.Empty;
        var worldEvent = new EntityEnteredLocation(
            customerId,
            forgeId,
            new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero));

        var updatedWorld = initialWorld.Apply(worldEvent);

        Assert.Null(initialWorld.GetLocation(customerId));
        Assert.Equal(forgeId, updatedWorld.GetLocation(customerId));
    }

    [Fact]
    public void Apply_EntityLeftLocation_RemovesEntityFromLocation()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        var enteredWorld = WorldSnapshot.Empty.Apply(new EntityEnteredLocation(
            customerId,
            forgeId,
            new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero)));
        var worldEvent = new EntityLeftLocation(
            customerId,
            forgeId,
            new DateTimeOffset(2026, 8, 3, 8, 10, 0, TimeSpan.Zero));

        var updatedWorld = enteredWorld.Apply(worldEvent);

        Assert.Equal(forgeId, enteredWorld.GetLocation(customerId));
        Assert.Null(updatedWorld.GetLocation(customerId));
    }

    [Fact]
    public void Apply_EntityLeftLocationFromDifferentLocation_Throws()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        var marketId = new LocationId("market");
        var world = WorldSnapshot.Empty.Apply(new EntityEnteredLocation(
            customerId,
            forgeId,
            new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero)));
        var worldEvent = new EntityLeftLocation(
            customerId,
            marketId,
            new DateTimeOffset(2026, 8, 3, 8, 10, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(() => world.Apply(worldEvent));
    }

    [Fact]
    public void Apply_OrderRequested_AddsRequestedOrderToWorld()
    {
        var orderId = new OrderId("order-1");
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var initialWorld = WorldSnapshot.Empty;
        var worldEvent = new OrderRequested(
            orderId,
            customerId,
            blacksmithId,
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero));

        var updatedWorld = initialWorld.Apply(worldEvent);

        Assert.Null(initialWorld.GetOrder(orderId));
        var order = Assert.IsType<Order>(updatedWorld.GetOrder(orderId));
        Assert.Equal(orderId, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(blacksmithId, order.ArtisanId);
        Assert.Equal("sword", order.RequestedItem);
        Assert.Equal(worldEvent.OccurredAt, order.RequestedAt);
        Assert.Equal(OrderStatus.Requested, order.Status);
        Assert.Null(order.AcceptedAt);
    }

    [Fact]
    public void Apply_DuplicateOrderRequested_Throws()
    {
        var worldEvent = new OrderRequested(
            new OrderId("order-1"),
            new EntityId("customer"),
            new EntityId("blacksmith"),
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero));
        var world = WorldSnapshot.Empty.Apply(worldEvent);

        Assert.Throws<InvalidOperationException>(() => world.Apply(worldEvent));
    }

    [Fact]
    public void Apply_OrderAccepted_ChangesOrderStatusToAccepted()
    {
        var orderId = new OrderId("order-1");
        var requestedWorld = WorldSnapshot.Empty.Apply(new OrderRequested(
            orderId,
            new EntityId("customer"),
            new EntityId("blacksmith"),
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero)));
        var worldEvent = new OrderAccepted(
            orderId,
            new DateTimeOffset(2026, 8, 3, 8, 12, 0, TimeSpan.Zero));

        var acceptedWorld = requestedWorld.Apply(worldEvent);

        Assert.Equal(OrderStatus.Requested, requestedWorld.GetOrder(orderId)?.Status);
        Assert.Equal(OrderStatus.Accepted, acceptedWorld.GetOrder(orderId)?.Status);
        Assert.Equal(worldEvent.OccurredAt, acceptedWorld.GetOrder(orderId)?.AcceptedAt);
    }

    [Fact]
    public void Apply_OrderAcceptedForMissingOrder_Throws()
    {
        var worldEvent = new OrderAccepted(
            new OrderId("missing-order"),
            new DateTimeOffset(2026, 8, 3, 8, 12, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(
            () => WorldSnapshot.Empty.Apply(worldEvent));
    }

    [Fact]
    public void Apply_OrderAcceptedTwice_Throws()
    {
        var orderId = new OrderId("order-1");
        var requestedWorld = WorldSnapshot.Empty.Apply(new OrderRequested(
            orderId,
            new EntityId("customer"),
            new EntityId("blacksmith"),
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero)));
        var worldEvent = new OrderAccepted(
            orderId,
            new DateTimeOffset(2026, 8, 3, 8, 12, 0, TimeSpan.Zero));
        var acceptedWorld = requestedWorld.Apply(worldEvent);

        Assert.Throws<InvalidOperationException>(() => acceptedWorld.Apply(worldEvent));
    }

    [Fact]
    public void Apply_PaymentTransferred_MovesCoinsBetweenOrderParties()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var initialWorld = CreateAcceptedOrderWorld(
            orderId,
            customerId,
            blacksmithId,
            customerBalance: 10,
            artisanBalance: 25);
        var worldEvent = new PaymentTransferred(
            orderId,
            customerId,
            blacksmithId,
            Amount: 5,
            new DateTimeOffset(2026, 8, 3, 8, 13, 0, TimeSpan.Zero));

        var updatedWorld = initialWorld.Apply(worldEvent);

        Assert.Equal(10, initialWorld.GetBalance(customerId));
        Assert.Equal(25, initialWorld.GetBalance(blacksmithId));
        Assert.Equal(5, updatedWorld.GetBalance(customerId));
        Assert.Equal(30, updatedWorld.GetBalance(blacksmithId));
    }

    [Fact]
    public void Apply_PaymentTransferredWithInsufficientFunds_Throws()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var world = CreateAcceptedOrderWorld(
            orderId,
            customerId,
            blacksmithId,
            customerBalance: 4,
            artisanBalance: 25);
        var worldEvent = new PaymentTransferred(
            orderId,
            customerId,
            blacksmithId,
            Amount: 5,
            new DateTimeOffset(2026, 8, 3, 8, 13, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(() => world.Apply(worldEvent));
    }

    [Fact]
    public void Apply_PaymentTransferredBeforeOrderAcceptance_Throws()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var world = WorldSnapshot.Create(new Dictionary<EntityId, int>
        {
            [customerId] = 10,
            [blacksmithId] = 25
        }).Apply(new OrderRequested(
            orderId,
            customerId,
            blacksmithId,
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero)));
        var worldEvent = new PaymentTransferred(
            orderId,
            customerId,
            blacksmithId,
            Amount: 5,
            new DateTimeOffset(2026, 8, 3, 8, 13, 0, TimeSpan.Zero));

        Assert.Throws<InvalidOperationException>(() => world.Apply(worldEvent));
    }

    private static WorldSnapshot CreateAcceptedOrderWorld(
        OrderId orderId,
        EntityId customerId,
        EntityId blacksmithId,
        int customerBalance,
        int artisanBalance)
    {
        var world = WorldSnapshot.Create(new Dictionary<EntityId, int>
        {
            [customerId] = customerBalance,
            [blacksmithId] = artisanBalance
        });
        world = world.Apply(new OrderRequested(
            orderId,
            customerId,
            blacksmithId,
            "sword",
            new DateTimeOffset(2026, 8, 3, 8, 11, 0, TimeSpan.Zero)));

        return world.Apply(new OrderAccepted(
            orderId,
            new DateTimeOffset(2026, 8, 3, 8, 12, 0, TimeSpan.Zero)));
    }
}
