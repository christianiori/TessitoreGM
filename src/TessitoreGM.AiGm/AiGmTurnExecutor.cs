using TessitoreGM.Events;

namespace TessitoreGM.AiGm;

public enum AiGmTurnExecutionStatus
{
    Completed,
    PendingConfirmation,
    ProviderUnavailable,
    InvalidPlan
}

public sealed record AiGmTurnExecutionResult(
    AiGmTurnExecutionStatus Status,
    WorldEventLog EventLog,
    string Message,
    bool WorldChanged);

/// <summary>
/// Expected provider failure. Adapters translate network, authentication and
/// quota failures into this exception so the campaign can safely fall back to
/// its human GM queue.
/// </summary>
public sealed class AiGmProviderUnavailableException : Exception
{
    public AiGmProviderUnavailableException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Executes one provider turn behind the deterministic validation boundary.
/// Provider failures and invalid plans never mutate the supplied event log.
/// </summary>
public sealed class AiGmTurnExecutor
{
    private readonly IAiGameMaster _gameMaster;
    private readonly AiGmContextBuilder _contextBuilder;
    private readonly AiGmTurnCoordinator _coordinator;

    public AiGmTurnExecutor(
        IAiGameMaster gameMaster,
        AiGmContextBuilder? contextBuilder = null,
        AiGmTurnCoordinator? coordinator = null)
    {
        _gameMaster = gameMaster ??
            throw new ArgumentNullException(nameof(gameMaster));
        _contextBuilder = contextBuilder ?? new AiGmContextBuilder();
        _coordinator = coordinator ?? new AiGmTurnCoordinator();
    }

    public async Task<AiGmTurnExecutionResult> ExecuteAsync(
        WorldEventLog eventLog,
        PlayerActionProposal playerAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(playerAction);

        var context = _contextBuilder.Build(eventLog, playerAction);
        AiGmTurnPlan plan;
        try
        {
            plan = await _gameMaster.PlanTurnAsync(
                context,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Unchanged(
                AiGmTurnExecutionStatus.ProviderUnavailable,
                eventLog,
                "Il Game Master AI non ha risposto in tempo. L'azione resta al GM umano.");
        }
        catch (AiGmProviderUnavailableException exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "Il Game Master AI non è disponibile. L'azione resta al GM umano."
                : exception.Message;
            return Unchanged(
                AiGmTurnExecutionStatus.ProviderUnavailable,
                eventLog,
                message);
        }
        catch (InvalidDataException)
        {
            return Unchanged(
                AiGmTurnExecutionStatus.InvalidPlan,
                eventLog,
                "La risposta del Game Master AI non rispetta il protocollo di Tessitore. " +
                "L'azione resta al GM umano.");
        }

        if (plan is null)
        {
            return Unchanged(
                AiGmTurnExecutionStatus.InvalidPlan,
                eventLog,
                "Il Game Master AI non ha restituito un piano. L'azione resta al GM umano.");
        }

        WorldEventLog updatedLog;
        try
        {
            updatedLog = _coordinator.RecordPlan(eventLog, context, plan);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
            NotSupportedException)
        {
            return Unchanged(
                AiGmTurnExecutionStatus.InvalidPlan,
                eventLog,
                "Il piano del Game Master AI è stato rifiutato dalle regole di Tessitore. " +
                "L'azione resta al GM umano.");
        }

        var pending = _coordinator.PendingConsequences(updatedLog)
            .Any(consequence =>
                consequence.PlayerActionId == playerAction.Id);
        return new AiGmTurnExecutionResult(
            pending
                ? AiGmTurnExecutionStatus.PendingConfirmation
                : AiGmTurnExecutionStatus.Completed,
            updatedLog,
            pending
                ? "Il Game Master AI attende una conferma importante."
                : "Il turno del Game Master AI è stato applicato.",
            WorldChanged: true);
    }

    private static AiGmTurnExecutionResult Unchanged(
        AiGmTurnExecutionStatus status,
        WorldEventLog eventLog,
        string message) =>
        new(status, eventLog, message, WorldChanged: false);
}
