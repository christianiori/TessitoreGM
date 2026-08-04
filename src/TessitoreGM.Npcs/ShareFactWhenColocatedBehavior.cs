using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class ShareFactWhenColocatedBehavior : INpcBehavior
{
    private readonly EntityId _listenerId;
    private readonly FactId _factId;

    public ShareFactWhenColocatedBehavior(
        EntityId listenerId,
        FactId factId)
    {
        _listenerId = listenerId;
        _factId = factId;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        var speakerLocation = world.GetLocation(npcId);
        if ((!memory.KnowsFact(_factId) &&
             !world.HasLearnedFact(npcId, _factId)) ||
            speakerLocation is null ||
            speakerLocation != world.GetLocation(_listenerId) ||
            world.HasSharedFact(npcId, _listenerId, _factId) ||
            world.CurrentTime > until)
        {
            return null;
        }

        return new FactShared(npcId, _listenerId, _factId, world.CurrentTime);
    }
}
