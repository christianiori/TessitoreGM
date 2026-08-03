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
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (until < world.CurrentTime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(until),
                "Simulation time cannot precede world time.");
        }

        var proposals = _rules
            .Select((rule, index) => new
            {
                Event = rule.ProposeNext(world, until),
                RuleIndex = index
            })
            .Where(proposal => proposal.Event is not null)
            .ToArray();

        foreach (var proposal in proposals)
        {
            if (proposal.Event!.OccurredAt < world.CurrentTime ||
                proposal.Event.OccurredAt > until)
            {
                throw new InvalidOperationException(
                    $"Rule proposed event at '{proposal.Event.OccurredAt:O}' " +
                    $"outside simulation interval " +
                    $"'{world.CurrentTime:O}' to '{until:O}'.");
            }
        }

        var selected = proposals
            .OrderBy(proposal => proposal.Event!.OccurredAt)
            .ThenBy(proposal => proposal.RuleIndex)
            .FirstOrDefault();

        if (selected is null)
        {
            return new SimulationResult(world, Array.Empty<IWorldEvent>());
        }

        var selectedEvent = selected.Event!;
        var updatedWorld = _processor.Apply(world, selectedEvent);
        return new SimulationResult(
            updatedWorld,
            new IWorldEvent[] { selectedEvent });
    }
}
