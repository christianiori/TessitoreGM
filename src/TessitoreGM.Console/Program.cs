using System.Globalization;
using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Narration;
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

        case "create-village":
            CreateVillage(ReadEventFileArgument(args, "village.json"));
            break;

        case "advance":
            AdvanceWorld(ReadEventFileArgument(args), until: null);
            break;

        case "advance-to":
            var advanceOptions = ReadAdvanceToOptions(args);
            AdvanceWorld(advanceOptions.EventFilePath, advanceOptions.Until);
            break;

        case "deliver":
            DeliverWorld(ReadEventFileArgument(args));
            break;

        case "replay":
            ReplayWorld(ReadEventFileArgument(args));
            break;

        case "narrate":
            NarrateWorld(ReadEventFileArgument(args));
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
            orderId),
        new WorldSimulationDefinition(
            new NpcSimulationDefinition[]
            {
                new(
                    blacksmithId,
                    "blacksmith",
                    new ScheduledArrivalDefinition[]
                    {
                        new(forgeId, At(8, 15))
                    },
                    new OrderProductionDefinition[]
                    {
                        new(
                            orderId,
                            new ItemId($"{options.ItemName}-1"),
                            forgeId,
                            At(8, 30),
                            TimeSpan.FromHours(4))
                    })
            },
            new EntityPresentationDefinition[]
            {
                new(customerId, "Cliente"),
                new(blacksmithId, "Fabbro")
            },
            new LocationPresentationDefinition[]
            {
                new(forgeId, "Forgia")
            }));
    var eventFilePath = Path.GetFullPath(options.EventFilePath);

    SaveEventLog(eventFilePath, eventLog);
    Console.WriteLine($"Created persistent world: {eventFilePath}.");
    ReplayWorld(eventFilePath);
}

static void CreateVillage(string path)
{
    var bakerId = new EntityId("elia-baker");
    var farmerId = new EntityId("brina-farmer");
    var innkeeperId = new EntityId("mira-innkeeper");
    var bakerHomeId = new LocationId("baker-home");
    var bakeryId = new LocationId("bakery");
    var farmsteadId = new LocationId("farmstead");
    var fieldsId = new LocationId("fields");
    var marketId = new LocationId("market");
    var innkeeperHomeId = new LocationId("innkeeper-home");
    var innId = new LocationId("inn");
    var initialTime = At(6, 0);
    var eventLog = new WorldEventLog(
        new WorldInitialState(initialTime, Array.Empty<EntityBalance>()),
        new IWorldEvent[]
        {
            new EntityEnteredLocation(bakerId, bakerHomeId, initialTime),
            new EntityEnteredLocation(farmerId, farmsteadId, initialTime),
            new EntityEnteredLocation(innkeeperId, innkeeperHomeId, initialTime)
        },
        new WorldSimulationDefinition(
            new NpcSimulationDefinition[]
            {
                CreateRoutineNpc(
                    bakerId,
                    "baker",
                    (bakeryId, 6, 0),
                    (bakerHomeId, 18, 0)),
                CreateRoutineNpc(
                    farmerId,
                    "farmer",
                    (fieldsId, 6, 30),
                    (farmsteadId, 19, 0)) with
                {
                    BalanceConditionalLocations =
                        new BalanceConditionalLocationDefinition[]
                        {
                            new(marketId, new TimeSpan(12, 0, 0), 5)
                        }
                },
                CreateRoutineNpc(
                    innkeeperId,
                    "innkeeper",
                    (marketId, 9, 0),
                    (innId, 10, 0),
                    (innkeeperHomeId, 23, 0))
            },
            new EntityPresentationDefinition[]
            {
                new(bakerId, "Elia il panettiere"),
                new(farmerId, "Brina la contadina"),
                new(innkeeperId, "Mira la locandiera")
            },
            new LocationPresentationDefinition[]
            {
                new(bakerHomeId, "Casa di Elia"),
                new(bakeryId, "Forno"),
                new(farmsteadId, "Cascina di Brina"),
                new(fieldsId, "Campi"),
                new(marketId, "Mercato"),
                new(innkeeperHomeId, "Casa di Mira"),
                new(innId, "Locanda")
            }));
    var eventFilePath = Path.GetFullPath(path);

    SaveEventLog(eventFilePath, eventLog);
    Console.WriteLine($"Created persistent village: {eventFilePath}.");
    ReplayWorld(eventFilePath);
}

static NpcSimulationDefinition CreateRoutineNpc(
    EntityId entityId,
    string role,
    params (LocationId DestinationId, int Hour, int Minute)[] stops) =>
    new(
        entityId,
        role,
        Array.Empty<ScheduledArrivalDefinition>(),
        Array.Empty<OrderProductionDefinition>(),
        stops.Select(stop => new DailyLocationRoutineDefinition(
            stop.DestinationId,
            new TimeSpan(stop.Hour, stop.Minute, 0))).ToArray());

static void AdvanceWorld(string path, DateTimeOffset? until)
{
    var loadedWorld = LoadWorld(path);
    var simulationStart = loadedWorld.World.CurrentTime;
    var simulationEnd = until ?? OnSameDay(simulationStart, 13, 0);

    if (simulationEnd < simulationStart)
    {
        throw new InvalidDataException(
            $"Cannot advance from '{simulationStart:O}' back to " +
            $"'{simulationEnd:O}'.");
    }

    var simulation = loadedWorld.EventLog.Simulation
        ?? throw new InvalidDataException(
            "The event log contains no simulation definition.");
    var initialWorld = CreateInitialWorld(loadedWorld.EventLog);
    var memoryEngine = new MemoryEngine();
    var rules = simulation.Npcs.Select(npc =>
    {
        var behaviors = npc.ScheduledArrivals
            .Select(arrival => (INpcBehavior)new ScheduledArrivalBehavior(
                arrival.DestinationId,
                arrival.ScheduledAt))
            .Concat((npc.DailyLocationRoutines ??
                Array.Empty<DailyLocationRoutineDefinition>())
                .Select(routine =>
                    (INpcBehavior)new DailyLocationRoutineBehavior(
                        routine.DestinationId,
                        routine.TimeOfDay)))
            .Concat((npc.BalanceConditionalLocations ??
                Array.Empty<BalanceConditionalLocationDefinition>())
                .Select(decision =>
                    (INpcBehavior)new BalanceConditionalLocationBehavior(
                        decision.DestinationId,
                        decision.TimeOfDay,
                        decision.MaximumBalance)))
            .Concat(npc.OrderProductions.Select(production =>
                (INpcBehavior)new OrderProductionBehavior(
                    production.OrderId,
                    production.ProducedItemId,
                    production.WorkLocationId,
                    production.WorkStartsNotBefore,
                    production.ProductionDuration)))
            .ToArray();

        return (IWorldRule)new NpcAgent(
            npc.EntityId,
            npc.Role,
            memoryEngine.Replay(
                npc.EntityId,
                initialWorld,
                loadedWorld.EventLog.Events),
            behaviors);
    }).ToArray();
    var simulator = new WorldSimulator(rules);
    var result = simulator.Advance(
        loadedWorld.World,
        simulationEnd);

    AppendEvents(loadedWorld, result.ProducedEvents);
    Console.WriteLine(
        $"Simulated interval: {simulationStart:O} -> {simulationEnd:O}.");
    Console.WriteLine($"Produced {result.ProducedEvents.Count} world events:");
    foreach (var worldEvent in result.ProducedEvents)
    {
        Console.WriteLine(
            $"- {worldEvent.OccurredAt:O} {worldEvent.GetType().Name}");
    }

    Console.WriteLine($"Advanced persistent world: {loadedWorld.EventFilePath}.");
    ReplayWorld(loadedWorld.EventFilePath);
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

static void NarrateWorld(string path)
{
    var loadedWorld = LoadWorld(path);
    var entityNames = GetEntityNames(loadedWorld.EventLog);
    var locationNames = GetLocationNames(loadedWorld.EventLog);
    var request = loadedWorld.EventLog.Events
        .OfType<OrderRequested>()
        .FirstOrDefault();

    if (request is not null)
    {
        entityNames.TryAdd(request.CustomerId, "Cliente");
        entityNames.TryAdd(request.ArtisanId, "Fabbro");
        locationNames.TryAdd(new LocationId("forge"), "Forgia");
    }

    var context = new NarrationContext(
        entityNames,
        locationNames);
    var lines = new DeterministicNarrator().Narrate(
        loadedWorld.EventLog.Events,
        context);

    Console.WriteLine($"Cronaca: {loadedWorld.EventFilePath}");
    foreach (var line in lines)
    {
        Console.WriteLine($"{line.OccurredAt:yyyy-MM-dd HH:mm} — {line.Text}");
    }
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
    Console.WriteLine($"Replayed persistent world: {eventFilePath}.");
    Console.WriteLine($"Loaded {eventLog.Events.Count} world events.");
    Console.WriteLine($"World time: {world.CurrentTime:yyyy-MM-dd HH:mm}.");
    var entityNames = GetEntityNames(eventLog);
    var locationNames = GetLocationNames(eventLog);

    if (eventLog.Simulation is not null)
    {
        Console.WriteLine("NPCs:");
        foreach (var npc in eventLog.Simulation.Npcs)
        {
            var location = world.GetLocation(npc.EntityId);
            var locationName = location is LocationId locationId
                ? locationNames.GetValueOrDefault(locationId, locationId.ToString())
                : "outside";
            Console.WriteLine(
                $"- {entityNames.GetValueOrDefault(npc.EntityId, npc.EntityId.ToString())} " +
                $"({npc.Role}): {locationName}.");
        }
    }

    var request = eventLog.Events.OfType<OrderRequested>().FirstOrDefault();
    if (request is null)
    {
        return;
    }

    var customerLocation = world.GetLocation(request.CustomerId);
    var customerLocationName = customerLocation is LocationId customerLocationId
        ? locationNames.GetValueOrDefault(
            customerLocationId,
            customerLocationId.ToString())
        : "outside";
    var completion = eventLog.Events.OfType<OrderCompleted>().FirstOrDefault();
    var order = world.GetOrder(request.OrderId);
    var item = completion is null
        ? null
        : world.GetItem(completion.ProducedItemId);
    Console.WriteLine(
        $"Customer location: {customerLocationName}.");
    Console.WriteLine($"Order status: {order?.Status}.");
    Console.WriteLine($"Order paid: {order?.AmountPaid}/{order?.TotalPrice} coins.");
    Console.WriteLine($"Customer balance: {world.GetBalance(request.CustomerId)} coins.");
    Console.WriteLine($"Artisan balance: {world.GetBalance(request.ArtisanId)} coins.");
    Console.WriteLine(item is null
        ? "Produced item: not created yet."
        : $"Produced item: {item.Name}, owned by {item.OwnerId}.");
}

static Dictionary<EntityId, string> GetEntityNames(WorldEventLog eventLog) =>
    eventLog.Simulation?.Entities?.ToDictionary(
        entity => entity.EntityId,
        entity => entity.Name)
    ?? new Dictionary<EntityId, string>();

static Dictionary<LocationId, string> GetLocationNames(WorldEventLog eventLog) =>
    eventLog.Simulation?.Locations?.ToDictionary(
        location => location.LocationId,
        location => location.Name)
    ?? new Dictionary<LocationId, string>();

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

static string ReadEventFileArgument(
    string[] args,
    string defaultPath = "world-events.json")
{
    if (args.Length > 2)
    {
        throw new InvalidDataException("Too many command arguments.");
    }

    return args.Length == 2 ? args[1] : defaultPath;
}

static AdvanceToOptions ReadAdvanceToOptions(string[] args)
{
    if (args.Length is not (2 or 3))
    {
        throw new InvalidDataException(
            "advance-to requires a date-time and an optional event file.");
    }

    var dateTimeText = args[1];
    var hasExplicitOffset =
        dateTimeText.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
        (dateTimeText.Length >= 6 &&
         dateTimeText[^3] == ':' &&
         dateTimeText[^6] is '+' or '-');

    if (!hasExplicitOffset ||
        !DateTimeOffset.TryParse(
            dateTimeText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var until))
    {
        throw new InvalidDataException(
            "The date-time must be ISO 8601 and include an offset, " +
            "for example 2026-08-03T13:00:00+00:00.");
    }

    return new AdvanceToOptions(
        until,
        args.Length == 3 ? args[2] : "world-events.json");
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
        "Create village: TessitoreGM.Console create-village [event-file]");
    Console.Error.WriteLine("Advance: TessitoreGM.Console advance [event-file]");
    Console.Error.WriteLine(
        "Advance to: TessitoreGM.Console advance-to " +
        "<ISO-date-time-with-offset> [event-file]");
    Console.Error.WriteLine("Deliver: TessitoreGM.Console deliver [event-file]");
    Console.Error.WriteLine("Replay: TessitoreGM.Console replay [event-file]");
    Console.Error.WriteLine("Narrate: TessitoreGM.Console narrate [event-file]");
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

internal sealed record AdvanceToOptions(
    DateTimeOffset Until,
    string EventFilePath);
