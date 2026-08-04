using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class ConsumeResourceForNeedBehavior : INpcBehavior
{
    private readonly NeedId _needId;
    private readonly ResourceId _resourceId;
    private readonly int _minimumNeed;
    private readonly int _quantity;
    private readonly int _relief;

    public ConsumeResourceForNeedBehavior(
        NeedId needId,
        ResourceId resourceId,
        int minimumNeed,
        int quantity,
        int relief)
    {
        if (minimumNeed is <= 0 or > 100 || quantity <= 0 || relief <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumNeed));
        }

        _needId = needId;
        _resourceId = resourceId;
        _minimumNeed = minimumNeed;
        _quantity = quantity;
        _relief = relief;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        return world.CurrentTime <= until &&
            world.GetNeedLevel(npcId, _needId) >= _minimumNeed &&
            world.GetResourceQuantity(npcId, _resourceId) >= _quantity
                ? new ResourceConsumed(
                    npcId,
                    _resourceId,
                    _quantity,
                    _needId,
                    _relief,
                    world.CurrentTime)
                : null;
    }
}
