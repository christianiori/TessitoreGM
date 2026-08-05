using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record CoinsTransferred(
    EntityId PayerId,
    EntityId PayeeId,
    int Amount,
    string Reason,
    DateTimeOffset OccurredAt) : IWorldEvent;
