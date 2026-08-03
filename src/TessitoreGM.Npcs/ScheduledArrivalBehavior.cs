using TessitoreGM.Core;
using TessitoreGM.Events;
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

    public IWorldEvent? Evaluate(
        EntityId npcId,
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (currentTime < _scheduledAt ||
            world.GetLocation(npcId) == _destinationId)
        {
            return null;
        }

        return new EntityEnteredLocation(npcId, _destinationId, currentTime);
    }
}
