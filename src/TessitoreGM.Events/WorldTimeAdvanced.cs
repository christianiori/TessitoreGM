namespace TessitoreGM.Events;

public sealed record WorldTimeAdvanced(
    DateTimeOffset OccurredAt) : IWorldEvent;
