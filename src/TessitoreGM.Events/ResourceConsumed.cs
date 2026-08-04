using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record ResourceConsumed(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity,
    NeedId NeedId,
    int Relief,
    DateTimeOffset OccurredAt) : IWorldEvent;
