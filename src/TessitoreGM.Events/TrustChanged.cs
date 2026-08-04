using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record TrustChanged(
    EntityId SubjectId,
    EntityId OtherEntityId,
    int Amount,
    string Reason,
    DateTimeOffset OccurredAt) : IWorldEvent;
