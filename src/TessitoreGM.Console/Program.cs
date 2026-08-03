using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

if (!TryReadOptions(args, out var options))
{
    Environment.ExitCode = 1;
    return;
}

var customerId = new EntityId("customer");
var blacksmithId = new EntityId("blacksmith");
var forgeId = new LocationId("forge");
var orderId = new OrderId("order-1");
var itemId = new ItemId($"{options.ItemName}-1");
var initialWorld = WorldSnapshot.Create(
    At(8, 0),
    new Dictionary<EntityId, int>
    {
        [customerId] = options.CustomerBalance,
        [blacksmithId] = 25
    });
var events = BuildEvents(
    options,
    customerId,
    blacksmithId,
    forgeId,
    orderId,
    itemId);

try
{
    var finalWorld = new WorldEventProcessor().Replay(initialWorld, events);
    var order = finalWorld.GetOrder(orderId);
    var item = finalWorld.GetItem(itemId);

    Console.WriteLine(
        $"Scenario: {options.ItemName}, price {options.TotalPrice}, " +
        $"deposit {options.Deposit}, starting balance {options.CustomerBalance}.");
    Console.WriteLine($"Replayed {events.Count} world events.");
    Console.WriteLine($"World time: {finalWorld.CurrentTime:yyyy-MM-dd HH:mm}.");
    Console.WriteLine($"Customer location: {finalWorld.GetLocation(customerId)?.ToString() ?? "outside"}.");
    Console.WriteLine($"Order status: {order?.Status}.");
    Console.WriteLine($"Order paid: {order?.AmountPaid}/{order?.TotalPrice} coins.");
    Console.WriteLine($"Customer balance: {finalWorld.GetBalance(customerId)} coins.");
    Console.WriteLine($"Blacksmith balance: {finalWorld.GetBalance(blacksmithId)} coins.");
    Console.WriteLine($"Produced item: {item?.Name}, owned by {item?.OwnerId}.");
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine($"Scenario failed: {exception.Message}");
    Environment.ExitCode = 1;
}

static List<IWorldEvent> BuildEvents(
    ScenarioOptions options,
    EntityId customerId,
    EntityId blacksmithId,
    LocationId forgeId,
    OrderId orderId,
    ItemId itemId)
{
    var events = new List<IWorldEvent>
    {
        new EntityEnteredLocation(customerId, forgeId, At(8, 10)),
        new OrderRequested(
            orderId,
            customerId,
            blacksmithId,
            options.ItemName,
            At(8, 11)),
        new OrderAccepted(orderId, options.TotalPrice, At(8, 12))
    };

    if (options.Deposit > 0)
    {
        events.Add(new PaymentTransferred(
            orderId,
            customerId,
            blacksmithId,
            options.Deposit,
            At(8, 13)));
    }

    events.Add(new EntityLeftLocation(customerId, forgeId, At(8, 14)));
    events.Add(new OrderWorkStarted(orderId, At(8, 30)));
    events.Add(new OrderCompleted(orderId, itemId, At(12, 30)));
    events.Add(new EntityEnteredLocation(customerId, forgeId, AtNextDay(9, 0)));

    var balanceDue = options.TotalPrice - options.Deposit;
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

    return events;
}

static bool TryReadOptions(string[] args, out ScenarioOptions options)
{
    if (args.Length == 0)
    {
        options = new ScenarioOptions("sword", 10, 5, 10);
        return true;
    }

    if (args.Length is not (3 or 4) ||
        string.IsNullOrWhiteSpace(args[0]) ||
        !int.TryParse(args[1], out var totalPrice) ||
        !int.TryParse(args[2], out var deposit) ||
        (args.Length == 4 && !int.TryParse(args[3], out _)))
    {
        PrintUsage();
        options = default!;
        return false;
    }

    var customerBalance = args.Length == 4
        ? int.Parse(args[3])
        : totalPrice;

    if (totalPrice <= 0 ||
        deposit < 0 ||
        deposit > totalPrice ||
        customerBalance < 0)
    {
        Console.Error.WriteLine(
            "Price must be positive; deposit and balance cannot be negative; " +
            "deposit cannot exceed price.");
        options = default!;
        return false;
    }

    options = new ScenarioOptions(args[0], totalPrice, deposit, customerBalance);
    return true;
}

static void PrintUsage() =>
    Console.Error.WriteLine(
        "Usage: TessitoreGM.Console <item> <price> <deposit> [customer-balance]");

static DateTimeOffset At(int hour, int minute) =>
    new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

static DateTimeOffset AtNextDay(int hour, int minute) =>
    new(2026, 8, 4, hour, minute, 0, TimeSpan.Zero);

internal sealed record ScenarioOptions(
    string ItemName,
    int TotalPrice,
    int Deposit,
    int CustomerBalance);
