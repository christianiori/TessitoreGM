using TessitoreGM.Events;

namespace TessitoreGM.World;

public interface IWorldRule
{
    IWorldEvent? Evaluate(WorldSnapshot world, DateTimeOffset currentTime);
}
