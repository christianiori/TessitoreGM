using System.Text.RegularExpressions;

namespace TessitoreGM.AiGm;

public sealed record AiGmTurnPlanValidation(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed class AiGmTurnPlanValidator
{
    private const int MaximumNarrationLength = 4000;
    private static readonly Regex[] ForbiddenPlayerAgencyPatterns =
    [
        new(@"\bti\s+(?:avvicini|allontani|muovi|metti|chiedi)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(?:decidi|scegli|inizi|provi|cerchi|pensi|dici|chiedi|rispondi|accetti|rifiuti|prendi|lasci|entri|esci|segui|attacchi|usi)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(?:hai|avevi)\s+(?:deciso|scelto|iniziato|provato|cercato|pensato|detto|chiesto|risposto|accettato|rifiutato|preso|lasciato|seguito|attaccato|usato)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];
    private readonly AiGmProposalValidator _proposalValidator;
    private readonly AiGmRollPolicyOptions _rollOptions;

    public AiGmTurnPlanValidator(
        AiGmProposalValidator proposalValidator,
        AiGmRollPolicyOptions? rollOptions = null)
    {
        _proposalValidator = proposalValidator ??
            throw new ArgumentNullException(nameof(proposalValidator));
        _rollOptions = rollOptions ?? new AiGmRollPolicyOptions();
        if (_rollOptions.MinimumModifier > _rollOptions.MaximumModifier ||
            _rollOptions.MinimumDifficulty <= 0 ||
            _rollOptions.MinimumDifficulty > _rollOptions.MaximumDifficulty)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rollOptions),
                "AI GM roll bounds are invalid.");
        }
    }

    public AiGmTurnPlanValidation Validate(
        AiGmTurnContext context,
        AiGmTurnPlan plan)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plan);

        var errors = new List<string>();

        if (plan.PlayerActionId != context.PlayerActionId)
        {
            errors.Add(
                "Il piano AI non risponde all'azione del giocatore corrente.");
        }

        if (string.IsNullOrWhiteSpace(plan.Narration))
        {
            errors.Add("La narrazione non può essere vuota.");
        }
        else if (plan.Narration.Length > MaximumNarrationLength)
        {
            errors.Add("La narrazione supera la lunghezza consentita.");
        }
        else if (AttributesAgencyToPlayer(context, plan.Narration))
        {
            errors.Add(
                "La narrazione attribuisce azioni, decisioni o pensieri al giocatore umano.");
        }

        if (plan.Roll is { } roll)
        {
            if (string.IsNullOrWhiteSpace(roll.Reason))
            {
                errors.Add("La richiesta di tiro deve avere una motivazione.");
            }

            if (roll.Reason?.Length > 500)
            {
                errors.Add("La motivazione del tiro è troppo lunga.");
            }

            if (roll.Modifier < _rollOptions.MinimumModifier ||
                roll.Modifier > _rollOptions.MaximumModifier)
            {
                errors.Add(
                    $"Il modificatore AI deve essere compreso tra " +
                    $"{_rollOptions.MinimumModifier} e " +
                    $"{_rollOptions.MaximumModifier}.");
            }

            if (roll.Difficulty is not { } difficulty ||
                difficulty < _rollOptions.MinimumDifficulty ||
                difficulty > _rollOptions.MaximumDifficulty)
            {
                errors.Add(
                    $"La difficoltà AI deve essere compresa tra " +
                    $"{_rollOptions.MinimumDifficulty} e " +
                    $"{_rollOptions.MaximumDifficulty}.");
            }

            if ((!_rollOptions.AllowAdvantage &&
                    roll.Mode == TessitoreGM.Events.D20RollMode.Advantage) ||
                (!_rollOptions.AllowDisadvantage &&
                    roll.Mode == TessitoreGM.Events.D20RollMode.Disadvantage))
            {
                errors.Add("La modalità del tiro AI non è consentita.");
            }

            if ((plan.Consequences ?? []).Count > 0)
            {
                errors.Add(
                    "Un piano che richiede un tiro non può applicare conseguenze prima del risultato.");
            }
        }

        foreach (var proposal in plan.Consequences ?? [])
        {
            var validation = _proposalValidator.Validate(proposal);
            if (!validation.IsValid)
            {
                errors.Add(validation.Error ?? "Conseguenza AI non valida.");
            }
        }

        return new AiGmTurnPlanValidation(errors.Count == 0, errors);
    }

    private static bool AttributesAgencyToPlayer(
        AiGmTurnContext context,
        string narration)
    {
        var playerNamePattern =
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(context.PlayerCharacterName)}" +
            @"(?![\p{L}\p{N}])";
        if (Regex.IsMatch(
            narration,
            playerNamePattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return ForbiddenPlayerAgencyPatterns.Any(pattern =>
            pattern.IsMatch(narration));
    }
}
