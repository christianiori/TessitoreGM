using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class OrderProductionBehavior : INpcBehavior
{
    private readonly OrderId _orderId;
    private readonly LocationId _workplaceId;
    private readonly OrderProductionRule _productionRule;

    public OrderProductionBehavior(
        OrderId orderId,
        ItemId producedItemId,
        LocationId workplaceId,
        TimeSpan productionDuration)
    {
        _orderId = orderId;
        _workplaceId = workplaceId;
        _productionRule = new OrderProductionRule(
            orderId,
            producedItemId,
            productionDuration);
    }

    public IWorldEvent? Evaluate(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        var order = world.GetOrder(_orderId);

        if (!memory.KnowsOrder(_orderId) ||
            order?.ArtisanId != npcId ||
            world.GetLocation(npcId) != _workplaceId)
        {
            return null;
        }

        return _productionRule.Evaluate(world, currentTime);
    }
}
