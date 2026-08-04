using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record ResourceProduced(
    EntityId ProducerId,
    ResourceId ResourceId,
    int Quantity,
    LocationId LocationId,
    DateTimeOffset OccurredAt) : IWorldEvent;
