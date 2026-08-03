using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record OrderRequested(
    OrderId OrderId,
    EntityId CustomerId,
    EntityId ArtisanId,
    string RequestedItem,
    DateTimeOffset OccurredAt) : IWorldEvent;
