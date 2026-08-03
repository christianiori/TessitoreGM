using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldSnapshot
{
    private readonly IReadOnlyDictionary<EntityId, LocationId> _entityLocations;
    private readonly IReadOnlyDictionary<OrderId, Order> _orders;
    private readonly IReadOnlyDictionary<EntityId, int> _balances;

    private WorldSnapshot(
        IReadOnlyDictionary<EntityId, LocationId> entityLocations,
        IReadOnlyDictionary<OrderId, Order> orders,
        IReadOnlyDictionary<EntityId, int> balances)
    {
        _entityLocations = entityLocations;
        _orders = orders;
        _balances = balances;
    }

    public static WorldSnapshot Empty { get; } =
        new(
            new Dictionary<EntityId, LocationId>(),
            new Dictionary<OrderId, Order>(),
            new Dictionary<EntityId, int>());

    public static WorldSnapshot Create(IReadOnlyDictionary<EntityId, int> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        if (balances.Values.Any(balance => balance < 0))
        {
            throw new ArgumentException("An initial balance cannot be negative.", nameof(balances));
        }

        return new WorldSnapshot(
            new Dictionary<EntityId, LocationId>(),
            new Dictionary<OrderId, Order>(),
            new Dictionary<EntityId, int>(balances));
    }

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

    public WorldSnapshot Apply(EntityEnteredLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations)
        {
            [worldEvent.EntityId] = worldEvent.LocationId
        };

        return new WorldSnapshot(entityLocations, _orders, _balances);
    }

    public WorldSnapshot Apply(EntityLeftLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        if (GetLocation(worldEvent.EntityId) != worldEvent.LocationId)
        {
            throw new InvalidOperationException(
                $"Entity '{worldEvent.EntityId}' is not in location '{worldEvent.LocationId}'.");
        }

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations);
        entityLocations.Remove(worldEvent.EntityId);

        return new WorldSnapshot(entityLocations, _orders, _balances);
    }

    public WorldSnapshot Apply(OrderRequested worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

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
                AcceptedAt: null)
        };

        return new WorldSnapshot(_entityLocations, orders, _balances);
    }

    public WorldSnapshot Apply(OrderAccepted worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

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

        var orders = new Dictionary<OrderId, Order>(_orders)
        {
            [worldEvent.OrderId] = order with
            {
                Status = OrderStatus.Accepted,
                AcceptedAt = worldEvent.OccurredAt
            }
        };

        return new WorldSnapshot(_entityLocations, orders, _balances);
    }

    public WorldSnapshot Apply(PaymentTransferred worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        if (worldEvent.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldEvent),
                "A payment amount must be greater than zero.");
        }

        if (!_orders.TryGetValue(worldEvent.OrderId, out var order) ||
            order.Status != OrderStatus.Accepted)
        {
            throw new InvalidOperationException(
                $"Order '{worldEvent.OrderId}' is not accepted.");
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

        var balances = new Dictionary<EntityId, int>(_balances)
        {
            [worldEvent.PayerId] = payerBalance - worldEvent.Amount,
            [worldEvent.PayeeId] = GetBalance(worldEvent.PayeeId) + worldEvent.Amount
        };

        return new WorldSnapshot(_entityLocations, _orders, balances);
    }
}
