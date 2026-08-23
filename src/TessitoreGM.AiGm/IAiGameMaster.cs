namespace TessitoreGM.AiGm;

/// <summary>
/// Produces a plan for one action that a human player has already declared.
/// The provider never receives a mutable world or direct access to save files.
/// </summary>
public interface IAiGameMaster
{
    Task<AiGmTurnPlan> PlanTurnAsync(
        AiGmTurnContext context,
        CancellationToken cancellationToken = default);
}
