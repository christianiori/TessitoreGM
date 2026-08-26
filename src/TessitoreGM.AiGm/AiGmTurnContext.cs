using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.AiGm;

public sealed record AiGmTurnContext(
    Guid PlayerActionId,
    EntityId PlayerCharacterId,
    string PlayerCharacterName,
    string PlayerAction,
    AiGmWorldState World,
    AiGmMemoryDossier Memory,
    AiGmAuthorizedPerspective AuthorizedPerspective,
    IReadOnlyList<string> Rules,
    AiGmCampaignCatalog? Catalog = null,
    AiGmResolvedRoll? RollResult = null,
    AiGmActionFrame? ActionFrame = null);

public enum AiGmActionKind
{
    Other,
    Social,
    Movement,
    Exploration,
    Economy,
    Conflict
}

public sealed record AiGmActionFrame(
    AiGmActionKind Kind,
    IReadOnlyList<AiGmActionTarget> Targets,
    LocationId? CurrentLocationId,
    string? CurrentLocationName,
    bool IsLikelyRisky,
    bool AllowsPlayerEconomy);

public sealed record AiGmActionTarget(
    EntityId EntityId,
    string Name,
    bool IsVisible);

public sealed record AiGmResolvedRoll(
    int Modifier,
    int Difficulty,
    bool DifficultyVisible,
    D20RollMode Mode,
    IReadOnlyList<int> Dice,
    int KeptDie,
    int Total,
    bool Succeeded,
    string Reason,
    string? PromptNarration);

/// <summary>
/// Strict narration allowlist reconstructed for the acting player character.
/// Information elsewhere in the dossier belongs to the private GM workspace.
/// </summary>
public sealed record AiGmAuthorizedPerspective(
    DateTimeOffset CurrentTime,
    WeatherCondition Weather,
    AiGmPerspectivePlayer Player,
    AiGmPerspectiveScene Scene,
    IReadOnlyList<FactId> KnownFacts,
    IReadOnlyList<AiGmRememberedEvent> ObservedEvents,
    IReadOnlyList<AiGmSceneExchange> SceneHistory);

public sealed record AiGmPerspectivePlayer(
    EntityId EntityId,
    string Name,
    int Coins,
    IReadOnlyList<AiGmResourceState> Resources);

public sealed record AiGmPerspectiveScene(
    LocationId? LocationId,
    string? LocationName,
    IReadOnlyList<AiGmVisibleCharacter> VisibleCharacters);

public sealed record AiGmVisibleCharacter(
    EntityId EntityId,
    string Name);

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
