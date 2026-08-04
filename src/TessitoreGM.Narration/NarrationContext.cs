using TessitoreGM.Core;

namespace TessitoreGM.Narration;

public sealed class NarrationContext
{
    private readonly IReadOnlyDictionary<EntityId, string> _entityNames;
    private readonly IReadOnlyDictionary<LocationId, string> _locationNames;
    private readonly IReadOnlyDictionary<ResourceId, string> _resourceNames;
    private readonly IReadOnlyDictionary<NeedId, string> _needNames;

    public NarrationContext(
        IReadOnlyDictionary<EntityId, string>? entityNames = null,
        IReadOnlyDictionary<LocationId, string>? locationNames = null,
        IReadOnlyDictionary<ResourceId, string>? resourceNames = null,
        IReadOnlyDictionary<NeedId, string>? needNames = null)
    {
        _entityNames = entityNames ?? new Dictionary<EntityId, string>();
        _locationNames = locationNames ?? new Dictionary<LocationId, string>();
        _resourceNames = resourceNames ?? new Dictionary<ResourceId, string>();
        _needNames = needNames ?? new Dictionary<NeedId, string>();
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

    public string Name(NeedId needId) =>
        _needNames.TryGetValue(needId, out var name)
            ? name
            : needId.ToString();
}
