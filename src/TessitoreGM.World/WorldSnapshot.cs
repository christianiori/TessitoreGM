using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldSnapshot
{
    private readonly IReadOnlyDictionary<EntityId, LocationId> _entityLocations;
    private readonly IReadOnlyDictionary<OrderId, Order> _orders;
    private readonly IReadOnlyDictionary<EntityId, int> _balances;
    private readonly IReadOnlyDictionary<ItemId, WorldItem> _items;
    private readonly IReadOnlySet<SharedFact> _sharedFacts;

    private WorldSnapshot(
        DateTimeOffset currentTime,
        IReadOnlyDictionary<EntityId, LocationId> entityLocations,
        IReadOnlyDictionary<OrderId, Order> orders,
        IReadOnlyDictionary<EntityId, int> balances,
        IReadOnlyDictionary<ItemId, WorldItem> items,
        IReadOnlySet<SharedFact>? sharedFacts = null)
    {
        CurrentTime = currentTime;
        _entityLocations = entityLocations;
        _orders = orders;
        _balances = balances;
        _items = items;
        _sharedFacts = sharedFacts ?? new HashSet<SharedFact>();
    }

    public static WorldSnapshot Empty { get; } =
        new(
            DateTimeOffset.MinValue,
            new Dictionary<EntityId, LocationId>(),
            new Dictionary<OrderId, Order>(),
            new Dictionary<EntityId, int>(),
            new Dictionary<ItemId, WorldItem>());

    public static WorldSnapshot Create(IReadOnlyDictionary<EntityId, int> balances)
        => Create(DateTimeOffset.MinValue, balances);

    public static WorldSnapshot Create(
        DateTimeOffset currentTime,
        IReadOnlyDictionary<EntityId, int> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        if (balances.Values.Any(balance => balance < 0))
        {
            throw new ArgumentException("An initial balance cannot be negative.", nameof(balances));
        }

        return new WorldSnapshot(
            currentTime,
            new Dictionary<EntityId, LocationId>(),
            new Dictionary<OrderId, Order>(),
            new Dictionary<EntityId, int>(balances),
            new Dictionary<ItemId, WorldItem>());
    }

    public DateTimeOffset CurrentTime { get; }

    public LocationId? GetLocation(EntityId entityId) =>
        _entityLocations.TryGetValue(entityId, out var locationId)
            ? locationId
            : null;

    public Order? GetOrder(OrderId orderId) =>
        _orders.TryGetValue(orderId, out var order)
            ? order
            : null;

    public int GetBalance(EntityId entityId) =>
        _balances.TryGetValue(entityId, out var balance)
            ? balance
            : 0;

    public WorldItem? GetItem(ItemId itemId) =>
        _items.TryGetValue(itemId, out var item)
            ? item
            : null;

    public WorldSnapshot Apply(EntityEnteredLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations)
        {
            [worldEvent.EntityId] = worldEvent.LocationId
        };

        return new WorldSnapshot(worldEvent.OccurredAt, entityLocations, _orders, _balances, _items, _sharedFacts);
    }

    public WorldSnapshot Apply(EntityLeftLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (GetLocation(worldEvent.EntityId) != worldEvent.LocationId)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.EntityId}' is not in location '{worldEvent.LocationId}'.");
        }

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations);
        entityLocations.Remove(worldEvent.EntityId);

        return new WorldSnapshot(worldEvent.OccurredAt, entityLocations, _orders, _balances, _items, _sharedFacts);
    }

    public WorldSnapshot Apply(OrderRequested worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (string.IsNullOrWhiteSpace(worldEvent.RequestedItem))
        {
            throw new ArgumentException(
                "A requested item cannot be empty.",
                nameof(worldEvent));
        }

        if (_orders.ContainsKey(worldEvent.OrderId))
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' already exists.");
        }

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = new Order(
                worldEvent.OrderId,
                worldEvent.CustomerId,
                worldEvent.ArtisanId,
                worldEvent.RequestedItem,
                worldEvent.OccurredAt,
                OrderStatus.Requested,
                TotalPrice: null,
                AmountPaid: 0,
                AcceptedAt: null,
                WorkStartedAt: null,
                CompletedAt: null,
                ProducedItemId: null,
                DeliveredAt: null)
        };

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, _balances, _items, _sharedFacts);
    }

    public WorldSnapshot Apply(OrderAccepted worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order))
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' does not exist.");
        }

        if (order.Status != OrderStatus.Requested)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not awaiting acceptance.");
        }

        if (worldEvent.TotalPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "An order price must be greater than zero.");
        }

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                Status = OrderStatus.Accepted,
                TotalPrice = worldEvent.TotalPrice,
                AcceptedAt = worldEvent.OccurredAt
            }
        };

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, _balances, _items, _sharedFacts);
    }

    public WorldSnapshot Apply(PaymentTransferred worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "A payment amount must be greater than zero.");
        }

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order) ||
            order.Status is OrderStatus.Requested or OrderStatus.Delivered)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' cannot receive payments.");
        }

        if (worldEvent.PayerId != order.CustomerId ||
            worldEvent.PayeeId != order.ArtisanId)
        {
            throw new InvalidOperationException(
                "Payment parties do not match the order.");
        }

        var payerBalance = GetBalance(worldEvent.PayerId);
        if (payerBalance < worldEvent.Amount)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.PayerId}' has insufficient funds.");
        }

        if (order.TotalPrice is null ||
            order.AmountPaid + worldEvent.Amount > order.TotalPrice.Value)
        {
            throw new InvalidOperationException(
                $"Payment exceeds the amount due for order '{worldEvent.OrderId}'.");
        }

        var balances = new Dictionary<EntityId, int>(_balances)
        {
            [worldEvent.PayerId] = payerBalance - worldEvent.Amount,
            [worldEvent.PayeeId] = GetBalance(worldEvent.PayeeId) + worldEvent.Amount
        };
        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                AmountPaid = order.AmountPaid + worldEvent.Amount
            }
        };

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, balances, _items, _sharedFacts);
    }

    public WorldSnapshot Apply(OrderWorkStarted worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order) ||
            order.Status != OrderStatus.Accepted)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not accepted.");
        }

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                Status = OrderStatus.InProgress,
                WorkStartedAt = worldEvent.OccurredAt
            }
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            orders,
            _balances,
            _items,
            _sharedFacts);
    }

    public WorldSnapshot Apply(OrderCompleted worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order) ||
            order.Status != OrderStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not in progress.");
        }

        if (_items.ContainsKey(worldEvent.ProducedItemId))
        {
            throw new InvalidOperationException(
                $"Item '{worldEvent.ProducedItemId}' already exists.");
        }

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                Status = OrderStatus.Completed,
                CompletedAt = worldEvent.OccurredAt,
                ProducedItemId = worldEvent.ProducedItemId
            }
        };
        var items = new Dictionary<ItemId, WorldItem>(_items)
        {
            [worldEvent.ProducedItemId] = new WorldItem(
                worldEvent.ProducedItemId,
                order.RequestedItem,
                order.ArtisanId,
                order.Id,
                worldEvent.OccurredAt)
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            orders,
            _balances,
            items,
            _sharedFacts);
    }

    public WorldSnapshot Apply(OrderDelivered worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order) ||
            order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not completed.");
        }

        if (order.TotalPrice is null || order.AmountPaid < order.TotalPrice.Value)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not fully paid.");
        }

        if (order.ProducedItemId is not ItemId itemId ||
            !_items.TryGetValue(itemId, out var item) ||
            item.OwnerId != order.ArtisanId)
        {
            throw new InvalidOperationException(
                $"Produced item for order '{worldEvent.OrderId}' is not available for delivery.");
        }

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                Status = OrderStatus.Delivered,
                DeliveredAt = worldEvent.OccurredAt
            }
        };
        var items = new Dictionary<ItemId, WorldItem>(_items)
        {
            [itemId] = item with { OwnerId = order.CustomerId }
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            orders,
            _balances,
            items,
            _sharedFacts);
    }

    public WorldSnapshot Apply(FactShared worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.SpeakerId == worldEvent.ListenerId)
        {
            throw new InvalidOperationException(
                "An entity cannot share a fact with itself.");
        }

        var sharedFacts = new HashSet<SharedFact>(_sharedFacts)
        {
            new(worldEvent.SpeakerId, worldEvent.ListenerId, worldEvent.FactId)
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            sharedFacts);
    }

    public bool HasSharedFact(
        EntityId speakerId,
        EntityId listenerId,
        FactId factId) =>
        _sharedFacts.Contains(new SharedFact(speakerId, listenerId, factId));

    public int GetTrust(EntityId subjectId, EntityId trustedEntityId) =>
        _sharedFacts.Count(sharedFact =>
            sharedFact.ListenerId == subjectId &&
            sharedFact.SpeakerId == trustedEntityId);

    public WorldSnapshot Apply(WorldTimeAdvanced worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        if (worldEvent.OccurredAt <= CurrentTime)
        {
            throw new InvalidOperationException(
                $"New world time '{worldEvent.OccurredAt:O}' must follow " +
                $"current world time '{CurrentTime:O}'.");
        }

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts);
    }

    private void EnsureChronological(IWorldEvent worldEvent)
    {
        if (worldEvent.OccurredAt < CurrentTime)
        {
            throw new InvalidOperationException(
                $"Event time '{worldEvent.OccurredAt:O}' precedes world time '{CurrentTime:O}'.");
        }
    }

    private readonly record struct SharedFact(
        EntityId SpeakerId,
        EntityId ListenerId,
        FactId FactId);
}
