using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class DailyResourceProductionBehavior : INpcBehavior
{
    private readonly ResourceId _resourceId;
    private readonly LocationId _locationId;
    private readonly TimeSpan _timeOfDay;
    private readonly int _targetStock;

    public DailyResourceProductionBehavior(
        ResourceId resourceId,
        LocationId locationId,
        TimeSpan timeOfDay,
        int targetStock)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDay));
        }

        if (targetStock <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetStock));
        }

        _resourceId = resourceId;
        _locationId = locationId;
        _timeOfDay = timeOfDay;
        _targetStock = targetStock;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        var currentStock = world.GetResourceQuantity(npcId, _resourceId);
        if (currentStock >= _targetStock)
        {
            return null;
        }

        var productionAt = new DateTimeOffset(
            world.CurrentTime.Date.Add(_timeOfDay),
            world.CurrentTime.Offset);
        if (productionAt < world.CurrentTime)
        {
            productionAt = productionAt.AddDays(1);
        }

        if (productionAt > until || world.GetLocation(npcId) != _locationId)
        {
            return null;
        }

        return new ResourceProduced(
            npcId,
            _resourceId,
            _targetStock - currentStock,
            _locationId,
            productionAt);
    }
}
