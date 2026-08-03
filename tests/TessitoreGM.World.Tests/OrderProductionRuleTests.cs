using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class OrderProductionRuleTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _artisanId = new("blacksmith");
    private readonly OrderId _orderId = new("order-1");
    private readonly ItemId _itemId = new("hammer-1");

    [Fact]
    public void Evaluate_AcceptedOrder_ProposesWorkStarted()
    {
        var world = CreateAcceptedWorld();

        var proposedEvent = CreateRule().Evaluate(
            world,
            At(8, 30));

        var workStarted = Assert.IsType<OrderWorkStarted>(proposedEvent);
        Assert.Equal(_orderId, workStarted.OrderId);
        Assert.Equal(At(8, 30), workStarted.OccurredAt);
    }

    [Fact]
    public void Evaluate_InProgressBeforeDuration_DoesNotProposeEvent()
    {
        var world = StartWork(CreateAcceptedWorld(), At(8, 30));

        var proposedEvent = CreateRule().Evaluate(world, At(12, 29));

        Assert.Null(proposedEvent);
    }

    [Fact]
    public void Evaluate_InProgressAfterDuration_ProposesCompleted()
    {
        var world = StartWork(CreateAcceptedWorld(), At(8, 30));

        var proposedEvent = CreateRule().Evaluate(world, At(12, 30));

        var completed = Assert.IsType<OrderCompleted>(proposedEvent);
        Assert.Equal(_orderId, completed.OrderId);
        Assert.Equal(_itemId, completed.ProducedItemId);
        Assert.Equal(At(12, 30), completed.OccurredAt);
    }

    [Fact]
    public void Evaluate_CompletedOrder_DoesNotProposeAnotherEvent()
    {
        var world = StartWork(CreateAcceptedWorld(), At(8, 30));
        world = new WorldEventProcessor().Apply(
            world,
            new OrderCompleted(_orderId, _itemId, At(12, 30)));

        var proposedEvent = CreateRule().Evaluate(world, At(13, 0));

        Assert.Null(proposedEvent);
    }

    private WorldSnapshot CreateAcceptedWorld()
    {
        IWorldEvent[] events =
        {
            new OrderRequested(
                _orderId,
                _customerId,
                _artisanId,
                "hammer",
                At(8, 11)),
            new OrderAccepted(_orderId, 14, At(8, 12))
        };

        return new WorldEventProcessor().Replay(WorldSnapshot.Empty, events);
    }

    private WorldSnapshot StartWork(WorldSnapshot world, DateTimeOffset at) =>
        new WorldEventProcessor().Apply(
            world,
            new OrderWorkStarted(_orderId, at));

    private OrderProductionRule CreateRule() =>
        new(_orderId, _itemId, TimeSpan.FromHours(4));

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
