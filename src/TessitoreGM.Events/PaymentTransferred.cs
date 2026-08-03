using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record PaymentTransferred(
    OrderId OrderId,
    EntityId PayerId,
    EntityId PayeeId,
    int Amount,
    DateTimeOffset OccurredAt) : IWorldEvent;
