using System.Text.RegularExpressions;
using TessitoreGM.Core;

namespace TessitoreGM.AiGm;

/// <summary>
/// Extracts deterministic anchors that keep a provider response tied to the
/// current action without asking the provider to redefine canonical state.
/// </summary>
public sealed class AiGmActionAnalyzer
{
    private static readonly Regex EconomyPattern = Pattern(
        "acquist|compr|vend|pag|don|regal|ced|scambi|baratt|rub|sottra|" +
        "prend|raccogli|ricev|guadagn|spend|monet|prezz|ricompens");
    private static readonly Regex ConflictPattern = Pattern(
        "attacc|colp|ferisc|uccid|minacc|combat|lott|spara|pugnala");
    private static readonly Regex MovementPattern = Pattern(
        "entr|esc|vado|vai|and|raggiung|spost|cammin|corr|salg|scend");
    private static readonly Regex ExplorationPattern = Pattern(
        "cerc|osserv|esamin|indag|ispezion|ascolt|segu|esplor");
    private static readonly Regex SocialPattern = Pattern(
        "parl|chied|salut|battut|scherz|distr|convinc|persuad|raccont|rispond");
    private static readonly Regex RiskPattern = Pattern(
        "attacc|colp|rub|sottra|minacc|forz|scassin|salt|arrampic|fugg|insegu|pericol");

    public AiGmActionFrame Analyze(
        string action,
        AiGmPerspectiveScene scene,
        AiGmCampaignCatalog? catalog,
        EntityId playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(scene);

        var visibleIds = scene.VisibleCharacters
            .Select(character => character.EntityId)
            .ToHashSet();
        var targets = (catalog?.Characters ?? [])
            .Where(character =>
                character.EntityId != playerId &&
                MentionsName(action, character.Name))
            .Select(character => new AiGmActionTarget(
                character.EntityId,
                character.Name,
                visibleIds.Contains(character.EntityId)))
            .ToArray();
        var kind = Classify(action);

        return new AiGmActionFrame(
            kind,
            targets,
            scene.LocationId,
            scene.LocationName,
            ConflictPattern.IsMatch(action) || RiskPattern.IsMatch(action),
            kind == AiGmActionKind.Economy);
    }

    private static AiGmActionKind Classify(string action)
    {
        if (ConflictPattern.IsMatch(action)) return AiGmActionKind.Conflict;
        if (EconomyPattern.IsMatch(action)) return AiGmActionKind.Economy;
        if (MovementPattern.IsMatch(action)) return AiGmActionKind.Movement;
        if (ExplorationPattern.IsMatch(action)) return AiGmActionKind.Exploration;
        if (SocialPattern.IsMatch(action)) return AiGmActionKind.Social;
        return AiGmActionKind.Other;
    }

    internal static bool MentionsName(string text, string name)
    {
        if (ContainsWhole(text, name))
        {
            return true;
        }

        return name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length >= 4)
            .Any(part => ContainsWhole(text, part));
    }

    private static bool ContainsWhole(string text, string value) => Regex.IsMatch(
        text,
        $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(value)}(?![\p{{L}}\p{{N}}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Regex Pattern(string stems) => new(
        $@"\b(?:{stems})\w*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
