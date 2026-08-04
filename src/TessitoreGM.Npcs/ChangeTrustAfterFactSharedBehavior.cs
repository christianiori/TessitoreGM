using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class ChangeTrustAfterFactSharedBehavior : INpcBehavior
{
    private readonly EntityId _speakerId;
    private readonly FactId _factId;
    private readonly int _amount;
    private readonly string _reason;

    public ChangeTrustAfterFactSharedBehavior(
        EntityId speakerId,
        FactId factId,
        int amount,
        string reason)
    {
        if (amount is 0 or < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "A trust change must be between -100 and 100 and cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A trust change requires a reason.",
                nameof(reason));
        }

        _speakerId = speakerId;
        _factId = factId;
        _amount = amount;
        _reason = reason;
    }

    public IWorldEvent? ProposeNext(
        EntityId npcId,
        NpcMemory memory,
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(world);

        if (!world.HasSharedFact(_speakerId, npcId, _factId) ||
            world.HasTrustReason(npcId, _speakerId, _reason) ||
            world.CurrentTime > until)
        {
            return null;
        }

        return new TrustChanged(
            npcId,
            _speakerId,
            _amount,
            _reason,
            world.CurrentTime);
    }
}
