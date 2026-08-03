namespace TessitoreGM.Narration;

public sealed record NarrationLine(
    DateTimeOffset OccurredAt,
    string Text);
