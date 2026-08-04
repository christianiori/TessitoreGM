using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class TradeWhenColocatedBehavior : INpcBehavior
{
    private readonly EntityId _sellerId;
    private readonly ResourceId _resourceId;
    private readonly int _quantity;
    private readonly int _totalPrice;

    public TradeWhenColocatedBehavior(
        EntityId sellerId,
        ResourceId resourceId,
        int quantity,
        int totalPrice)
    {
        if (quantity <= 0 || totalPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Trade quantity and price must be greater than zero.");
        }

        _sellerId = sellerId;
        _resourceId = resourceId;
        _quantity = quantity;
        _totalPrice = totalPrice;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        var buyerLocation = world.GetLocation(npcId);
        if (buyerLocation is null ||
            buyerLocation != world.GetLocation(_sellerId) ||
            world.GetResourceQuantity(npcId, _resourceId) > 0 ||
            world.GetBalance(npcId) < _totalPrice ||
            world.GetResourceQuantity(_sellerId, _resourceId) < _quantity ||
            world.CurrentTime > until)
        {
            return null;
        }

        return new TradeCompleted(
            npcId,
            _sellerId,
            _resourceId,
            _quantity,
            _totalPrice,
            world.CurrentTime);
    }
}
