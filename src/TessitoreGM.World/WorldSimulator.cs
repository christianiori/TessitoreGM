using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldSimulator
{
    private readonly IReadOnlyList<IWorldRule> _rules;
    private readonly WorldEventProcessor _processor;
    private readonly int _maxEvents;

    public WorldSimulator(IEnumerable<IWorldRule> rules, int maxEvents = 1000)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (maxEvents <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEvents),
                "Maximum event count must be greater than zero.");
        }

        _rules = rules.ToArray();
        _processor = new WorldEventProcessor();
        _maxEvents = maxEvents;
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

        var simulatedWorld = world;
        var producedEvents = new List<IWorldEvent>();
        IWorldEvent? previousEvent = null;

        while (true)
        {
            var proposals = _rules
                .Select((rule, index) => new
                {
                    Event = rule.ProposeNext(simulatedWorld, until),
                    RuleIndex = index
                })
                .Where(proposal => proposal.Event is not null)
                .ToArray();

            foreach (var proposal in proposals)
            {
                if (proposal.Event!.OccurredAt < simulatedWorld.CurrentTime ||
                    proposal.Event.OccurredAt > until)
                {
                    throw new InvalidOperationException(
                        $"Rule proposed event at '{proposal.Event.OccurredAt:O}' " +
                        $"outside simulation interval " +
                        $"'{simulatedWorld.CurrentTime:O}' to '{until:O}'.");
                }
            }

            var selected = proposals
                .OrderBy(proposal => proposal.Event!.OccurredAt)
                .ThenBy(proposal => proposal.RuleIndex)
                .FirstOrDefault();

            if (selected is null)
            {
                break;
            }

            if (producedEvents.Count >= _maxEvents)
            {
                throw new InvalidOperationException(
                    $"Simulation exceeded the maximum of {_maxEvents} events.");
            }

            var selectedEvent = selected.Event!;

            if (Equals(selectedEvent, previousEvent))
            {
                throw new InvalidOperationException(
                    $"Rule repeatedly proposed the same event " +
                    $"'{selectedEvent.GetType().Name}' at " +
                    $"'{selectedEvent.OccurredAt:O}'.");
            }

            simulatedWorld = _processor.Apply(simulatedWorld, selectedEvent);
            producedEvents.Add(selectedEvent);
            previousEvent = selectedEvent;
        }

        if (simulatedWorld.CurrentTime < until)
        {
            var timeAdvanced = new WorldTimeAdvanced(until);
            simulatedWorld = _processor.Apply(simulatedWorld, timeAdvanced);
            producedEvents.Add(timeAdvanced);
        }

        return new SimulationResult(simulatedWorld, producedEvents);
    }
}
