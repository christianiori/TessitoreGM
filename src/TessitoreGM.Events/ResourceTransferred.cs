using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record ResourceTransferred(
    EntityId SourceId,
    EntityId DestinationId,
    ResourceId ResourceId,
    int Quantity,
    string Reason,
    DateTimeOffset OccurredAt) : IWorldEvent;
