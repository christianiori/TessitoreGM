using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class OrderProductionRule : IWorldRule
{
    private readonly OrderId _orderId;
    private readonly ItemId _producedItemId;
    private readonly DateTimeOffset _workStartNotBefore;
    private readonly TimeSpan _productionDuration;

    public OrderProductionRule(
        OrderId orderId,
        ItemId producedItemId,
        DateTimeOffset workStartNotBefore,
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
        _workStartNotBefore = workStartNotBefore;
        _productionDuration = productionDuration;
    }

    public IWorldEvent? ProposeNext(
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        var order = world.GetOrder(_orderId);

        IWorldEvent? proposedEvent = order?.Status switch
        {
            OrderStatus.Accepted => new OrderWorkStarted(
                _orderId,
                LaterOf(world.CurrentTime, _workStartNotBefore)),
            OrderStatus.InProgress
                when order.WorkStartedAt is DateTimeOffset startedAt
                => new OrderCompleted(
                    _orderId,
                    _producedItemId,
                    LaterOf(
                        world.CurrentTime,
                        startedAt + _productionDuration)),
            _ => null
        };

        return proposedEvent?.OccurredAt <= until ? proposedEvent : null;
    }

    private static DateTimeOffset LaterOf(
        DateTimeOffset first,
        DateTimeOffset second) => first >= second ? first : second;
}
