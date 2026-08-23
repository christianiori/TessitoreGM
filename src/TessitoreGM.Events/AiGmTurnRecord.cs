using TessitoreGM.Core;

namespace TessitoreGM.Events;

public enum AiGmTurnStatus
{
    PendingConfirmation,
    Completed
}

public enum AiGmConsequenceStatus
{
    PendingConfirmation,
    Applied,
    Rejected
}

public enum AiGmConsequenceKind
{
    MoveEntity,
    TransferCoins,
    AcquireResource,
    LoseResource,
    TransferResource,
    RevealFact,
    ChangeTrust
}

public sealed record AiGmTurnRecord(
    Guid Id,
    Guid PlayerActionId,
    EntityId PlayerCharacterId,
    string Narration,
    DateTimeOffset CreatedAt,
    AiGmTurnStatus Status,
    IReadOnlyList<AiGmConsequenceRecord> Consequences,
    LocationId? SceneLocationId = null,
    IReadOnlyList<EntityId>? SceneActorIds = null);

public sealed record AiGmConsequenceRecord(
    Guid Id,
    AiGmConsequenceKind Kind,
    AiGmConsequenceStatus Status,
    bool RequiresConfirmation,
    string AssessmentReason,
    string Reason,
    EntityId? EntityId = null,
    EntityId? OtherEntityId = null,
    LocationId? LocationId = null,
    ResourceId? ResourceId = null,
    FactId? FactId = null,
    int? Amount = null,
    DateTimeOffset? ResolvedAt = null,
    string? Resolution = null);
