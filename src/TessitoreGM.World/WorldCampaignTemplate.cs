using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldCampaignTemplate
{
    public WorldEventLog CreateFresh(WorldEventLog source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var initialEvents = source.Events
            .OfType<EntityEnteredLocation>()
            .Where(worldEvent =>
                worldEvent.OccurredAt == source.InitialWorld.CurrentTime)
            .GroupBy(worldEvent => worldEvent.EntityId)
            .Select(group => (IWorldEvent)group.First())
            .ToArray();

        _ = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                source.InitialWorld.CurrentTime,
                source.InitialWorld.Balances.ToDictionary(
                    balance => balance.EntityId,
                    balance => balance.Amount),
                source.InitialWorld.ResourceStocks ??
                    Array.Empty<EntityResourceStock>(),
                source.InitialWorld.Weather),
            initialEvents);

        return new WorldEventLog(
            source.InitialWorld,
            initialEvents,
            source.Simulation);
    }
}
