using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldEventLogEditor
{
    public WorldEventLog Append(
        WorldEventLog eventLog,
        WorldSnapshot world,
        IWorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(worldEvent);

        _ = new WorldEventProcessor().Apply(world, worldEvent);
        return eventLog with
        {
            Events = eventLog.Events.Append(worldEvent).ToArray()
        };
    }
}
