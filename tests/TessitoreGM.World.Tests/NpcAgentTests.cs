using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class NpcAgentTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _blacksmithId = new("blacksmith");
    private readonly OrderId _orderId = new("order-1");
    private readonly ItemId _itemId = new("hammer-1");

    [Fact]
    public void Evaluate_AcceptedOwnOrder_ProposesStartingWork()
    {
        var world = CreateAcceptedWorld(_blacksmithId);
        var blacksmith = CreateBlacksmith();

        var proposedEvent = blacksmith.Evaluate(world, At(8, 30));

        var workStarted = Assert.IsType<OrderWorkStarted>(proposedEvent);
        Assert.Equal(_orderId, workStarted.OrderId);
        Assert.Equal(_blacksmithId, blacksmith.Id);
        Assert.Equal("blacksmith", blacksmith.Role);
    }

    [Fact]
    public void Evaluate_OrderAssignedToAnotherArtisan_DoesNotAct()
    {
        var world = CreateAcceptedWorld(new EntityId("armorer"));
        var blacksmith = CreateBlacksmith();

        var proposedEvent = blacksmith.Evaluate(world, At(8, 30));

        Assert.Null(proposedEvent);
    }

    [Fact]
    public void Advance_NpcAgent_AppliesItsDecisionToWorld()
    {
        var world = CreateAcceptedWorld(_blacksmithId);
        var simulator = new WorldSimulator(new IWorldRule[] { CreateBlacksmith() });

        var result = simulator.Advance(world, At(8, 30));

        Assert.IsType<OrderWorkStarted>(Assert.Single(result.ProducedEvents));
        Assert.Equal(
            OrderStatus.InProgress,
            result.World.GetOrder(_orderId)?.Status);
    }

    private NpcAgent CreateBlacksmith() =>
        new(
            _blacksmithId,
            "blacksmith",
            new INpcBehavior[]
            {
                new OrderProductionBehavior(
                    _orderId,
                    _itemId,
                    TimeSpan.FromHours(4))
            });

    private WorldSnapshot CreateAcceptedWorld(EntityId artisanId)
    {
        IWorldEvent[] events =
        {
            new OrderRequested(
                _orderId,
                _customerId,
                artisanId,
                "hammer",
                At(8, 11)),
            new OrderAccepted(_orderId, 14, At(8, 12))
        };

        return new WorldEventProcessor().Replay(WorldSnapshot.Empty, events);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
