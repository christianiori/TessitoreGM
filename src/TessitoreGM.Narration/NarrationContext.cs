using TessitoreGM.Core;

namespace TessitoreGM.Narration;

public sealed class NarrationContext
{
    private readonly IReadOnlyDictionary<EntityId, string> _entityNames;
    private readonly IReadOnlyDictionary<LocationId, string> _locationNames;
    private readonly IReadOnlyDictionary<ResourceId, string> _resourceNames;

    public NarrationContext(
        IReadOnlyDictionary<EntityId, string>? entityNames = null,
        IReadOnlyDictionary<LocationId, string>? locationNames = null,
        IReadOnlyDictionary<ResourceId, string>? resourceNames = null)
    {
        _entityNames = entityNames ?? new Dictionary<EntityId, string>();
        _locationNames = locationNames ?? new Dictionary<LocationId, string>();
        _resourceNames = resourceNames ?? new Dictionary<ResourceId, string>();
    }

    public string Name(EntityId entityId) =>
        _entityNames.TryGetValue(entityId, out var name)
            ? name
            : entityId.ToString();

    public string Name(LocationId locationId) =>
        _locationNames.TryGetValue(locationId, out var name)
            ? name
            : locationId.ToString();

    public string Name(ResourceId resourceId) =>
        _resourceNames.TryGetValue(resourceId, out var name)
            ? name
            : resourceId.ToString();
}
