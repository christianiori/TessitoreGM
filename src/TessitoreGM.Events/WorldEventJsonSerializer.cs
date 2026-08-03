using System.Text.Json;

namespace TessitoreGM.Events;

public sealed class WorldEventJsonSerializer
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(IEnumerable<IWorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var persistedEvents = events.Select(worldEvent =>
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
            new EventLogDocument(CurrentVersion, persistedEvents),
            JsonOptions);
    }

    public IReadOnlyList<IWorldEvent> Deserialize(string json)
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

        if (document.Events is null)
        {
            throw new InvalidDataException("The event log does not contain an events list.");
        }

        return document.Events.Select(DeserializeEvent).ToArray();
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
        _ => throw new NotSupportedException(
            $"World event '{worldEvent.GetType().Name}' is not supported.")
    };

    private sealed record EventLogDocument(
        int Version,
        IReadOnlyList<PersistedEvent> Events);

    private sealed record PersistedEvent(
        string Type,
        JsonElement Data);
}
