using TessitoreGM.Core;

namespace TessitoreGM.Narration;

public sealed class NarrationContext
{
    private readonly IReadOnlyDictionary<EntityId, string> _entityNames;
    private readonly IReadOnlyDictionary<LocationId, string> _locationNames;

    public NarrationContext(
        IReadOnlyDictionary<EntityId, string>? entityNames = null,
        IReadOnlyDictionary<LocationId, string>? locationNames = null)
    {
        _entityNames = entityNames ?? new Dictionary<EntityId, string>();
        _locationNames = locationNames ?? new Dictionary<LocationId, string>();
    }

    public string Name(EntityId entityId) =>
        _entityNames.TryGetValue(entityId, out var name)
            ? name
            : entityId.ToString();

    public string Name(LocationId locationId) =>
        _locationNames.TryGetValue(locationId, out var name)
            ? name
            : locationId.ToString();
}
