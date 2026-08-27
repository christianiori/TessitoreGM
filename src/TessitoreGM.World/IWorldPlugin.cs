namespace TessitoreGM.World;

/// <summary>
/// Public contract implemented by a trusted local TessitoreGM plugin.
/// </summary>
public interface IWorldPlugin
{
    string Id { get; }

    string Version { get; }

    void Register(WorldRuleRegistry rules);
}
