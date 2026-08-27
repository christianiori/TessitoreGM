using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.World;

namespace TessitoreGM.Npcs;

public sealed class ConfiguredWorldSimulator
{
    public SimulationResult Advance(
        WorldEventLog eventLog,
        WorldSnapshot world,
        DateTimeOffset until,
        IEnumerable<WorldRuleRegistration>? externalRules = null)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(world);

        var simulation = eventLog.Simulation
            ?? throw new InvalidDataException(
                "The event log contains no simulation definition.");
        var balances = eventLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            balances,
            eventLog.InitialWorld.ResourceStocks ??
                Array.Empty<EntityResourceStock>(),
            eventLog.InitialWorld.Weather);
        var memoryEngine = new MemoryEngine();
        var rules = new WorldRuleRegistry();
        if (simulation.WeatherCycle is { } weatherCycle)
        {
            rules.Register("world:weather-cycle", new DailyWeatherCycleRule(
                weatherCycle.TimeOfDay,
                weatherCycle.Conditions));
        }

        foreach (var npc in simulation.Npcs)
        {
            var memory = memoryEngine.Replay(
                npc.EntityId,
                initialWorld,
                eventLog.Events);
            foreach (var factId in npc.InitialKnownFacts ??
                Array.Empty<FactId>())
            {
                memory = memory.LearnFact(factId);
            }

            rules.Register($"npc:{npc.EntityId}", new NpcAgent(
                npc.EntityId,
                npc.Role,
                memory,
                CreateBehaviors(npc)));
        }

        foreach (var registration in externalRules ??
            Array.Empty<WorldRuleRegistration>())
        {
            rules.Register(registration.Id, registration.Rule);
        }

        return rules.CreateSimulator().Advance(world, until);
    }

    private static IReadOnlyList<INpcBehavior> CreateBehaviors(
        NpcSimulationDefinition npc) =>
        npc.ScheduledArrivals
            .Select(arrival => (INpcBehavior)new ScheduledArrivalBehavior(
                arrival.DestinationId,
                arrival.ScheduledAt))
            .Concat((npc.DailyLocationRoutines ??
                Array.Empty<DailyLocationRoutineDefinition>())
                .Select(routine =>
                    (INpcBehavior)new DailyLocationRoutineBehavior(
                        routine.DestinationId,
                        routine.TimeOfDay)))
            .Concat((npc.BalanceConditionalLocations ??
                Array.Empty<BalanceConditionalLocationDefinition>())
                .Select(decision =>
                    (INpcBehavior)new BalanceConditionalLocationBehavior(
                        decision.DestinationId,
                        decision.TimeOfDay,
                        decision.MaximumBalance)))
            .Concat((npc.KnowledgeConditionalLocations ??
                Array.Empty<KnowledgeConditionalLocationDefinition>())
                .Select(decision =>
                    (INpcBehavior)new KnowledgeConditionalLocationBehavior(
                        decision.DestinationId,
                        decision.TimeOfDay,
                        decision.RequiredFactId)))
            .Concat((npc.FactSharings ?? Array.Empty<FactSharingDefinition>())
                .Select(sharing =>
                    (INpcBehavior)new ShareFactWhenColocatedBehavior(
                        sharing.ListenerId,
                        sharing.FactId)))
            .Concat((npc.TrustConditionalLocations ??
                Array.Empty<TrustConditionalLocationDefinition>())
                .Select(decision =>
                    (INpcBehavior)new TrustConditionalLocationBehavior(
                        decision.TrustedEntityId,
                        decision.DestinationId,
                        decision.TimeOfDay,
                        decision.MinimumTrust)))
            .Concat((npc.TrustChangesAfterFactSharing ??
                Array.Empty<TrustChangeAfterFactSharedDefinition>())
                .Select(change =>
                    (INpcBehavior)new ChangeTrustAfterFactSharedBehavior(
                        change.SpeakerId,
                        change.FactId,
                        change.Amount,
                        change.Reason)))
            .Concat((npc.TradesWhenColocated ??
                Array.Empty<TradeWhenColocatedDefinition>())
                .Select(trade =>
                    (INpcBehavior)new TradeWhenColocatedBehavior(
                        trade.SellerId,
                        trade.ResourceId,
                        trade.Quantity,
                        trade.TotalPrice)))
            .Concat((npc.DailyNeedIncreases ??
                Array.Empty<DailyNeedIncreaseDefinition>())
                .Select(increase =>
                    (INpcBehavior)new DailyNeedIncreaseBehavior(
                        increase.NeedId,
                        increase.TimeOfDay,
                        increase.Amount)))
            .Concat((npc.ResourceConsumptions ??
                Array.Empty<ResourceConsumptionDefinition>())
                .Select(consumption =>
                    (INpcBehavior)new ConsumeResourceForNeedBehavior(
                        consumption.NeedId,
                        consumption.ResourceId,
                        consumption.MinimumNeed,
                        consumption.Quantity,
                        consumption.Relief)))
            .Concat((npc.DailyResourceProductions ??
                Array.Empty<DailyResourceProductionDefinition>())
                .Select(production =>
                    (INpcBehavior)new DailyResourceProductionBehavior(
                        production.ResourceId,
                        production.LocationId,
                        production.TimeOfDay,
                        production.TargetStock)))
            .Concat(npc.OrderProductions.Select(production =>
                (INpcBehavior)new OrderProductionBehavior(
                    production.OrderId,
                    production.ProducedItemId,
                    production.WorkLocationId,
                    production.WorkStartsNotBefore,
                    production.ProductionDuration)))
            .ToArray();
}
