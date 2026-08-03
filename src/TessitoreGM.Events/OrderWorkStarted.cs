using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record OrderWorkStarted(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IWorldEvent;
