using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class ScheduledEntityArrivalRule : IWorldRule
{
    private readonly EntityId _entityId;
    private readonly LocationId _destinationId;
    private readonly DateTimeOffset _scheduledAt;

    public ScheduledEntityArrivalRule(
        EntityId entityId,
        LocationId destinationId,
        DateTimeOffset scheduledAt)
    {
        _entityId = entityId;
        _destinationId = destinationId;
        _scheduledAt = scheduledAt;
    }

    public IWorldEvent? Evaluate(
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (currentTime < _scheduledAt ||
            world.GetLocation(_entityId) == _destinationId)
        {
            return null;
        }

        return new EntityEnteredLocation(
            _entityId,
            _destinationId,
            currentTime);
    }
}
