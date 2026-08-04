using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record NeedIncreased(
    EntityId EntityId,
    NeedId NeedId,
    int Amount,
    DateTimeOffset OccurredAt) : IWorldEvent;
