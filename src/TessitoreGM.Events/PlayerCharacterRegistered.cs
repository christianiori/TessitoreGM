using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record PlayerCharacterRegistered(
    EntityId EntityId,
    string Name,
    DateTimeOffset OccurredAt) : IWorldEvent;
