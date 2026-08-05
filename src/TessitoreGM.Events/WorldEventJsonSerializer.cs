using System.Text.Json;

namespace TessitoreGM.Events;

public sealed class WorldEventJsonSerializer
{
    private const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(WorldEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(eventLog.InitialWorld);
        ArgumentNullException.ThrowIfNull(eventLog.InitialWorld.Balances);
        ArgumentNullException.ThrowIfNull(eventLog.Events);

        ValidateBalances(eventLog.InitialWorld.Balances);
        ValidateResourceStocks(eventLog.InitialWorld.ResourceStocks);
        ValidateSimulation(eventLog.Simulation);

        var persistedEvents = eventLog.Events.Select(worldEvent =>
        {
            ArgumentNullException.ThrowIfNull(worldEvent);

            return new PersistedEvent(
                GetTypeName(worldEvent),
                JsonSerializer.SerializeToElement(
                    worldEvent,
                    worldEvent.GetType(),
                    JsonOptions));
        }).ToArray();

        return JsonSerializer.Serialize(
            new EventLogDocument(
                CurrentVersion,
                eventLog.InitialWorld,
                persistedEvents,
                eventLog.Simulation),
            JsonOptions);
    }

    public WorldEventLog Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("An event log cannot be empty.", nameof(json));
        }

        EventLogDocument document;

        try
        {
            document = JsonSerializer.Deserialize<EventLogDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The event log is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The event log contains invalid JSON.", exception);
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Event log version '{document.Version}' is not supported.");
        }

        if (document.InitialWorld is null || document.InitialWorld.Balances is null)
        {
            throw new InvalidDataException("The event log has no initial world state.");
        }

        if (document.Events is null)
        {
            throw new InvalidDataException("The event log does not contain an events list.");
        }

        ValidateBalances(document.InitialWorld.Balances);
        ValidateResourceStocks(document.InitialWorld.ResourceStocks);
        ValidateSimulation(document.Simulation);

        return new WorldEventLog(
            document.InitialWorld,
            document.Events.Select(DeserializeEvent).ToArray(),
            document.Simulation);
    }

    private static void ValidateBalances(IReadOnlyList<EntityBalance> balances)
    {
        if (balances.Any(balance => balance.Amount < 0))
        {
            throw new InvalidDataException("An initial balance cannot be negative.");
        }

        if (balances.Select(balance => balance.EntityId).Distinct().Count() != balances.Count)
        {
            throw new InvalidDataException("Initial balances contain duplicate entities.");
        }
    }

    private static void ValidateResourceStocks(
        IReadOnlyList<EntityResourceStock>? resourceStocks)
    {
        if (resourceStocks is null)
        {
            return;
        }

        if (resourceStocks.Any(stock => stock.Quantity < 0))
        {
            throw new InvalidDataException(
                "An initial resource quantity cannot be negative.");
        }

        if (resourceStocks
            .Select(stock => (stock.EntityId, stock.ResourceId))
            .Distinct().Count() != resourceStocks.Count)
        {
            throw new InvalidDataException(
                "Initial resource stocks contain duplicate entries.");
        }
    }

    private static void ValidateSimulation(WorldSimulationDefinition? simulation)
    {
        if (simulation is null)
        {
            return;
        }

        if (simulation.Npcs is null ||
            simulation.Npcs.Any(npc =>
                npc is null ||
                string.IsNullOrWhiteSpace(npc.Role) ||
                npc.ScheduledArrivals is null ||
                npc.OrderProductions is null ||
                (npc.DailyLocationRoutines?.Any(routine =>
                    routine.TimeOfDay < TimeSpan.Zero ||
                    routine.TimeOfDay >= TimeSpan.FromDays(1)) ?? false) ||
                (npc.BalanceConditionalLocations?.Any(decision =>
                    decision.TimeOfDay < TimeSpan.Zero ||
                    decision.TimeOfDay >= TimeSpan.FromDays(1) ||
                    decision.MaximumBalance < 0) ?? false) ||
                (npc.KnowledgeConditionalLocations?.Any(decision =>
                    decision.TimeOfDay < TimeSpan.Zero ||
                    decision.TimeOfDay >= TimeSpan.FromDays(1)) ?? false) ||
                npc.InitialKnownFacts?.Distinct().Count() !=
                    npc.InitialKnownFacts?.Count ||
                (npc.TrustConditionalLocations?.Any(decision =>
                    decision.TimeOfDay < TimeSpan.Zero ||
                    decision.TimeOfDay >= TimeSpan.FromDays(1) ||
                    decision.MinimumTrust <= 0) ?? false) ||
                (npc.TrustChangesAfterFactSharing?.Any(change =>
                    change.Amount is 0 or < -100 or > 100 ||
                    string.IsNullOrWhiteSpace(change.Reason)) ?? false) ||
                (npc.TradesWhenColocated?.Any(trade =>
                    trade.Quantity <= 0 || trade.TotalPrice <= 0) ?? false) ||
                (npc.DailyNeedIncreases?.Any(increase =>
                    increase.TimeOfDay < TimeSpan.Zero ||
                    increase.TimeOfDay >= TimeSpan.FromDays(1) ||
                    increase.Amount is <= 0 or > 100) ?? false) ||
                (npc.ResourceConsumptions?.Any(consumption =>
                    consumption.MinimumNeed is <= 0 or > 100 ||
                    consumption.Quantity <= 0 ||
                    consumption.Relief <= 0) ?? false) ||
                (npc.DailyResourceProductions?.Any(production =>
                    production.TimeOfDay < TimeSpan.Zero ||
                    production.TimeOfDay >= TimeSpan.FromDays(1) ||
                    production.TargetStock <= 0) ?? false) ||
                npc.OrderProductions.Any(production =>
                    production.ProductionDuration <= TimeSpan.Zero)))
        {
            throw new InvalidDataException(
                "The world simulation definition is invalid.");
        }

        if (simulation.Npcs.Select(npc => npc.EntityId).Distinct().Count() !=
            simulation.Npcs.Count)
        {
            throw new InvalidDataException(
                "The world simulation contains duplicate NPC definitions.");
        }

        if (simulation.Entities?.Any(entity =>
                string.IsNullOrWhiteSpace(entity.Name)) == true ||
            simulation.Entities?.Select(entity => entity.EntityId)
                .Distinct().Count() != simulation.Entities?.Count)
        {
            throw new InvalidDataException(
                "The world simulation contains invalid entity names.");
        }

        if (simulation.Locations?.Any(location =>
                string.IsNullOrWhiteSpace(location.Name)) == true ||
            simulation.Locations?.Select(location => location.LocationId)
                .Distinct().Count() != simulation.Locations?.Count)
        {
            throw new InvalidDataException(
                "The world simulation contains invalid location names.");
        }

        if (simulation.Resources?.Any(resource =>
                string.IsNullOrWhiteSpace(resource.Name)) == true ||
            simulation.Resources?.Select(resource => resource.ResourceId)
                .Distinct().Count() != simulation.Resources?.Count)
        {
            throw new InvalidDataException(
                "The world simulation contains invalid resource names.");
        }

        if (simulation.Needs?.Any(need =>
                string.IsNullOrWhiteSpace(need.Name)) == true ||
            simulation.Needs?.Select(need => need.NeedId)
                .Distinct().Count() != simulation.Needs?.Count)
        {
            throw new InvalidDataException(
                "The world simulation contains invalid need names.");
        }
    }

    private static IWorldEvent DeserializeEvent(PersistedEvent persistedEvent)
    {
        try
        {
            return persistedEvent.Type switch
            {
                "entity-entered-location" => Deserialize<EntityEnteredLocation>(persistedEvent.Data),
                "entity-left-location" => Deserialize<EntityLeftLocation>(persistedEvent.Data),
                "order-requested" => Deserialize<OrderRequested>(persistedEvent.Data),
                "order-accepted" => Deserialize<OrderAccepted>(persistedEvent.Data),
                "payment-transferred" => Deserialize<PaymentTransferred>(persistedEvent.Data),
                "order-work-started" => Deserialize<OrderWorkStarted>(persistedEvent.Data),
                "order-completed" => Deserialize<OrderCompleted>(persistedEvent.Data),
                "order-delivered" => Deserialize<OrderDelivered>(persistedEvent.Data),
                "fact-shared" => Deserialize<FactShared>(persistedEvent.Data),
                "fact-revealed" => Deserialize<FactRevealed>(persistedEvent.Data),
                "trust-changed" => Deserialize<TrustChanged>(persistedEvent.Data),
                "trade-completed" => Deserialize<TradeCompleted>(persistedEvent.Data),
                "need-increased" => Deserialize<NeedIncreased>(persistedEvent.Data),
                "resource-consumed" => Deserialize<ResourceConsumed>(persistedEvent.Data),
                "resource-produced" => Deserialize<ResourceProduced>(persistedEvent.Data),
                "player-action-recorded" => Deserialize<PlayerActionRecorded>(persistedEvent.Data),
                "world-time-advanced" => Deserialize<WorldTimeAdvanced>(persistedEvent.Data),
                _ => throw new InvalidDataException(
                    $"World event type '{persistedEvent.Type}' is not supported.")
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"World event '{persistedEvent.Type}' contains invalid data.",
                exception);
        }
    }

    private static TEvent Deserialize<TEvent>(JsonElement data)
        where TEvent : IWorldEvent =>
        data.Deserialize<TEvent>(JsonOptions)
        ?? throw new InvalidDataException(
            $"World event '{typeof(TEvent).Name}' contains no data.");

    private static string GetTypeName(IWorldEvent worldEvent) => worldEvent switch
    {
        EntityEnteredLocation => "entity-entered-location",
        EntityLeftLocation => "entity-left-location",
        OrderRequested => "order-requested",
        OrderAccepted => "order-accepted",
        PaymentTransferred => "payment-transferred",
        OrderWorkStarted => "order-work-started",
        OrderCompleted => "order-completed",
        OrderDelivered => "order-delivered",
        FactShared => "fact-shared",
        FactRevealed => "fact-revealed",
        TrustChanged => "trust-changed",
        TradeCompleted => "trade-completed",
        NeedIncreased => "need-increased",
        ResourceConsumed => "resource-consumed",
        ResourceProduced => "resource-produced",
        PlayerActionRecorded => "player-action-recorded",
        WorldTimeAdvanced => "world-time-advanced",
        _ => throw new NotSupportedException(
            $"World event '{worldEvent.GetType().Name}' is not supported.")
    };

    private sealed record EventLogDocument(
        int Version,
        WorldInitialState InitialWorld,
        IReadOnlyList<PersistedEvent> Events,
        WorldSimulationDefinition? Simulation = null);

    private sealed record PersistedEvent(
        string Type,
        JsonElement Data);
}
