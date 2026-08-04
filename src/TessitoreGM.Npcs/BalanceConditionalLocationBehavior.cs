using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class BalanceConditionalLocationBehavior : INpcBehavior
{
    private readonly LocationId _destinationId;
    private readonly TimeSpan _timeOfDay;
    private readonly int _maximumBalance;

    public BalanceConditionalLocationBehavior(
        LocationId destinationId,
        TimeSpan timeOfDay,
        int maximumBalance)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDay),
                "A daily decision time must fall within one day.");
        }

        if (maximumBalance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBalance),
                "A balance threshold cannot be negative.");
        }

        _destinationId = destinationId;
        _timeOfDay = timeOfDay;
        _maximumBalance = maximumBalance;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        if (world.GetBalance(npcId) > _maximumBalance ||
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
