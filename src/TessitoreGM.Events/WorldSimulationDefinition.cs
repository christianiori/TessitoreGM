using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldSimulationDefinition(
    IReadOnlyList<NpcSimulationDefinition> Npcs);

public sealed record NpcSimulationDefinition(
    EntityId EntityId,
    string Role,
    IReadOnlyList<ScheduledArrivalDefinition> ScheduledArrivals,
    IReadOnlyList<OrderProductionDefinition> OrderProductions);

public sealed record ScheduledArrivalDefinition(
    LocationId DestinationId,
    DateTimeOffset ScheduledAt);

public sealed record OrderProductionDefinition(
    OrderId OrderId,
    ItemId ProducedItemId,
    LocationId WorkLocationId,
    DateTimeOffset WorkStartsNotBefore,
    TimeSpan ProductionDuration);
