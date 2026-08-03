using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

try
{
    if (args.Length == 0)
    {
        PrintUsage();
        Environment.ExitCode = 1;
        return;
    }

    switch (args[0].ToLowerInvariant())
    {
        case "create":
            if (!TryReadCreateOptions(args[1..], out var options))
            {
                Environment.ExitCode = 1;
                return;
            }

            CreateWorld(options);
            break;

        case "replay":
            if (args.Length > 2)
            {
                PrintUsage();
                Environment.ExitCode = 1;
                return;
            }

            ReplayWorld(args.Length == 2 ? args[1] : "world-events.json");
            break;

        default:
            PrintUsage();
            Environment.ExitCode = 1;
            break;
    }
}
catch (Exception exception) when (
    exception is InvalidOperationException or IOException or InvalidDataException)
{
    Console.Error.WriteLine($"Operation failed: {exception.Message}");
    Environment.ExitCode = 1;
}

static void CreateWorld(ScenarioOptions options)
{
    var customerId = new EntityId("customer");
    var blacksmithId = new EntityId("blacksmith");
    var forgeId = new LocationId("forge");
    var orderId = new OrderId("order-1");
    var itemId = new ItemId($"{options.ItemName}-1");
    var eventLog = new WorldEventLog(
        new WorldInitialState(
            At(8, 0),
            new EntityBalance[]
            {
                new(customerId, options.CustomerBalance),
                new(blacksmithId, 25)
            }),
        BuildEvents(
            options,
            customerId,
            blacksmithId,
            forgeId,
            orderId,
            itemId));
    var eventFilePath = Path.GetFullPath(options.EventFilePath);
    var eventDirectory = Path.GetDirectoryName(eventFilePath);

    if (!string.IsNullOrEmpty(eventDirectory))
    {
        Directory.CreateDirectory(eventDirectory);
    }

    File.WriteAllText(
        eventFilePath,
        new WorldEventJsonSerializer().Serialize(eventLog));

    Console.WriteLine($"Created persistent world: {eventFilePath}.");
    ReplayWorld(eventFilePath);
}

static void ReplayWorld(string path)
{
    var eventFilePath = Path.GetFullPath(path);
    var eventLog = new WorldEventJsonSerializer().Deserialize(
        File.ReadAllText(eventFilePath));
    var balances = eventLog.InitialWorld.Balances.ToDictionary(
        balance => balance.EntityId,
        balance => balance.Amount);
    var initialWorld = WorldSnapshot.Create(
        eventLog.InitialWorld.CurrentTime,
        balances);
    var finalWorld = new WorldEventProcessor().Replay(
        initialWorld,
        eventLog.Events);
    var request = eventLog.Events.OfType<OrderRequested>().FirstOrDefault()
        ?? throw new InvalidDataException("The event log contains no order request.");
    var completion = eventLog.Events.OfType<OrderCompleted>().FirstOrDefault()
        ?? throw new InvalidDataException("The event log contains no completed order.");
    var order = finalWorld.GetOrder(request.OrderId);
    var item = finalWorld.GetItem(completion.ProducedItemId);

    Console.WriteLine($"Replayed persistent world: {eventFilePath}.");
    Console.WriteLine($"Loaded {eventLog.Events.Count} world events.");
    Console.WriteLine($"World time: {finalWorld.CurrentTime:yyyy-MM-dd HH:mm}.");
    Console.WriteLine(
        $"Customer location: " +
        $"{finalWorld.GetLocation(request.CustomerId)?.ToString() ?? "outside"}.");
    Console.WriteLine($"Order status: {order?.Status}.");
    Console.WriteLine($"Order paid: {order?.AmountPaid}/{order?.TotalPrice} coins.");
    Console.WriteLine($"Customer balance: {finalWorld.GetBalance(request.CustomerId)} coins.");
    Console.WriteLine($"Artisan balance: {finalWorld.GetBalance(request.ArtisanId)} coins.");
    Console.WriteLine($"Produced item: {item?.Name}, owned by {item?.OwnerId}.");
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

static bool TryReadCreateOptions(string[] args, out ScenarioOptions options)
{
    if (args.Length is not (3 or 4 or 5) ||
        string.IsNullOrWhiteSpace(args[0]) ||
        !int.TryParse(args[1], out var totalPrice) ||
        !int.TryParse(args[2], out var deposit) ||
        (args.Length >= 4 && !int.TryParse(args[3], out _)) ||
        (args.Length == 5 && string.IsNullOrWhiteSpace(args[4])))
    {
        PrintUsage();
        options = default!;
        return false;
    }

    var customerBalance = args.Length >= 4
        ? int.Parse(args[3])
        : totalPrice;
    var eventFilePath = args.Length == 5
        ? args[4]
        : "world-events.json";

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

    options = new ScenarioOptions(
        args[0],
        totalPrice,
        deposit,
        customerBalance,
        eventFilePath);
    return true;
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        "Create: TessitoreGM.Console create <item> <price> <deposit> " +
        "[customer-balance] [event-file]");
    Console.Error.WriteLine(
        "Replay: TessitoreGM.Console replay [event-file]");
}

static DateTimeOffset At(int hour, int minute) =>
    new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

static DateTimeOffset AtNextDay(int hour, int minute) =>
    new(2026, 8, 4, hour, minute, 0, TimeSpan.Zero);

internal sealed record ScenarioOptions(
    string ItemName,
    int TotalPrice,
    int Deposit,
    int CustomerBalance,
    string EventFilePath);
