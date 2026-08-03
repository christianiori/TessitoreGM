using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record OrderDelivered(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IWorldEvent;
