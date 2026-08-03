using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record OrderCompleted(
    OrderId OrderId,
    ItemId ProducedItemId,
    DateTimeOffset OccurredAt) : IWorldEvent;
