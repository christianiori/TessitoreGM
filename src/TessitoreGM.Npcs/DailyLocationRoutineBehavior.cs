using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class DailyLocationRoutineBehavior : INpcBehavior
{
    private readonly LocationId _destinationId;
    private readonly TimeSpan _timeOfDay;

    public DailyLocationRoutineBehavior(
        LocationId destinationId,
        TimeSpan timeOfDay)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDay),
                "A daily routine time must fall within one day.");
        }

        _destinationId = destinationId;
        _timeOfDay = timeOfDay;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetLocation(npcId) == _destinationId)
        {
            return null;
        }

        var scheduledAt = new DateTimeOffset(
            world.CurrentTime.Date.Add(_timeOfDay),
            world.CurrentTime.Offset);

        if (scheduledAt < world.CurrentTime)
        {
            scheduledAt = scheduledAt.AddDays(1);
        }

        return scheduledAt <= until
            ? new EntityEnteredLocation(npcId, _destinationId, scheduledAt)
            : null;
    }
}
