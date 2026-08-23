using TessitoreGM.Events;

namespace TessitoreGM.AiGm;

public sealed record AiGmTurnPlan(
    Guid PlayerActionId,
    string Narration,
    AiGmRollRequest? Roll,
    IReadOnlyList<AiGmCommandProposal> Consequences);

public sealed record AiGmRollRequest(
    int Modifier,
    int? Difficulty,
    bool DifficultyVisible,
    D20RollMode Mode,
    string Reason);
