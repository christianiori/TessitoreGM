using TessitoreGM.Core;

namespace TessitoreGM.World;

public sealed record Order(
    OrderId Id,
    EntityId CustomerId,
    EntityId ArtisanId,
    string RequestedItem,
    DateTimeOffset RequestedAt,
    OrderStatus Status,
    int? TotalPrice,
    int AmountPaid,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? WorkStartedAt,
    DateTimeOffset? CompletedAt,
    ItemId? ProducedItemId,
    DateTimeOffset? DeliveredAt);
