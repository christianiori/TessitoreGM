using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class OrderProductionRule
{
    public IWorldEvent? Evaluate(
        WorldSnapshot world,
        OrderId orderId,
        ItemId producedItemId,
        DateTimeOffset currentTime,
        TimeSpan productionDuration)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (currentTime < world.CurrentTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentTime),
                "Simulation time cannot precede world time.");
        }

        if (productionDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productionDuration),
                "Production duration cannot be negative.");
        }

        var order = world.GetOrder(orderId);

        return order?.Status switch
        {
            OrderStatus.Accepted => new OrderWorkStarted(orderId, currentTime),
            OrderStatus.InProgress
                when order.WorkStartedAt is DateTimeOffset startedAt &&
                     currentTime >= startedAt + productionDuration
                => new OrderCompleted(orderId, producedItemId, currentTime),
            _ => null
        };
    }
}
