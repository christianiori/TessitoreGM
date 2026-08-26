using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.AiGm;

/// <summary>
/// Persists AI GM plans, applies validated routine consequences and keeps
/// important consequences inert until an explicit approval is recorded.
/// </summary>
public sealed class AiGmTurnCoordinator
{
    private readonly WorldEventProcessor _processor = new();
    private readonly WorldEventLogEditor _editor = new();
    private readonly AiGmConsequencePolicyOptions _policyOptions;
    private readonly AiGmRollPolicyOptions _rollOptions;

    public AiGmTurnCoordinator(
        AiGmConsequencePolicyOptions? policyOptions = null,
        AiGmRollPolicyOptions? rollOptions = null)
    {
        _policyOptions = policyOptions ?? new AiGmConsequencePolicyOptions();
        _rollOptions = rollOptions ?? new AiGmRollPolicyOptions();
    }

    public WorldEventLog RecordPlan(
        WorldEventLog eventLog,
        AiGmTurnContext context,
        AiGmTurnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plan);

        if ((eventLog.AiGmTurns ?? []).Any(turn =>
            turn.PlayerActionId == context.PlayerActionId))
        {
            throw new InvalidOperationException(
                "This player action already has a persisted AI GM turn.");
        }

        var action = (eventLog.PlayerActions ?? []).SingleOrDefault(candidate =>
            candidate.Id == context.PlayerActionId)
            ?? throw new InvalidOperationException(
                "The AI GM turn does not refer to a persisted player action.");
        if (action.PlayerCharacterId != context.PlayerCharacterId ||
            action.Status is not (PlayerActionStatus.Pending or
                PlayerActionStatus.Rolled))
        {
            throw new InvalidOperationException(
                "The player action is not awaiting an AI GM resolution.");
        }

        var world = Replay(eventLog);
        var planValidation = new AiGmTurnPlanValidator(
            new AiGmProposalValidator(eventLog, world),
            _rollOptions)
            .Validate(context, plan);
        if (!planValidation.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(" ", planValidation.Errors));
        }

        if (plan.Roll is { } roll)
        {
            if (action.Status != PlayerActionStatus.Pending)
            {
                throw new InvalidOperationException(
                    "Il Game Master AI non può richiedere un secondo tiro per la stessa azione.");
            }

            return RequestRoll(eventLog, action, plan.Narration, roll, world);
        }

        var humanPlayerIds = eventLog.Events
            .OfType<PlayerCharacterRegistered>()
            .Select(player => player.EntityId)
            .ToHashSet();
        var policy = new AiGmConsequencePolicy(
            humanPlayerIds,
            _policyOptions);
        var updatedLog = eventLog;
        var records = new List<AiGmConsequenceRecord>();

        foreach (var proposal in plan.Consequences ?? [])
        {
            var currentWorld = Replay(updatedLog);
            var validation = new AiGmProposalValidator(updatedLog, currentWorld)
                .Validate(proposal);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    validation.Error ?? "The AI GM consequence is invalid.");
            }

            var assessment = policy.Assess(proposal);
            var requiresConfirmation = assessment.Importance ==
                AiGmConsequenceImportance.RequiresConfirmation;
            var consequenceId = Guid.NewGuid();

            if (requiresConfirmation)
            {
                records.Add(ToRecord(
                    consequenceId,
                    proposal,
                    assessment,
                    AiGmConsequenceStatus.PendingConfirmation,
                    null));
                continue;
            }

            var worldEvent = ToWorldEvent(proposal, currentWorld.CurrentTime);
            updatedLog = _editor.Append(updatedLog, currentWorld, worldEvent);
            records.Add(ToRecord(
                consequenceId,
                proposal,
                assessment,
                AiGmConsequenceStatus.Applied,
                currentWorld.CurrentTime));
        }

        var turnStatus = records.Any(record =>
            record.Status == AiGmConsequenceStatus.PendingConfirmation)
                ? AiGmTurnStatus.PendingConfirmation
                : AiGmTurnStatus.Completed;
        var turn = new AiGmTurnRecord(
            Guid.NewGuid(),
            context.PlayerActionId,
            context.PlayerCharacterId,
            plan.Narration,
            world.CurrentTime,
            turnStatus,
            records,
            context.World.Actors.First(actor =>
                actor.EntityId == context.PlayerCharacterId).LocationId,
            context.World.Actors.Select(actor => actor.EntityId).ToArray());

        updatedLog = updatedLog with
        {
            AiGmTurns = (updatedLog.AiGmTurns ?? []).Append(turn).ToArray()
        };

        return turnStatus == AiGmTurnStatus.Completed
            ? CompletePlayerAction(updatedLog, turn)
            : updatedLog;
    }

    private static WorldEventLog RequestRoll(
        WorldEventLog eventLog,
        PlayerActionProposal action,
        string narration,
        AiGmRollRequest request,
        WorldSnapshot world)
    {
        var actions = (eventLog.PlayerActions ?? []).ToArray();
        var actionIndex = Array.FindIndex(actions, candidate =>
            candidate.Id == action.Id);
        actions[actionIndex] = action with
        {
            Status = PlayerActionStatus.RollRequested,
            Roll = new D20Roll(
                request.Modifier,
                request.Difficulty,
                request.DifficultyVisible,
                request.Mode,
                world.CurrentTime,
                Reason: request.Reason.Trim(),
                PromptNarration: narration.Trim())
        };

        return eventLog with { PlayerActions = actions };
    }

    public WorldEventLog ResolveConsequence(
        WorldEventLog eventLog,
        Guid consequenceId,
        bool approve,
        string? resolution = null)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        if (consequenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A consequence id cannot be empty.",
                nameof(consequenceId));
        }

        var turns = (eventLog.AiGmTurns ?? []).ToArray();
        var turnIndex = Array.FindIndex(turns, turn =>
            turn.Consequences.Any(consequence => consequence.Id == consequenceId));
        if (turnIndex < 0)
        {
            throw new InvalidOperationException(
                "The AI GM consequence does not exist.");
        }

        var consequences = turns[turnIndex].Consequences.ToArray();
        var consequenceIndex = Array.FindIndex(
            consequences,
            consequence => consequence.Id == consequenceId);
        var consequence = consequences[consequenceIndex];
        if (consequence.Status != AiGmConsequenceStatus.PendingConfirmation)
        {
            throw new InvalidOperationException(
                "The AI GM consequence is no longer awaiting confirmation.");
        }

        var world = Replay(eventLog);
        var updatedLog = eventLog;
        if (approve)
        {
            var proposal = ToProposal(consequence);
            var validation = new AiGmProposalValidator(eventLog, world)
                .Validate(proposal);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    validation.Error ?? "The consequence is no longer valid.");
            }

            updatedLog = _editor.Append(
                eventLog,
                world,
                ToWorldEvent(proposal, world.CurrentTime));
            consequences[consequenceIndex] = consequence with
            {
                Status = AiGmConsequenceStatus.Applied,
                ResolvedAt = world.CurrentTime,
                Resolution = NormalizedResolution(resolution, "Approvata")
            };
        }
        else
        {
            consequences[consequenceIndex] = consequence with
            {
                Status = AiGmConsequenceStatus.Rejected,
                ResolvedAt = world.CurrentTime,
                Resolution = NormalizedResolution(resolution, "Rifiutata")
            };
        }

        var turn = turns[turnIndex] with
        {
            Consequences = consequences,
            Status = consequences.Any(candidate =>
                candidate.Status ==
                    AiGmConsequenceStatus.PendingConfirmation)
                        ? AiGmTurnStatus.PendingConfirmation
                        : AiGmTurnStatus.Completed
        };
        turns[turnIndex] = turn;
        updatedLog = updatedLog with { AiGmTurns = turns };

        return turn.Status == AiGmTurnStatus.Completed
            ? CompletePlayerAction(updatedLog, turn)
            : updatedLog;
    }

    public IReadOnlyList<AiGmPendingConsequence> PendingConsequences(
        WorldEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(eventLog);

        return (eventLog.AiGmTurns ?? [])
            .SelectMany(turn => turn.Consequences
                .Where(consequence => consequence.Status ==
                    AiGmConsequenceStatus.PendingConfirmation)
                .Select(consequence => new AiGmPendingConsequence(
                    turn.Id,
                    turn.PlayerActionId,
                    turn.PlayerCharacterId,
                    turn.Narration,
                    consequence)))
            .ToArray();
    }

    private WorldEventLog CompletePlayerAction(
        WorldEventLog eventLog,
        AiGmTurnRecord turn)
    {
        var actions = (eventLog.PlayerActions ?? []).ToArray();
        var actionIndex = Array.FindIndex(
            actions,
            action => action.Id == turn.PlayerActionId);
        if (actionIndex < 0)
        {
            throw new InvalidOperationException(
                "The AI GM turn has no matching player action.");
        }
        if (actions[actionIndex].Status == PlayerActionStatus.Approved)
        {
            return eventLog;
        }

        var world = Replay(eventLog);
        var playerName = eventLog.Events
            .OfType<PlayerCharacterRegistered>()
            .Last(player => player.EntityId == turn.PlayerCharacterId)
            .Name;
        var updatedLog = _editor.Append(
            eventLog,
            world,
            new PlayerActionRecorded(
                playerName,
                actions[actionIndex].Description,
                world.CurrentTime));
        actions[actionIndex] = actions[actionIndex] with
        {
            Status = PlayerActionStatus.Approved,
            Resolution = "Risolta dal Game Master AI.",
            ResolvedAt = world.CurrentTime
        };

        return updatedLog with { PlayerActions = actions };
    }

    private WorldSnapshot Replay(WorldEventLog eventLog) =>
        _processor.Replay(
            WorldSnapshot.Create(
                eventLog.InitialWorld.CurrentTime,
                eventLog.InitialWorld.Balances.ToDictionary(
                    balance => balance.EntityId,
                    balance => balance.Amount),
                eventLog.InitialWorld.ResourceStocks ?? [],
                eventLog.InitialWorld.Weather),
            eventLog.Events);

    private static IWorldEvent ToWorldEvent(
        AiGmCommandProposal proposal,
        DateTimeOffset occurredAt) => proposal switch
        {
            MoveEntityProposal move => new EntityEnteredLocation(
                move.EntityId,
                move.DestinationId,
                occurredAt),
            TransferCoinsProposal coins => new CoinsTransferred(
                coins.PayerId,
                coins.PayeeId,
                coins.Amount,
                coins.Reason,
                occurredAt),
            AcquireResourceProposal acquired => new ResourceAcquired(
                acquired.EntityId,
                acquired.ResourceId,
                acquired.Quantity,
                acquired.Reason,
                occurredAt),
            LoseResourceProposal lost => new ResourceLost(
                lost.EntityId,
                lost.ResourceId,
                lost.Quantity,
                lost.Reason,
                occurredAt),
            TransferResourceProposal transfer => new ResourceTransferred(
                transfer.SourceId,
                transfer.DestinationId,
                transfer.ResourceId,
                transfer.Quantity,
                transfer.Reason,
                occurredAt),
            RevealFactProposal revealed => new FactRevealed(
                revealed.EntityId,
                revealed.FactId,
                occurredAt),
            ChangeTrustProposal trust => new TrustChanged(
                trust.SubjectId,
                trust.OtherEntityId,
                trust.Amount,
                trust.Reason,
                occurredAt),
            _ => throw new NotSupportedException(
                "The AI GM consequence type is not supported.")
        };

    private static AiGmConsequenceRecord ToRecord(
        Guid id,
        AiGmCommandProposal proposal,
        AiGmConsequenceAssessment assessment,
        AiGmConsequenceStatus status,
        DateTimeOffset? resolvedAt)
    {
        var requiresConfirmation = assessment.Importance ==
            AiGmConsequenceImportance.RequiresConfirmation;
        return proposal switch
        {
            MoveEntityProposal move => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.MoveEntity, status,
                requiresConfirmation, assessment.Reason, move.Reason,
                EntityId: move.EntityId, LocationId: move.DestinationId,
                ResolvedAt: resolvedAt),
            TransferCoinsProposal coins => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.TransferCoins, status,
                requiresConfirmation, assessment.Reason, coins.Reason,
                EntityId: coins.PayerId, OtherEntityId: coins.PayeeId,
                Amount: coins.Amount, ResolvedAt: resolvedAt),
            AcquireResourceProposal acquired => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.AcquireResource, status,
                requiresConfirmation, assessment.Reason, acquired.Reason,
                EntityId: acquired.EntityId, ResourceId: acquired.ResourceId,
                Amount: acquired.Quantity, ResolvedAt: resolvedAt),
            LoseResourceProposal lost => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.LoseResource, status,
                requiresConfirmation, assessment.Reason, lost.Reason,
                EntityId: lost.EntityId, ResourceId: lost.ResourceId,
                Amount: lost.Quantity, ResolvedAt: resolvedAt),
            TransferResourceProposal transfer => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.TransferResource, status,
                requiresConfirmation, assessment.Reason, transfer.Reason,
                EntityId: transfer.SourceId,
                OtherEntityId: transfer.DestinationId,
                ResourceId: transfer.ResourceId, Amount: transfer.Quantity,
                ResolvedAt: resolvedAt),
            RevealFactProposal revealed => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.RevealFact, status,
                requiresConfirmation, assessment.Reason, revealed.Reason,
                EntityId: revealed.EntityId, FactId: revealed.FactId,
                ResolvedAt: resolvedAt),
            ChangeTrustProposal trust => new AiGmConsequenceRecord(
                id, AiGmConsequenceKind.ChangeTrust, status,
                requiresConfirmation, assessment.Reason, trust.Reason,
                EntityId: trust.SubjectId,
                OtherEntityId: trust.OtherEntityId, Amount: trust.Amount,
                ResolvedAt: resolvedAt),
            _ => throw new NotSupportedException(
                "The AI GM consequence type is not supported.")
        };
    }

    private static AiGmCommandProposal ToProposal(
        AiGmConsequenceRecord consequence) => consequence.Kind switch
        {
            AiGmConsequenceKind.MoveEntity => new MoveEntityProposal(
                consequence.EntityId!.Value,
                consequence.LocationId!.Value,
                consequence.Reason),
            AiGmConsequenceKind.TransferCoins => new TransferCoinsProposal(
                consequence.EntityId!.Value,
                consequence.OtherEntityId!.Value,
                consequence.Amount!.Value,
                consequence.Reason),
            AiGmConsequenceKind.AcquireResource => new AcquireResourceProposal(
                consequence.EntityId!.Value,
                consequence.ResourceId!.Value,
                consequence.Amount!.Value,
                consequence.Reason),
            AiGmConsequenceKind.LoseResource => new LoseResourceProposal(
                consequence.EntityId!.Value,
                consequence.ResourceId!.Value,
                consequence.Amount!.Value,
                consequence.Reason),
            AiGmConsequenceKind.TransferResource =>
                new TransferResourceProposal(
                    consequence.EntityId!.Value,
                    consequence.OtherEntityId!.Value,
                    consequence.ResourceId!.Value,
                    consequence.Amount!.Value,
                    consequence.Reason),
            AiGmConsequenceKind.RevealFact => new RevealFactProposal(
                consequence.EntityId!.Value,
                consequence.FactId!.Value,
                consequence.Reason),
            AiGmConsequenceKind.ChangeTrust => new ChangeTrustProposal(
                consequence.EntityId!.Value,
                consequence.OtherEntityId!.Value,
                consequence.Amount!.Value,
                consequence.Reason),
            _ => throw new NotSupportedException(
                "The persisted AI GM consequence type is not supported.")
        };

    private static string NormalizedResolution(
        string? resolution,
        string fallback)
    {
        var normalized = resolution?.Trim();
        if (normalized?.Length > 500)
        {
            throw new ArgumentException(
                "The resolution cannot exceed 500 characters.",
                nameof(resolution));
        }

        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}

public sealed record AiGmPendingConsequence(
    Guid TurnId,
    Guid PlayerActionId,
    EntityId PlayerCharacterId,
    string Narration,
    AiGmConsequenceRecord Consequence);
