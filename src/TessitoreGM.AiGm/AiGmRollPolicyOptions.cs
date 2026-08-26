namespace TessitoreGM.AiGm;

/// <summary>
/// Bounds AI-authored d20 requests before Tessitore persists them. Human GM
/// controls remain independent and may use the wider limits of the dashboard.
/// </summary>
public sealed record AiGmRollPolicyOptions(
    int MinimumModifier = -10,
    int MaximumModifier = 10,
    int MinimumDifficulty = 5,
    int MaximumDifficulty = 30,
    bool AllowAdvantage = true,
    bool AllowDisadvantage = true);
