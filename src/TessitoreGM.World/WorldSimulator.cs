using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldSimulator
{
    private readonly IReadOnlyList<IWorldRule> _rules;
    private readonly WorldEventProcessor _processor;

    public WorldSimulator(IEnumerable<IWorldRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules.ToArray();
        _processor = new WorldEventProcessor();
    }

    public SimulationResult Advance(
        WorldSnapshot world,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (currentTime < world.CurrentTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentTime),
                "Simulation time cannot precede world time.");
        }

        var producedEvents = new List<IWorldEvent>();

        foreach (var rule in _rules)
        {
            var proposedEvent = rule.Evaluate(world, currentTime);

            if (proposedEvent is null)
            {
                continue;
            }

            world = _processor.Apply(world, proposedEvent);
            producedEvents.Add(proposedEvent);
        }

        return new SimulationResult(world, producedEvents);
    }
}
