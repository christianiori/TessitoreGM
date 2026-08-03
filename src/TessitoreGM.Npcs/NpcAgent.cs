using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class NpcAgent : IWorldRule
{
    private readonly IReadOnlyList<INpcBehavior> _behaviors;

    public NpcAgent(
        EntityId id,
        string role,
        NpcMemory memory,
        IEnumerable<INpcBehavior> behaviors)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("An NPC role cannot be empty.", nameof(role));
        }

        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(behaviors);

        if (memory.OwnerId != id)
        {
            throw new ArgumentException(
                "NPC memory must belong to the agent.",
                nameof(memory));
        }

        Id = id;
        Role = role;
        Memory = memory;
        _behaviors = behaviors.ToArray();
    }

    public EntityId Id { get; }

    public string Role { get; }

    public NpcMemory Memory { get; }

    public IWorldEvent? ProposeNext(
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        return _behaviors
            .Select((behavior, index) => new
            {
                Event = behavior.ProposeNext(
                Id,
                Memory,
                world,
                    until),
                BehaviorIndex = index
            })
            .Where(proposal => proposal.Event is not null)
            .OrderBy(proposal => proposal.Event!.OccurredAt)
            .ThenBy(proposal => proposal.BehaviorIndex)
            .Select(proposal => proposal.Event)
            .FirstOrDefault();
    }
}
