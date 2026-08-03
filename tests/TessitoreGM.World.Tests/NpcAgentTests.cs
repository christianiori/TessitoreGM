using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class NpcAgentTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _blacksmithId = new("blacksmith");
    private readonly OrderId _orderId = new("order-1");
    private readonly ItemId _itemId = new("hammer-1");
    private readonly LocationId _forgeId = new("forge");

    [Fact]
    public void ProposeNext_AcceptedOwnOrder_ProposesStartingWork()
    {
        var world = PlaceAtForge(CreateAcceptedWorld(_blacksmithId));
        var blacksmith = CreateBlacksmith();

        var proposedEvent = blacksmith.ProposeNext(world, At(13, 0));

        var workStarted = Assert.IsType<OrderWorkStarted>(proposedEvent);
        Assert.Equal(_orderId, workStarted.OrderId);
        Assert.Equal(_blacksmithId, blacksmith.Id);
        Assert.Equal("blacksmith", blacksmith.Role);
    }

    [Fact]
    public void ProposeNext_OrderAssignedToAnotherArtisan_DoesNotAct()
    {
        var world = PlaceAtForge(CreateAcceptedWorld(new EntityId("armorer")));
        var blacksmith = CreateBlacksmith();

        var proposedEvent = blacksmith.ProposeNext(world, At(13, 0));

        Assert.Null(proposedEvent);
    }

    [Fact]
    public void ProposeNext_UnknownOwnOrder_DoesNotAct()
    {
        var world = PlaceAtForge(CreateAcceptedWorld(_blacksmithId));
        var blacksmith = CreateBlacksmith(NpcMemory.Empty(_blacksmithId));

        var proposedEvent = blacksmith.ProposeNext(world, At(13, 0));

        Assert.Null(proposedEvent);
    }

    [Fact]
    public void Advance_NpcAgent_AppliesItsDecisionToWorld()
    {
        var world = PlaceAtForge(CreateAcceptedWorld(_blacksmithId));
        var simulator = new WorldSimulator(new IWorldRule[] { CreateBlacksmith() });

        var result = simulator.Advance(world, At(8, 30));

        Assert.IsType<OrderWorkStarted>(Assert.Single(result.ProducedEvents));
        Assert.Equal(
            OrderStatus.InProgress,
            result.World.GetOrder(_orderId)?.Status);
    }

    [Fact]
    public void Advance_MultipleBehaviors_ProducesAutonomousSequence()
    {
        var world = CreateAcceptedWorld(_blacksmithId);
        var blacksmith = CreateBlacksmithWithArrival();
        var simulator = new WorldSimulator(new IWorldRule[] { blacksmith });

        var result = simulator.Advance(world, At(13, 0));

        Assert.Collection(
            result.ProducedEvents,
            worldEvent => Assert.IsType<EntityEnteredLocation>(worldEvent),
            worldEvent => Assert.IsType<OrderWorkStarted>(worldEvent),
            worldEvent => Assert.IsType<OrderCompleted>(worldEvent),
            worldEvent => Assert.IsType<WorldTimeAdvanced>(worldEvent));
        Assert.Equal(_forgeId, result.World.GetLocation(_blacksmithId));
        Assert.Equal(OrderStatus.Completed, result.World.GetOrder(_orderId)?.Status);
        Assert.Equal(_blacksmithId, result.World.GetItem(_itemId)?.OwnerId);
        Assert.Equal(At(13, 0), result.World.CurrentTime);
    }

    private NpcAgent CreateBlacksmith(NpcMemory? memory = null) =>
        new(
            _blacksmithId,
            "blacksmith",
            memory ?? CreateOrderMemory(),
            new INpcBehavior[]
            {
                new OrderProductionBehavior(
                    _orderId,
                    _itemId,
                    _forgeId,
                    At(8, 30),
                    TimeSpan.FromHours(4))
            });

    private NpcAgent CreateBlacksmithWithArrival() =>
        new(
            _blacksmithId,
            "blacksmith",
            CreateOrderMemory(),
            new INpcBehavior[]
            {
                new ScheduledArrivalBehavior(_forgeId, At(8, 15)),
                new OrderProductionBehavior(
                    _orderId,
                    _itemId,
                    _forgeId,
                    At(8, 30),
                    TimeSpan.FromHours(4))
            });

    private NpcMemory CreateOrderMemory() =>
        NpcMemory.Empty(_blacksmithId).Remember(
            new OrderRequested(
                _orderId,
                _customerId,
                _blacksmithId,
                "hammer",
                At(8, 11)));

    private WorldSnapshot PlaceAtForge(WorldSnapshot world) =>
        new WorldEventProcessor().Apply(
            world,
            new EntityEnteredLocation(
                _blacksmithId,
                _forgeId,
                world.CurrentTime));

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
