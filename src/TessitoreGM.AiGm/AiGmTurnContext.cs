using TessitoreGM.Core;

namespace TessitoreGM.AiGm;

public sealed record AiGmTurnContext(
    Guid PlayerActionId,
    EntityId PlayerCharacterId,
    string PlayerCharacterName,
    string PlayerAction,
    AiGmWorldState World,
    AiGmMemoryDossier Memory,
    IReadOnlyList<string> Rules,
    AiGmCampaignCatalog? Catalog = null);

public sealed record AiGmCampaignCatalog(
    IReadOnlyList<AiGmCatalogCharacter> Characters,
    IReadOnlyList<AiGmCatalogLocation> Locations,
    IReadOnlyList<AiGmCatalogResource> Resources,
    IReadOnlyList<AiGmCatalogNeed> Needs,
    IReadOnlyList<FactId> Facts);

public sealed record AiGmCatalogCharacter(
    EntityId EntityId,
    string Name,
    bool IsHumanPlayer,
    string Role,
    LocationId? CurrentLocationId,
    string? CurrentLocationName);

public sealed record AiGmCatalogLocation(
    LocationId LocationId,
    string Name);

public sealed record AiGmCatalogResource(
    ResourceId ResourceId,
    string Name);

public sealed record AiGmCatalogNeed(
    NeedId NeedId,
    string Name);

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
    IReadOnlyList<AiGmActorMemory> ActorMemories,
    IReadOnlyList<AiGmSceneExchange>? SceneHistory = null);

public sealed record AiGmSceneExchange(
    Guid PlayerActionId,
    DateTimeOffset OccurredAt,
    string PlayerAction,
    string Narration);

public sealed record AiGmActorMemory(
    EntityId EntityId,
    string Name,
    IReadOnlyList<FactId> KnownFacts,
    IReadOnlyList<AiGmRememberedEvent> ObservedEvents);

public sealed record AiGmRememberedEvent(
    DateTimeOffset OccurredAt,
    string EventType,
    string Summary);
