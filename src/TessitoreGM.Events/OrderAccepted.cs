using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record OrderAccepted(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IWorldEvent;
