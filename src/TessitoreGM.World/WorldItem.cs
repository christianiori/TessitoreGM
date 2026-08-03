using TessitoreGM.Core;

namespace TessitoreGM.World;

public sealed record WorldItem(
    ItemId Id,
    string Name,
    EntityId OwnerId,
    OrderId SourceOrderId,
    DateTimeOffset CreatedAt);
