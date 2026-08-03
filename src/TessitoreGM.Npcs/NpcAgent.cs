using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class NpcAgent : IWorldRule
{
    private readonly IReadOnlyList<INpcBehavior> _behaviors;

    public NpcAgent(
        EntityId id,
        string role,
        IEnumerable<INpcBehavior> behaviors)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("An NPC role cannot be empty.", nameof(role));
        }

        ArgumentNullException.ThrowIfNull(behaviors);

        Id = id;
        Role = role;
        _behaviors = behaviors.ToArray();
    }

    public EntityId Id { get; }

    public string Role { get; }

    public IWorldEvent? Evaluate(
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (var behavior in _behaviors)
        {
            var proposedEvent = behavior.Evaluate(Id, world, currentTime);

            if (proposedEvent is not null)
            {
                return proposedEvent;
            }
        }

        return null;
    }
}
