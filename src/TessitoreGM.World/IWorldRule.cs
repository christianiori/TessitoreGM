using TessitoreGM.Events;

namespace TessitoreGM.World;

/// <summary>
/// Public extension contract for a deterministic simulation rule. A rule may
/// inspect the current immutable snapshot and propose one supported world
/// event within the requested interval. It must not mutate external state.
/// </summary>
public interface IWorldRule
{
    /// <summary>
    /// Returns the next event proposed by this rule, or <see langword="null"/>
    /// when no event is due before <paramref name="until"/>.
    /// </summary>
    IWorldEvent? ProposeNext(WorldSnapshot world, DateTimeOffset until);
}
