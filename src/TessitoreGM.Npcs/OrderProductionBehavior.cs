using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class OrderProductionBehavior : INpcBehavior
{
    private readonly OrderId _orderId;
    private readonly OrderProductionRule _productionRule;

    public OrderProductionBehavior(
        OrderId orderId,
        ItemId producedItemId,
        TimeSpan productionDuration)
    {
        _orderId = orderId;
        _productionRule = new OrderProductionRule(
            orderId,
            producedItemId,
            productionDuration);
    }

    public IWorldEvent? Evaluate(
        EntityId npcId,
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        var order = world.GetOrder(_orderId);

        if (order?.ArtisanId != npcId)
        {
            return null;
        }

        return _productionRule.Evaluate(world, currentTime);
    }
}
