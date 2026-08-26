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
    private static readonly Regex PlayerEconomicIntentPattern = new(
        @"\b(?:acquist|compr|vend|pag|don|regal|ced|scambi|baratt|" +
        @"rub|sottra|prend|raccogli|ricev|guadagn|perd|spend|" +
        @"monet|prezz|ricompens|risors|provvist|merce|inventari)\w*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UnobservableMentalStatePattern = new(
        @"\b(?:spera|sperando|sperare|pensa|pensando|pensare|crede|credendo|" +
        @"credere|vuole|volendo|desidera|desiderando|teme|temendo|decide|" +
        @"decidendo|immagina|immaginando|si\s+chiede)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
        else if (FindPlayerAgencyAttribution(
            context,
            plan.Narration) is { } agencyAttribution)
        {
            errors.Add(
                "La narrazione attribuisce azioni, decisioni o pensieri " +
                $"al giocatore umano. Frammento: \"{agencyAttribution}\".");
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

            if (IsUngroundedPlayerEconomicConsequence(context, proposal))
            {
                errors.Add(
                    "La conseguenza modifica monete o risorse del giocatore, " +
                    "ma la sua azione non esprime un intento economico o materiale.");
            }
        }

        return new AiGmTurnPlanValidation(errors.Count == 0, errors);
    }

    internal static bool IsUngroundedPlayerEconomicConsequence(
        AiGmTurnContext context,
        AiGmCommandProposal proposal) =>
        ChangesPlayerEconomy(context, proposal) &&
        !HasPlayerEconomicIntent(context, proposal);

    internal static string RemovePlayerAgencySentences(
        AiGmTurnContext context,
        string narration)
    {
        if (string.IsNullOrWhiteSpace(narration) ||
            FindPlayerAgencyAttribution(context, narration) is null)
        {
            return narration;
        }

        var safeSentences = Regex.Split(
                narration.Trim(),
                @"(?<=[.!?])\s+")
            .Where(sentence =>
                FindPlayerAgencyAttribution(context, sentence) is null)
            .ToArray();

        return safeSentences.Length > 0
            ? string.Join(" ", safeSentences)
            : "La scena reagisce alla tua azione, lasciandoti libero di decidere come proseguire.";
    }

    internal static string EnsureActionRelevance(
        AiGmTurnContext context,
        string narration)
    {
        var frame = context.ActionFrame;
        if (frame is null)
        {
            return narration;
        }

        var sceneSafeSentences = Regex.Split(
                narration.Trim(),
                @"(?<=[.!?])\s+")
            .Where(sentence => !(context.Catalog?.Locations ?? [])
                .Where(location => location.LocationId != frame.CurrentLocationId)
                .Any(location => ContainsWhole(sentence, location.Name)))
            .ToArray();
        var sceneSafeNarration = sceneSafeSentences.Length > 0
            ? string.Join(" ", sceneSafeSentences)
            : string.Empty;
        var target = frame.Targets.FirstOrDefault();
        if (target is null)
        {
            return string.IsNullOrWhiteSpace(sceneSafeNarration)
                ? "La scena resta immutata e attende la tua prossima decisione."
                : sceneSafeNarration;
        }

        if (!target.IsVisible)
        {
            var describesAbsence = AiGmActionAnalyzer.MentionsName(
                    sceneSafeNarration,
                    target.Name) &&
                Regex.IsMatch(
                    sceneSafeNarration,
                    @"\b(?:non\s+(?:è|si\s+trova)\s+(?:qui|presente)|assente|altrove)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return describesAbsence
                ? sceneSafeNarration
                : $"{target.Name} non è presente in questa scena; non può reagire direttamente alla tua azione.";
        }

        return AiGmActionAnalyzer.MentionsName(sceneSafeNarration, target.Name)
            ? sceneSafeNarration
            : $"{target.Name} reagisce alla tua azione e attende di vedere come proseguirai.";
    }

    internal static string RemoveUnobservableMentalStateSentences(
        string narration)
    {
        if (string.IsNullOrWhiteSpace(narration) ||
            !UnobservableMentalStatePattern.IsMatch(narration))
        {
            return narration;
        }

        var observable = Regex.Split(narration.Trim(), @"(?<=[.!?])\s+")
            .Where(sentence =>
                !UnobservableMentalStatePattern.IsMatch(sentence))
            .ToArray();
        return observable.Length > 0
            ? string.Join(" ", observable)
            : string.Empty;
    }

    private static bool ContainsWhole(string text, string value) => Regex.IsMatch(
        text,
        $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(value)}(?![\p{{L}}\p{{N}}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool ChangesPlayerEconomy(
        AiGmTurnContext context,
        AiGmCommandProposal proposal) => proposal switch
        {
            TransferCoinsProposal coins =>
                coins.PayerId == context.PlayerCharacterId ||
                coins.PayeeId == context.PlayerCharacterId,
            AcquireResourceProposal acquired =>
                acquired.EntityId == context.PlayerCharacterId,
            LoseResourceProposal lost =>
                lost.EntityId == context.PlayerCharacterId,
            TransferResourceProposal transfer =>
                transfer.SourceId == context.PlayerCharacterId ||
                transfer.DestinationId == context.PlayerCharacterId,
            _ => false
        };

    private static bool HasPlayerEconomicIntent(
        AiGmTurnContext context,
        AiGmCommandProposal proposal)
    {
        if (PlayerEconomicIntentPattern.IsMatch(context.PlayerAction))
        {
            return true;
        }

        var resourceId = proposal switch
        {
            AcquireResourceProposal acquired => acquired.ResourceId,
            LoseResourceProposal lost => lost.ResourceId,
            TransferResourceProposal transfer => transfer.ResourceId,
            _ => (TessitoreGM.Core.ResourceId?)null
        };
        if (resourceId is null)
        {
            return false;
        }

        var resource = context.Catalog?.Resources.FirstOrDefault(candidate =>
            candidate.ResourceId == resourceId.Value);
        return resource is not null &&
            Regex.IsMatch(
                context.PlayerAction,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(resource.Name)}" +
                @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? FindPlayerAgencyAttribution(
        AiGmTurnContext context,
        string narration)
    {
        var playerNamePattern =
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(context.PlayerCharacterName)}" +
            @"(?![\p{L}\p{N}])\s*,?\s+(?:(?:si|gli|le|lo|la|ne)\s+)?" +
            @"(?:avvicina|allontana|muove|mette|decide|sceglie|inizia|" +
            @"prova|cerca|pensa|dice|chiede|risponde|accetta|rifiuta|" +
            @"prende|lascia|entra|esce|segue|attacca|usa)\b";
        var namedPlayerMatch = Regex.Match(
            narration,
            playerNamePattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (namedPlayerMatch.Success)
        {
            return namedPlayerMatch.Value;
        }

        foreach (var pattern in ForbiddenPlayerAgencyPatterns)
        {
            var match = pattern.Match(narration);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }
}
