using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldSimulationDefinition(
    IReadOnlyList<NpcSimulationDefinition> Npcs,
    IReadOnlyList<EntityPresentationDefinition>? Entities = null,
    IReadOnlyList<LocationPresentationDefinition>? Locations = null);

public sealed record EntityPresentationDefinition(
    EntityId EntityId,
    string Name);

public sealed record LocationPresentationDefinition(
    LocationId LocationId,
    string Name);

public sealed record NpcSimulationDefinition(
    EntityId EntityId,
    string Role,
    IReadOnlyList<ScheduledArrivalDefinition> ScheduledArrivals,
    IReadOnlyList<OrderProductionDefinition> OrderProductions,
    IReadOnlyList<DailyLocationRoutineDefinition>? DailyLocationRoutines = null,
    IReadOnlyList<BalanceConditionalLocationDefinition>?
        BalanceConditionalLocations = null);

public sealed record DailyLocationRoutineDefinition(
    LocationId DestinationId,
    TimeSpan TimeOfDay);

public sealed record BalanceConditionalLocationDefinition(
    LocationId DestinationId,
    TimeSpan TimeOfDay,
    int MaximumBalance);

public sealed record ScheduledArrivalDefinition(
    LocationId DestinationId,
    DateTimeOffset ScheduledAt);

public sealed record OrderProductionDefinition(
    OrderId OrderId,
    ItemId ProducedItemId,
    LocationId WorkLocationId,
    DateTimeOffset WorkStartsNotBefore,
    TimeSpan ProductionDuration);
