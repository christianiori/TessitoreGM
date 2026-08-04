using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class KnowledgeConditionalLocationBehavior : INpcBehavior
{
    private readonly LocationId _destinationId;
    private readonly TimeSpan _timeOfDay;
    private readonly FactId _requiredFactId;

    public KnowledgeConditionalLocationBehavior(
        LocationId destinationId,
        TimeSpan timeOfDay,
        FactId requiredFactId)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDay),
                "A daily decision time must fall within one day.");
        }

        _destinationId = destinationId;
        _timeOfDay = timeOfDay;
        _requiredFactId = requiredFactId;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        if (!memory.KnowsFact(_requiredFactId) ||
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
