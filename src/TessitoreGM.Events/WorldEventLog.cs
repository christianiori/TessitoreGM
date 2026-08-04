using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldEventLog(
    WorldInitialState InitialWorld,
    IReadOnlyList<IWorldEvent> Events,
    WorldSimulationDefinition? Simulation = null);

public sealed record WorldInitialState(
    DateTimeOffset CurrentTime,
    IReadOnlyList<EntityBalance> Balances,
    IReadOnlyList<EntityResourceStock>? ResourceStocks = null);

public sealed record EntityBalance(
    EntityId EntityId,
    int Amount);

public sealed record EntityResourceStock(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity);
