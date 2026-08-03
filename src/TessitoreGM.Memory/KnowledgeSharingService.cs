using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Memory;

public sealed class KnowledgeSharingService
{
    public FactShared Share(
        WorldSnapshot world,
        NpcMemory speakerMemory,
        EntityId listenerId,
        FactId factId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(speakerMemory);

        if (!speakerMemory.KnowsFact(factId))
        {
            throw new InvalidOperationException(
                $"Entity '{speakerMemory.OwnerId}' does not know fact '{factId}'.");
        }

        if (speakerMemory.OwnerId == listenerId)
        {
            throw new InvalidOperationException(
                "An entity cannot share a fact with itself.");
        }

        var speakerLocation = world.GetLocation(speakerMemory.OwnerId);
        if (speakerLocation is null || speakerLocation != world.GetLocation(listenerId))
        {
            throw new InvalidOperationException(
                "Speaker and listener must be in the same location.");
        }

        return new FactShared(
            speakerMemory.OwnerId,
            listenerId,
            factId,
            occurredAt);
    }
}
