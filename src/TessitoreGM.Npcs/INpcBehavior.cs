using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public interface INpcBehavior
{
    IWorldEvent? Evaluate(
        EntityId npcId,
        WorldSnapshot world,
        DateTimeOffset currentTime);
}
