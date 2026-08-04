using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class TrustConditionalLocationBehavior : INpcBehavior
{
    private readonly EntityId _trustedEntityId;
    private readonly LocationId _destinationId;
    private readonly TimeSpan _timeOfDay;
    private readonly int _minimumTrust;

    public TrustConditionalLocationBehavior(
        EntityId trustedEntityId,
        LocationId destinationId,
        TimeSpan timeOfDay,
        int minimumTrust)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDay),
                "A daily decision time must fall within one day.");
        }

        if (minimumTrust <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumTrust),
                "Minimum trust must be greater than zero.");
        }

        _trustedEntityId = trustedEntityId;
        _destinationId = destinationId;
        _timeOfDay = timeOfDay;
        _minimumTrust = minimumTrust;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetTrust(npcId, _trustedEntityId) < _minimumTrust ||
            world.GetLocation(npcId) == _destinationId)
        {
            return null;
        }

        var decisionAt = new DateTimeOffset(
            world.CurrentTime.Date.Add(_timeOfDay),
            world.CurrentTime.Offset);

        if (decisionAt < world.CurrentTime)
        {
            decisionAt = decisionAt.AddDays(1);
        }

        return decisionAt <= until
            ? new EntityEnteredLocation(npcId, _destinationId, decisionAt)
            : null;
    }
}
