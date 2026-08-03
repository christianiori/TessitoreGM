using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class ScheduledArrivalBehavior : INpcBehavior
{
    private readonly LocationId _destinationId;
    private readonly DateTimeOffset _scheduledAt;

    public ScheduledArrivalBehavior(
        LocationId destinationId,
        DateTimeOffset scheduledAt)
    {
        _destinationId = destinationId;
        _scheduledAt = scheduledAt;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetLocation(npcId) == _destinationId)
        {
            return null;
        }

        var eventTime = _scheduledAt < world.CurrentTime
            ? world.CurrentTime
            : _scheduledAt;

        if (eventTime > until)
        {
            return null;
        }

        return new EntityEnteredLocation(npcId, _destinationId, eventTime);
    }
}
