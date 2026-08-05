using TessitoreGM.Events;

namespace TessitoreGM.Gm;

internal sealed record PendingWorldAdvance(
    string WorldFile,
    int BaseEventCount,
    DateTimeOffset BaseTime,
    DateTimeOffset TargetTime,
    IReadOnlyList<IWorldEvent> Events);
