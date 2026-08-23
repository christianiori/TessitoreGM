using TessitoreGM.Core;

namespace TessitoreGM.AiGm;

public sealed record AiGmTurnContext(
    Guid PlayerActionId,
    EntityId PlayerCharacterId,
    string PlayerCharacterName,
    string PlayerAction,
    AiGmWorldState World,
    AiGmMemoryDossier Memory,
    IReadOnlyList<string> Rules);

public sealed record AiGmWorldState(
    DateTimeOffset CurrentTime,
    WeatherCondition Weather,
    IReadOnlyList<AiGmActorState> Actors);

public sealed record AiGmActorState(
    EntityId EntityId,
    string Name,
    bool IsHumanPlayer,
    string Role,
    LocationId? LocationId,
    string? LocationName,
    int Coins,
    IReadOnlyList<AiGmResourceState> Resources);

public sealed record AiGmResourceState(
    ResourceId ResourceId,
    string Name,
    int Quantity);

public sealed record AiGmMemoryDossier(
    IReadOnlyList<AiGmRememberedEvent> CanonicalChronicle,
    IReadOnlyList<AiGmActorMemory> ActorMemories);

public sealed record AiGmActorMemory(
    EntityId EntityId,
    string Name,
    IReadOnlyList<FactId> KnownFacts,
    IReadOnlyList<AiGmRememberedEvent> ObservedEvents);

public sealed record AiGmRememberedEvent(
    DateTimeOffset OccurredAt,
    string EventType,
    string Summary);
