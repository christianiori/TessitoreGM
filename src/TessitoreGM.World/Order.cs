using TessitoreGM.Core;

namespace TessitoreGM.World;

public sealed record Order(
    OrderId Id,
    EntityId CustomerId,
    EntityId ArtisanId,
    string RequestedItem,
    DateTimeOffset RequestedAt,
    OrderStatus Status,
    DateTimeOffset? AcceptedAt);
