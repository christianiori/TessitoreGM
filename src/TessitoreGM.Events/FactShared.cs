using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record FactShared(
    EntityId SpeakerId,
    EntityId ListenerId,
    FactId FactId,
    DateTimeOffset OccurredAt) : IWorldEvent;
