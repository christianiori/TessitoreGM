using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;
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

        case "advance":
            AdvanceWorld(ReadEventFileArgument(args));
            break;

        case "deliver":
            DeliverWorld(ReadEventFileArgument(args));
            break;

        case "replay":
            ReplayWorld(ReadEventFileArgument(args));
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
    var eventLog = new WorldEventLog(
        new WorldInitialState(
            At(8, 0),
            new EntityBalance[]
            {
                new(customerId, options.CustomerBalance),
                new(blacksmithId, 25)
            }),
        BuildCreateEvents(
            options,
            customerId,
            blacksmithId,
            forgeId,
            orderId));
    var eventFilePath = Path.GetFullPath(options.EventFilePath);

    SaveEventLog(eventFilePath, eventLog);
    Console.WriteLine($"Created persistent world: {eventFilePath}.");
    ReplayWorld(eventFilePath);
}

static void AdvanceWorld(string path)
{
    var loadedWorld = LoadWorld(path);
    var request = GetOrderRequest(loadedWorld.EventLog);
    var itemId = new ItemId($"{request.RequestedItem}-1");
    var forgeId = new LocationId("forge");
    var blacksmithMemory = new MemoryEngine().Replay(
        request.ArtisanId,
        CreateInitialWorld(loadedWorld.EventLog),
        loadedWorld.EventLog.Events);
    var simulator = new WorldSimulator(new IWorldRule[]
    {
        new NpcAgent(
            request.ArtisanId,
            "blacksmith",
            blacksmithMemory,
            new INpcBehavior[]
            {
                new ScheduledArrivalBehavior(
                    forgeId,
                    OnSameDay(loadedWorld.World.CurrentTime, 8, 15)),
                new OrderProductionBehavior(
                    request.OrderId,
                    itemId,
                    forgeId,
                    TimeSpan.FromHours(4))
            })
    });
    var newEvents = new List<IWorldEvent>();
    var world = loadedWorld.World;

    SimulateAt(OnSameDay(world.CurrentTime, 8, 15));
    SimulateAt(OnSameDay(world.CurrentTime, 8, 30));
    SimulateAt(OnSameDay(world.CurrentTime, 12, 30));

    AppendEvents(loadedWorld, newEvents);
    Console.WriteLine($"Advanced persistent world: {loadedWorld.EventFilePath}.");
    ReplayWorld(loadedWorld.EventFilePath);

    void SimulateAt(DateTimeOffset currentTime)
    {
        var result = simulator.Advance(world, currentTime);
        world = result.World;
        newEvents.AddRange(result.ProducedEvents);
    }
}

static void DeliverWorld(string path)
{
    var loadedWorld = LoadWorld(path);
    var request = GetOrderRequest(loadedWorld.EventLog);
    var order = loadedWorld.World.GetOrder(request.OrderId)
        ?? throw new InvalidDataException($"Order '{request.OrderId}' does not exist.");
    var forgeId = new LocationId("forge");
    var deliveryDay = loadedWorld.World.CurrentTime.AddDays(1);
    var newEvents = new List<IWorldEvent>
    {
        new EntityEnteredLocation(
            request.CustomerId,
            forgeId,
            OnSameDay(deliveryDay, 9, 0))
    };
    var balanceDue = (order.TotalPrice ?? 0) - order.AmountPaid;

    if (balanceDue > 0)
    {
        newEvents.Add(new PaymentTransferred(
            request.OrderId,
            request.CustomerId,
            request.ArtisanId,
            balanceDue,
            OnSameDay(deliveryDay, 9, 1)));
    }

    newEvents.Add(new OrderDelivered(
        request.OrderId,
        OnSameDay(deliveryDay, 9, 2)));
    newEvents.Add(new EntityLeftLocation(
        request.CustomerId,
        forgeId,
        OnSameDay(deliveryDay, 9, 3)));

    AppendEvents(loadedWorld, newEvents);
    Console.WriteLine($"Delivered order in persistent world: {loadedWorld.EventFilePath}.");
    ReplayWorld(loadedWorld.EventFilePath);
}

static void AppendEvents(LoadedWorld loadedWorld, IReadOnlyList<IWorldEvent> newEvents)
{
    new WorldEventProcessor().Replay(loadedWorld.World, newEvents);

    var updatedEventLog = loadedWorld.EventLog with
    {
        Events = loadedWorld.EventLog.Events.Concat(newEvents).ToArray()
    };

    SaveEventLog(loadedWorld.EventFilePath, updatedEventLog);
}

static void ReplayWorld(string path)
{
    var loadedWorld = LoadWorld(path);
    DisplayWorld(loadedWorld.EventFilePath, loadedWorld.EventLog, loadedWorld.World);
}

static LoadedWorld LoadWorld(string path)
{
    var eventFilePath = Path.GetFullPath(path);
    var eventLog = new WorldEventJsonSerializer().Deserialize(
        File.ReadAllText(eventFilePath));
    var initialWorld = CreateInitialWorld(eventLog);
    var world = new WorldEventProcessor().Replay(initialWorld, eventLog.Events);

    return new LoadedWorld(eventFilePath, eventLog, world);
}

static WorldSnapshot CreateInitialWorld(WorldEventLog eventLog)
{
    var balances = eventLog.InitialWorld.Balances.ToDictionary(
        balance => balance.EntityId,
        balance => balance.Amount);
    return WorldSnapshot.Create(
        eventLog.InitialWorld.CurrentTime,
        balances);
}

static void SaveEventLog(string path, WorldEventLog eventLog)
{
    var eventDirectory = Path.GetDirectoryName(path);

    if (!string.IsNullOrEmpty(eventDirectory))
    {
        Directory.CreateDirectory(eventDirectory);
    }

    File.WriteAllText(path, new WorldEventJsonSerializer().Serialize(eventLog));
}

static void DisplayWorld(
    string eventFilePath,
    WorldEventLog eventLog,
    WorldSnapshot world)
{
    var request = GetOrderRequest(eventLog);
    var completion = eventLog.Events.OfType<OrderCompleted>().FirstOrDefault();
    var order = world.GetOrder(request.OrderId);
    var item = completion is null
        ? null
        : world.GetItem(completion.ProducedItemId);

    Console.WriteLine($"Replayed persistent world: {eventFilePath}.");
    Console.WriteLine($"Loaded {eventLog.Events.Count} world events.");
    Console.WriteLine($"World time: {world.CurrentTime:yyyy-MM-dd HH:mm}.");
    Console.WriteLine(
        $"Customer location: " +
        $"{world.GetLocation(request.CustomerId)?.ToString() ?? "outside"}.");
    Console.WriteLine($"Order status: {order?.Status}.");
    Console.WriteLine($"Order paid: {order?.AmountPaid}/{order?.TotalPrice} coins.");
    Console.WriteLine($"Customer balance: {world.GetBalance(request.CustomerId)} coins.");
    Console.WriteLine($"Artisan balance: {world.GetBalance(request.ArtisanId)} coins.");
    Console.WriteLine(item is null
        ? "Produced item: not created yet."
        : $"Produced item: {item.Name}, owned by {item.OwnerId}.");
}

static OrderRequested GetOrderRequest(WorldEventLog eventLog) =>
    eventLog.Events.OfType<OrderRequested>().FirstOrDefault()
    ?? throw new InvalidDataException("The event log contains no order request.");

static List<IWorldEvent> BuildCreateEvents(
    ScenarioOptions options,
    EntityId customerId,
    EntityId blacksmithId,
    LocationId forgeId,
    OrderId orderId)
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
    return events;
}

static string ReadEventFileArgument(string[] args)
{
    if (args.Length > 2)
    {
        throw new InvalidDataException("Too many command arguments.");
    }

    return args.Length == 2 ? args[1] : "world-events.json";
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
    Console.Error.WriteLine("Advance: TessitoreGM.Console advance [event-file]");
    Console.Error.WriteLine("Deliver: TessitoreGM.Console deliver [event-file]");
    Console.Error.WriteLine("Replay: TessitoreGM.Console replay [event-file]");
}

static DateTimeOffset At(int hour, int minute) =>
    new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

static DateTimeOffset OnSameDay(
    DateTimeOffset date,
    int hour,
    int minute) =>
    new(date.Year, date.Month, date.Day, hour, minute, 0, date.Offset);

internal sealed record ScenarioOptions(
    string ItemName,
    int TotalPrice,
    int Deposit,
    int CustomerBalance,
    string EventFilePath);

internal sealed record LoadedWorld(
    string EventFilePath,
    WorldEventLog EventLog,
    WorldSnapshot World);
