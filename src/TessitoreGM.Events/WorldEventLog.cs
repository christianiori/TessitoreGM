using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldEventLog(
    WorldInitialState InitialWorld,
    IReadOnlyList<IWorldEvent> Events,
    WorldSimulationDefinition? Simulation = null,
    IReadOnlyList<PlayerActionProposal>? PlayerActions = null);

public enum PlayerActionStatus
{
    Pending,
    RollRequested,
    Rolled,
    Approved,
    Rejected
}

public enum D20RollMode
{
    Normal,
    Advantage,
    Disadvantage
}

public sealed record PlayerActionProposal(
    Guid Id,
    EntityId PlayerCharacterId,
    string Description,
    DateTimeOffset SubmittedAt,
    PlayerActionStatus Status = PlayerActionStatus.Pending,
    string? Resolution = null,
    DateTimeOffset? ResolvedAt = null,
    D20Roll? Roll = null);

public sealed record D20Roll(
    int Modifier,
    int? Difficulty,
    bool DifficultyVisible,
    D20RollMode Mode,
    DateTimeOffset RequestedAt,
    IReadOnlyList<int>? Dice = null,
    int? KeptDie = null,
    int? Total = null,
    DateTimeOffset? RolledAt = null);

public sealed record WorldInitialState(
    DateTimeOffset CurrentTime,
    IReadOnlyList<EntityBalance> Balances,
    IReadOnlyList<EntityResourceStock>? ResourceStocks = null,
    WeatherCondition Weather = WeatherCondition.Clear);

public sealed record EntityBalance(
    EntityId EntityId,
    int Amount);

public sealed record EntityResourceStock(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity);
