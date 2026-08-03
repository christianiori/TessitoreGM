using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class OrderProductionRule : IWorldRule
{
    private readonly OrderId _orderId;
    private readonly ItemId _producedItemId;
    private readonly TimeSpan _productionDuration;

    public OrderProductionRule(
        OrderId orderId,
        ItemId producedItemId,
        TimeSpan productionDuration)
    {
        if (productionDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productionDuration),
                "Production duration cannot be negative.");
        }

        _orderId = orderId;
        _producedItemId = producedItemId;
        _productionDuration = productionDuration;
    }

    public IWorldEvent? Evaluate(
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        var order = world.GetOrder(_orderId);

        return order?.Status switch
        {
            OrderStatus.Accepted => new OrderWorkStarted(_orderId, currentTime),
            OrderStatus.InProgress
                when order.WorkStartedAt is DateTimeOffset startedAt &&
                     currentTime >= startedAt + _productionDuration
                => new OrderCompleted(_orderId, _producedItemId, currentTime),
            _ => null
        };
    }
}
