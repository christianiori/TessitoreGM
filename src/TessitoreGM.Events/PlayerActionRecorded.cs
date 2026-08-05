namespace TessitoreGM.Events;

public sealed record PlayerActionRecorded(
    string Actor,
    string Description,
    DateTimeOffset OccurredAt) : IWorldEvent;
