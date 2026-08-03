using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public interface INpcBehavior
{
    IWorldEvent? Evaluate(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset currentTime);
}
