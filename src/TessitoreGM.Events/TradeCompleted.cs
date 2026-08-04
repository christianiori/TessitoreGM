using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record TradeCompleted(
    EntityId BuyerId,
    EntityId SellerId,
    ResourceId ResourceId,
    int Quantity,
    int TotalPrice,
    DateTimeOffset OccurredAt) : IWorldEvent;
