namespace TessitoreGM.AiGm;

public sealed record AiGmTurnPlanValidation(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed class AiGmTurnPlanValidator
{
    private const int MaximumNarrationLength = 4000;
    private readonly AiGmProposalValidator _proposalValidator;

    public AiGmTurnPlanValidator(AiGmProposalValidator proposalValidator)
    {
        _proposalValidator = proposalValidator ??
            throw new ArgumentNullException(nameof(proposalValidator));
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

        if (plan.Roll is { } roll)
        {
            if (string.IsNullOrWhiteSpace(roll.Reason))
            {
                errors.Add("La richiesta di tiro deve avere una motivazione.");
            }

            if (roll.Difficulty is <= 0)
            {
                errors.Add("La difficoltà del tiro deve essere positiva.");
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
}
