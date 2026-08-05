using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record ResourceLost(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity,
    string Reason,
    DateTimeOffset OccurredAt) : IWorldEvent;
