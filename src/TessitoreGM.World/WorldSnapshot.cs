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
    private readonly IReadOnlySet<TrustChange> _trustChanges;
    private readonly IReadOnlyDictionary<ResourceStockKey, int> _resourceStocks;
    private readonly IReadOnlyDictionary<NeedStateKey, int> _needs;
    private readonly IReadOnlyDictionary<NeedStateKey, DateTimeOffset>
        _lastNeedIncreases;
    private readonly WeatherCondition _weather;
    private readonly DateTimeOffset? _lastWeatherChangedAt;

    private WorldSnapshot(
        DateTimeOffset currentTime,
        IReadOnlyDictionary<EntityId, LocationId> entityLocations,
        IReadOnlyDictionary<OrderId, Order> orders,
        IReadOnlyDictionary<EntityId, int> balances,
        IReadOnlyDictionary<ItemId, WorldItem> items,
        IReadOnlySet<SharedFact>? sharedFacts = null,
        IReadOnlySet<TrustChange>? trustChanges = null,
        IReadOnlyDictionary<ResourceStockKey, int>? resourceStocks = null,
        IReadOnlyDictionary<NeedStateKey, int>? needs = null,
        IReadOnlyDictionary<NeedStateKey, DateTimeOffset>?
            lastNeedIncreases = null,
        WeatherCondition weather = WeatherCondition.Clear,
        DateTimeOffset? lastWeatherChangedAt = null)
    {
        CurrentTime = currentTime;
        _entityLocations = entityLocations;
        _orders = orders;
        _balances = balances;
        _items = items;
        _sharedFacts = sharedFacts ?? new HashSet<SharedFact>();
        _trustChanges = trustChanges ?? new HashSet<TrustChange>();
        _resourceStocks = resourceStocks ??
            new Dictionary<ResourceStockKey, int>();
        _needs = needs ?? new Dictionary<NeedStateKey, int>();
        _lastNeedIncreases = lastNeedIncreases ??
            new Dictionary<NeedStateKey, DateTimeOffset>();
        _weather = weather;
        _lastWeatherChangedAt = lastWeatherChangedAt;
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
        => Create(
            currentTime,
            balances,
            Array.Empty<EntityResourceStock>());

    public static WorldSnapshot Create(
        DateTimeOffset currentTime,
        IReadOnlyDictionary<EntityId, int> balances,
        IReadOnlyList<EntityResourceStock> resourceStocks,
        WeatherCondition weather = WeatherCondition.Clear)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(resourceStocks);

        if (balances.Values.Any(balance => balance < 0))
        {
            throw new ArgumentException("An initial balance cannot be negative.", nameof(balances));
        }

        if (resourceStocks.Any(stock => stock.Quantity < 0))
        {
            throw new ArgumentException(
                "An initial resource quantity cannot be negative.",
                nameof(resourceStocks));
        }

        if (!Enum.IsDefined(weather))
        {
            throw new ArgumentOutOfRangeException(nameof(weather));
        }

        var stocks = resourceStocks.ToDictionary(
            stock => new ResourceStockKey(stock.EntityId, stock.ResourceId),
            stock => stock.Quantity);

        return new WorldSnapshot(
            currentTime,
            new Dictionary<EntityId, LocationId>(),
            new Dictionary<OrderId, Order>(),
            new Dictionary<EntityId, int>(balances),
            new Dictionary<ItemId, WorldItem>(),
            resourceStocks: stocks,
            weather: weather);
    }

    public DateTimeOffset CurrentTime { get; }

    public WeatherCondition Weather => _weather;

    public DateTimeOffset? LastWeatherChangedAt => _lastWeatherChangedAt;

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

    public int GetResourceQuantity(EntityId entityId, ResourceId resourceId) =>
        _resourceStocks.TryGetValue(
            new ResourceStockKey(entityId, resourceId),
            out var quantity)
            ? quantity
            : 0;

    public int GetNeedLevel(EntityId entityId, NeedId needId) =>
        _needs.TryGetValue(new NeedStateKey(entityId, needId), out var level)
            ? level
            : 0;

    public DateTimeOffset? GetLastNeedIncreaseAt(
        EntityId entityId,
        NeedId needId) =>
        _lastNeedIncreases.TryGetValue(
            new NeedStateKey(entityId, needId),
            out var occurredAt)
                ? occurredAt
                : null;

    public WorldSnapshot Apply(EntityEnteredLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations)
        {
            [worldEvent.EntityId] = worldEvent.LocationId
        };

        return new WorldSnapshot(worldEvent.OccurredAt, entityLocations, _orders, _balances, _items, _sharedFacts, _trustChanges, _resourceStocks, _needs, _lastNeedIncreases, _weather, _lastWeatherChangedAt);
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

        return new WorldSnapshot(worldEvent.OccurredAt, entityLocations, _orders, _balances, _items, _sharedFacts, _trustChanges, _resourceStocks, _needs, _lastNeedIncreases, _weather, _lastWeatherChangedAt);
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

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, _balances, _items, _sharedFacts, _trustChanges, _resourceStocks, _needs, _lastNeedIncreases, _weather, _lastWeatherChangedAt);
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

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, _balances, _items, _sharedFacts, _trustChanges, _resourceStocks, _needs, _lastNeedIncreases, _weather, _lastWeatherChangedAt);
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

        return new WorldSnapshot(worldEvent.OccurredAt, _entityLocations, orders, balances, _items, _sharedFacts, _trustChanges, _resourceStocks, _needs, _lastNeedIncreases, _weather, _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(CoinsTransferred worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "A coin transfer amount must be greater than zero.");
        }
        if (worldEvent.PayerId == worldEvent.PayeeId)
        {
            throw new InvalidOperationException(
                "A coin transfer requires two different entities.");
        }
        if (string.IsNullOrWhiteSpace(worldEvent.Reason))
        {
            throw new ArgumentException(
                "A coin transfer requires a reason.",
                nameof(worldEvent));
        }

        var payerBalance = GetBalance(worldEvent.PayerId);
        if (payerBalance < worldEvent.Amount)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.PayerId}' has insufficient funds.");
        }

        var balances = new Dictionary<EntityId, int>(_balances)
        {
            [worldEvent.PayerId] = payerBalance - worldEvent.Amount,
            [worldEvent.PayeeId] = GetBalance(worldEvent.PayeeId) + worldEvent.Amount
        };
        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
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
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
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
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
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
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
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
            sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public bool HasSharedFact(
        EntityId speakerId,
        EntityId listenerId,
        FactId factId) =>
        _sharedFacts.Contains(new SharedFact(speakerId, listenerId, factId));

    public bool HasLearnedFact(EntityId entityId, FactId factId) =>
        _sharedFacts.Any(shared =>
            shared.ListenerId == entityId && shared.FactId == factId);

    public WorldSnapshot Apply(FactRevealed worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(TrustChanged worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.SubjectId == worldEvent.OtherEntityId)
        {
            throw new InvalidOperationException(
                "An entity cannot change trust toward itself.");
        }

        if (worldEvent.Amount is 0 or < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "A trust change must be between -100 and 100 and cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(worldEvent.Reason))
        {
            throw new ArgumentException(
                "A trust change requires a reason.",
                nameof(worldEvent));
        }

        var trustChange = new TrustChange(
            worldEvent.SubjectId,
            worldEvent.OtherEntityId,
            worldEvent.Amount,
            worldEvent.Reason);
        if (_trustChanges.Any(change =>
            change.SubjectId == worldEvent.SubjectId &&
            change.OtherEntityId == worldEvent.OtherEntityId &&
            change.Reason == worldEvent.Reason))
        {
            throw new InvalidOperationException(
                $"Trust reason '{worldEvent.Reason}' was already applied.");
        }

        var trustChanges = new HashSet<TrustChange>(_trustChanges)
        {
            trustChange
        };
        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public int GetTrust(EntityId subjectId, EntityId otherEntityId) =>
        Math.Clamp(
            _trustChanges
                .Where(change =>
                    change.SubjectId == subjectId &&
                    change.OtherEntityId == otherEntityId)
                .Sum(change => change.Amount),
            -100,
            100);

    public bool HasTrustReason(
        EntityId subjectId,
        EntityId otherEntityId,
        string reason) =>
        _trustChanges.Any(change =>
            change.SubjectId == subjectId &&
            change.OtherEntityId == otherEntityId &&
            change.Reason == reason);

    public WorldSnapshot Apply(TradeCompleted worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.BuyerId == worldEvent.SellerId)
        {
            throw new InvalidOperationException(
                "An entity cannot trade with itself.");
        }

        if (worldEvent.Quantity <= 0 || worldEvent.TotalPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "Trade quantity and price must be greater than zero.");
        }

        var buyerBalance = GetBalance(worldEvent.BuyerId);
        if (buyerBalance < worldEvent.TotalPrice)
        {
            throw new InvalidOperationException(
                $"Buyer '{worldEvent.BuyerId}' has insufficient funds.");
        }

        var sellerQuantity = GetResourceQuantity(
            worldEvent.SellerId,
            worldEvent.ResourceId);
        if (sellerQuantity < worldEvent.Quantity)
        {
            throw new InvalidOperationException(
                $"Seller '{worldEvent.SellerId}' has insufficient stock.");
        }

        var balances = new Dictionary<EntityId, int>(_balances)
        {
            [worldEvent.BuyerId] = buyerBalance - worldEvent.TotalPrice,
            [worldEvent.SellerId] =
                GetBalance(worldEvent.SellerId) + worldEvent.TotalPrice
        };
        var buyerStockKey = new ResourceStockKey(
            worldEvent.BuyerId,
            worldEvent.ResourceId);
        var sellerStockKey = new ResourceStockKey(
            worldEvent.SellerId,
            worldEvent.ResourceId);
        var resourceStocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [buyerStockKey] = GetResourceQuantity(
                worldEvent.BuyerId,
                worldEvent.ResourceId) + worldEvent.Quantity,
            [sellerStockKey] = sellerQuantity - worldEvent.Quantity
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            balances,
            _items,
            _sharedFacts,
            _trustChanges,
            resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(NeedIncreased worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.Amount is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "A need increase must be between 1 and 100.");
        }

        var key = new NeedStateKey(worldEvent.EntityId, worldEvent.NeedId);
        var needs = new Dictionary<NeedStateKey, int>(_needs)
        {
            [key] = Math.Clamp(
                GetNeedLevel(worldEvent.EntityId, worldEvent.NeedId) +
                    worldEvent.Amount,
                0,
                100)
        };
        var lastNeedIncreases =
            new Dictionary<NeedStateKey, DateTimeOffset>(_lastNeedIncreases)
            {
                [key] = worldEvent.OccurredAt
            };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            needs,
            lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(ResourceConsumed worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.Quantity <= 0 || worldEvent.Relief <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "Consumed quantity and relief must be greater than zero.");
        }

        var resourceQuantity = GetResourceQuantity(
            worldEvent.EntityId,
            worldEvent.ResourceId);
        if (resourceQuantity < worldEvent.Quantity)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.EntityId}' has insufficient stock.");
        }

        var needLevel = GetNeedLevel(worldEvent.EntityId, worldEvent.NeedId);
        if (needLevel <= 0)
        {
            throw new InvalidOperationException(
                $"Need '{worldEvent.NeedId}' does not require relief.");
        }

        var stocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [new ResourceStockKey(
                worldEvent.EntityId,
                worldEvent.ResourceId)] = resourceQuantity - worldEvent.Quantity
        };
        var needs = new Dictionary<NeedStateKey, int>(_needs)
        {
            [new NeedStateKey(worldEvent.EntityId, worldEvent.NeedId)] =
                Math.Max(0, needLevel - worldEvent.Relief)
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            stocks,
            needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(ResourceProduced worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (worldEvent.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "Produced quantity must be greater than zero.");
        }

        if (GetLocation(worldEvent.ProducerId) != worldEvent.LocationId)
        {
            throw new InvalidOperationException(
                $"Producer '{worldEvent.ProducerId}' is not in production " +
                $"location '{worldEvent.LocationId}'.");
        }

        var stocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [new ResourceStockKey(
                worldEvent.ProducerId,
                worldEvent.ResourceId)] = GetResourceQuantity(
                    worldEvent.ProducerId,
                    worldEvent.ResourceId) + worldEvent.Quantity
        };

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            stocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(ResourceAcquired worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);
        EnsureResourceConsequence(worldEvent.Quantity, worldEvent.Reason);

        var key = new ResourceStockKey(
            worldEvent.EntityId,
            worldEvent.ResourceId);
        var stocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [key] = GetResourceQuantity(
                worldEvent.EntityId,
                worldEvent.ResourceId) + worldEvent.Quantity
        };
        return WithResourceStocks(worldEvent.OccurredAt, stocks);
    }

    public WorldSnapshot Apply(ResourceLost worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);
        EnsureResourceConsequence(worldEvent.Quantity, worldEvent.Reason);

        var currentQuantity = GetResourceQuantity(
            worldEvent.EntityId,
            worldEvent.ResourceId);
        if (currentQuantity < worldEvent.Quantity)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.EntityId}' has insufficient stock.");
        }
        var key = new ResourceStockKey(
            worldEvent.EntityId,
            worldEvent.ResourceId);
        var stocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [key] = currentQuantity - worldEvent.Quantity
        };
        return WithResourceStocks(worldEvent.OccurredAt, stocks);
    }

    public WorldSnapshot Apply(ResourceTransferred worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);
        EnsureResourceConsequence(worldEvent.Quantity, worldEvent.Reason);
        if (worldEvent.SourceId == worldEvent.DestinationId)
        {
            throw new InvalidOperationException(
                "A resource transfer requires two different entities.");
        }

        var sourceQuantity = GetResourceQuantity(
            worldEvent.SourceId,
            worldEvent.ResourceId);
        if (sourceQuantity < worldEvent.Quantity)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.SourceId}' has insufficient stock.");
        }
        var sourceKey = new ResourceStockKey(
            worldEvent.SourceId,
            worldEvent.ResourceId);
        var destinationKey = new ResourceStockKey(
            worldEvent.DestinationId,
            worldEvent.ResourceId);
        var stocks = new Dictionary<ResourceStockKey, int>(_resourceStocks)
        {
            [sourceKey] = sourceQuantity - worldEvent.Quantity,
            [destinationKey] = GetResourceQuantity(
                worldEvent.DestinationId,
                worldEvent.ResourceId) + worldEvent.Quantity
        };
        return WithResourceStocks(worldEvent.OccurredAt, stocks);
    }

    public WorldSnapshot Apply(WeatherChanged worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        if (!Enum.IsDefined(worldEvent.Condition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "The weather condition is not defined.");
        }

        if (worldEvent.Condition == _weather)
        {
            throw new InvalidOperationException(
                $"Weather is already '{worldEvent.Condition}'.");
        }

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            worldEvent.Condition,
            worldEvent.OccurredAt);
    }

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
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(PlayerActionRecorded worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    public WorldSnapshot Apply(PlayerCharacterRegistered worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        EnsureChronological(worldEvent);

        return new WorldSnapshot(
            worldEvent.OccurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            _resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);
    }

    private void EnsureChronological(IWorldEvent worldEvent)
    {
        if (worldEvent.OccurredAt < CurrentTime)
        {
            throw new InvalidOperationException(
                $"Event time '{worldEvent.OccurredAt:O}' precedes world time '{CurrentTime:O}'.");
        }
    }

    private static void EnsureResourceConsequence(int quantity, string reason)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "A resource quantity must be greater than zero.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A resource consequence requires a reason.",
                nameof(reason));
        }
    }

    private WorldSnapshot WithResourceStocks(
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<ResourceStockKey, int> resourceStocks) =>
        new(
            occurredAt,
            _entityLocations,
            _orders,
            _balances,
            _items,
            _sharedFacts,
            _trustChanges,
            resourceStocks,
            _needs,
            _lastNeedIncreases,
            _weather,
            _lastWeatherChangedAt);

    private readonly record struct SharedFact(
        EntityId SpeakerId,
        EntityId ListenerId,
        FactId FactId);

    private readonly record struct TrustChange(
        EntityId SubjectId,
        EntityId OtherEntityId,
        int Amount,
        string Reason);

    private readonly record struct ResourceStockKey(
        EntityId EntityId,
        ResourceId ResourceId);

    private readonly record struct NeedStateKey(
        EntityId EntityId,
        NeedId NeedId);
}
