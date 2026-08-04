using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record FactRevealed(
    EntityId EntityId,
    FactId FactId,
    DateTimeOffset OccurredAt) : IWorldEvent;
