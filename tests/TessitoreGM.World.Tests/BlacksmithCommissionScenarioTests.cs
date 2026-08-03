using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class BlacksmithCommissionScenarioTests
{
    [Theory]
    [InlineData("sword", 10, 5)]
    [InlineData("dagger", 6, 2)]
    [InlineData("armor", 20, 8)]
    public void Replay_CommissionWithDifferentParameters_ProducesExpectedWorld(
        string itemName,
        int totalPrice,
        int deposit)
    {
        var scenario = BuildScenario(itemName, totalPrice, deposit, totalPrice);

        var finalWorld = new WorldEventProcessor().Replay(
            scenario.InitialWorld,
            scenario.Events);

        Assert.Null(finalWorld.GetLocation(scenario.CustomerId));
        Assert.Equal(OrderStatus.Delivered, finalWorld.GetOrder(scenario.OrderId)?.Status);
        Assert.Equal(totalPrice, finalWorld.GetOrder(scenario.OrderId)?.TotalPrice);
        Assert.Equal(totalPrice, finalWorld.GetOrder(scenario.OrderId)?.AmountPaid);
        Assert.Equal(0, finalWorld.GetBalance(scenario.CustomerId));
        Assert.Equal(25 + totalPrice, finalWorld.GetBalance(scenario.BlacksmithId));
        Assert.Equal(AtNextDay(9, 3), finalWorld.CurrentTime);
        var item = Assert.IsType<WorldItem>(finalWorld.GetItem(scenario.ItemId));
        Assert.Equal(itemName, item.Name);
        Assert.Equal(scenario.CustomerId, item.OwnerId);

        Assert.Null(scenario.InitialWorld.GetLocation(scenario.CustomerId));
        Assert.Null(scenario.InitialWorld.GetOrder(scenario.OrderId));
        Assert.Null(scenario.InitialWorld.GetItem(scenario.ItemId));
        Assert.Equal(totalPrice, scenario.InitialWorld.GetBalance(scenario.CustomerId));
        Assert.Equal(25, scenario.InitialWorld.GetBalance(scenario.BlacksmithId));
        Assert.Equal(At(8, 0), scenario.InitialWorld.CurrentTime);
    }

    [Theory]
    [InlineData("sword", 10, 5, 9)]
    [InlineData("armor", 20, 8, 19)]
    public void Replay_CommissionWithInsufficientFunds_Throws(
        string itemName,
        int totalPrice,
        int deposit,
        int customerBalance)
    {
        var scenario = BuildScenario(
            itemName,
            totalPrice,
            deposit,
            customerBalance);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new WorldEventProcessor().Replay(
                scenario.InitialWorld,
                scenario.Events));

        Assert.Contains("insufficient funds", exception.Message);
    }

    [Fact]
    public void Replay_SameEventsTwice_ProducesEquivalentWorlds()
    {
        var scenario = BuildScenario("dagger", 6, 2, 6);
        var processor = new WorldEventProcessor();

        var firstWorld = processor.Replay(scenario.InitialWorld, scenario.Events);
        var secondWorld = processor.Replay(scenario.InitialWorld, scenario.Events);

        Assert.Equal(firstWorld.CurrentTime, secondWorld.CurrentTime);
        Assert.Equal(
            firstWorld.GetOrder(scenario.OrderId),
            secondWorld.GetOrder(scenario.OrderId));
        Assert.Equal(
            firstWorld.GetItem(scenario.ItemId),
            secondWorld.GetItem(scenario.ItemId));
        Assert.Equal(
            firstWorld.GetBalance(scenario.CustomerId),
            secondWorld.GetBalance(scenario.CustomerId));
        Assert.Equal(
            firstWorld.GetBalance(scenario.BlacksmithId),
            secondWorld.GetBalance(scenario.BlacksmithId));
    }

    [Fact]
    public void Replay_EventBeforeCurrentWorldTime_Throws()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        var initialWorld = WorldSnapshot.Create(
            At(8, 10),
            new Dictionary<EntityId, int>());
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(customerId, forgeId, At(8, 9))
        };

        Assert.Throws<InvalidOperationException>(
            () => new WorldEventProcessor().Replay(initialWorld, events));
    }

    [Fact]
    public void Replay_EventsOutOfOrder_Throws()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(customerId, forgeId, At(8, 10)),
            new EntityLeftLocation(customerId, forgeId, At(8, 9))
        };

        Assert.Throws<InvalidOperationException>(
            () => new WorldEventProcessor().Replay(WorldSnapshot.Empty, events));
    }

    private static CommissionScenario BuildScenario(
        string itemName,
        int totalPrice,
        int deposit,
        int customerBalance)
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId($"{itemName}-1");
        var initialWorld = WorldSnapshot.Create(
            At(8, 0),
            new Dictionary<EntityId, int>
            {
                [customerId] = customerBalance,
                [blacksmithId] = 25
            });
        var events = new List<IWorldEvent>
        {
            new EntityEnteredLocation(customerId, forgeId, At(8, 10)),
            new OrderRequested(
                orderId,
                customerId,
                blacksmithId,
                itemName,
                At(8, 11)),
            new OrderAccepted(orderId, totalPrice, At(8, 12))
        };

        if (deposit > 0)
        {
            events.Add(new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                deposit,
                At(8, 13)));
        }

        events.Add(new EntityLeftLocation(customerId, forgeId, At(8, 14)));
        events.Add(new OrderWorkStarted(orderId, At(8, 30)));
        events.Add(new OrderCompleted(orderId, itemId, At(12, 30)));
        events.Add(new EntityEnteredLocation(customerId, forgeId, AtNextDay(9, 0)));

        var balanceDue = totalPrice - deposit;
        if (balanceDue > 0)
        {
            events.Add(new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                balanceDue,
                AtNextDay(9, 1)));
        }

        events.Add(new OrderDelivered(orderId, AtNextDay(9, 2)));
        events.Add(new EntityLeftLocation(customerId, forgeId, AtNextDay(9, 3)));

        return new CommissionScenario(
            customerId,
            blacksmithId,
            orderId,
            itemId,
            initialWorld,
            events);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset AtNextDay(int hour, int minute) =>
        new(2026, 8, 4, hour, minute, 0, TimeSpan.Zero);

    private sealed record CommissionScenario(
        EntityId CustomerId,
        EntityId BlacksmithId,
        OrderId OrderId,
        ItemId ItemId,
        WorldSnapshot InitialWorld,
        IReadOnlyList<IWorldEvent> Events);
}
