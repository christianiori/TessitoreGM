using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record EntityLeftLocation(
    EntityId EntityId,
    LocationId LocationId,
    DateTimeOffset OccurredAt) : IWorldEvent;
