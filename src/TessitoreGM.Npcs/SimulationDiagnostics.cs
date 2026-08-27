using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class SimulationDiagnostics
{
    public SimulationDiagnosticReport Analyze(
        WorldEventLog eventLog,
        WorldSnapshot world,
        TimeSpan horizon,
        IEnumerable<WorldRuleRegistration>? externalRules = null)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(world);
        if (horizon <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(horizon));
        }

        var simulation = eventLog.Simulation
            ?? throw new InvalidDataException(
                "Il salvataggio non contiene una configurazione della simulazione.");
        var registeredExternalRules = externalRules?.ToArray() ??
            Array.Empty<WorldRuleRegistration>();
        var result = new ConfiguredWorldSimulator().Advance(
            eventLog,
            world,
            world.CurrentTime.Add(horizon),
            registeredExternalRules);

        return new SimulationDiagnosticReport(
            world.CurrentTime,
            result.World.CurrentTime,
            simulation.Npcs.Count +
                (simulation.WeatherCycle is null ? 0 : 1) +
                registeredExternalRules.Length,
            simulation.Npcs.Sum(BehaviorCount),
            result.ProducedEvents);
    }

    private static int BehaviorCount(NpcSimulationDefinition npc) =>
        npc.ScheduledArrivals.Count +
        npc.OrderProductions.Count +
        (npc.DailyLocationRoutines?.Count ?? 0) +
        (npc.BalanceConditionalLocations?.Count ?? 0) +
        (npc.KnowledgeConditionalLocations?.Count ?? 0) +
        (npc.FactSharings?.Count ?? 0) +
        (npc.TrustConditionalLocations?.Count ?? 0) +
        (npc.TrustChangesAfterFactSharing?.Count ?? 0) +
        (npc.TradesWhenColocated?.Count ?? 0) +
        (npc.DailyNeedIncreases?.Count ?? 0) +
        (npc.ResourceConsumptions?.Count ?? 0) +
        (npc.DailyResourceProductions?.Count ?? 0);
}

public sealed record SimulationDiagnosticReport(
    DateTimeOffset From,
    DateTimeOffset Until,
    int RuleCount,
    int BehaviorCount,
    IReadOnlyList<IWorldEvent> ProposedEvents);
