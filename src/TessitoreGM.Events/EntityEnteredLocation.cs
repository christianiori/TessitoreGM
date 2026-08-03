using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record EntityEnteredLocation(
    EntityId EntityId,
    LocationId LocationId,
    DateTimeOffset OccurredAt) : IWorldEvent;
