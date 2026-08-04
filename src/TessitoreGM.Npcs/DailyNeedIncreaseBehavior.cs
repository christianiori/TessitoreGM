using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class DailyNeedIncreaseBehavior : INpcBehavior
{
    private readonly NeedId _needId;
    private readonly TimeSpan _timeOfDay;
    private readonly int _amount;

    public DailyNeedIncreaseBehavior(
        NeedId needId,
        TimeSpan timeOfDay,
        int amount)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDay));
        }

        if (amount <= 0 || amount > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        _needId = needId;
        _timeOfDay = timeOfDay;
        _amount = amount;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        var scheduledAt = new DateTimeOffset(
            world.CurrentTime.Date.Add(_timeOfDay),
            world.CurrentTime.Offset);
        var lastIncrease = world.GetLastNeedIncreaseAt(npcId, _needId);

        if (scheduledAt < world.CurrentTime || lastIncrease >= scheduledAt)
        {
            scheduledAt = scheduledAt.AddDays(1);
        }

        return scheduledAt <= until
            ? new NeedIncreased(npcId, _needId, _amount, scheduledAt)
            : null;
    }
}
