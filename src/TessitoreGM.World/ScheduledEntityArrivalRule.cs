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

    public IWorldEvent? ProposeNext(
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetLocation(_entityId) == _destinationId)
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

        return new EntityEnteredLocation(
            _entityId,
            _destinationId,
            eventTime);
    }
}
