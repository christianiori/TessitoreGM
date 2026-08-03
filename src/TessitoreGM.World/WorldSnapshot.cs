using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldSnapshot
{
    private readonly IReadOnlyDictionary<EntityId, LocationId> _entityLocations;

    private WorldSnapshot(IReadOnlyDictionary<EntityId, LocationId> entityLocations)
    {
        _entityLocations = entityLocations;
    }

    public static WorldSnapshot Empty { get; } =
        new(new Dictionary<EntityId, LocationId>());

    public LocationId? GetLocation(EntityId entityId) =>
        _entityLocations.TryGetValue(entityId, out var locationId)
            ? locationId
            : null;

    public WorldSnapshot Apply(EntityEnteredLocation worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        var entityLocations = new Dictionary<EntityId, LocationId>(_entityLocations)
        {
            [worldEvent.EntityId] = worldEvent.LocationId
        };

        return new WorldSnapshot(entityLocations);
    }
}
