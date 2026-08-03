using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldEventLog(
    WorldInitialState InitialWorld,
    IReadOnlyList<IWorldEvent> Events);

public sealed record WorldInitialState(
    DateTimeOffset CurrentTime,
    IReadOnlyList<EntityBalance> Balances);

public sealed record EntityBalance(
    EntityId EntityId,
    int Amount);
