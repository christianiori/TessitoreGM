using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WorldSimulationDefinition(
    IReadOnlyList<NpcSimulationDefinition> Npcs,
    IReadOnlyList<EntityPresentationDefinition>? Entities = null,
    IReadOnlyList<LocationPresentationDefinition>? Locations = null,
    IReadOnlyList<ResourcePresentationDefinition>? Resources = null);

public sealed record EntityPresentationDefinition(
    EntityId EntityId,
    string Name);

public sealed record LocationPresentationDefinition(
    LocationId LocationId,
    string Name);

public sealed record ResourcePresentationDefinition(
    ResourceId ResourceId,
    string Name);

public sealed record NpcSimulationDefinition(
    EntityId EntityId,
    string Role,
    IReadOnlyList<ScheduledArrivalDefinition> ScheduledArrivals,
    IReadOnlyList<OrderProductionDefinition> OrderProductions,
    IReadOnlyList<DailyLocationRoutineDefinition>? DailyLocationRoutines = null,
    IReadOnlyList<BalanceConditionalLocationDefinition>?
        BalanceConditionalLocations = null,
    IReadOnlyList<KnowledgeConditionalLocationDefinition>?
        KnowledgeConditionalLocations = null,
    IReadOnlyList<FactId>? InitialKnownFacts = null,
    IReadOnlyList<FactSharingDefinition>? FactSharings = null,
    IReadOnlyList<TrustConditionalLocationDefinition>?
        TrustConditionalLocations = null,
    IReadOnlyList<TrustChangeAfterFactSharedDefinition>?
        TrustChangesAfterFactSharing = null,
    IReadOnlyList<TradeWhenColocatedDefinition>? TradesWhenColocated = null);

public sealed record DailyLocationRoutineDefinition(
    LocationId DestinationId,
    TimeSpan TimeOfDay);

public sealed record BalanceConditionalLocationDefinition(
    LocationId DestinationId,
    TimeSpan TimeOfDay,
    int MaximumBalance);

public sealed record KnowledgeConditionalLocationDefinition(
    LocationId DestinationId,
    TimeSpan TimeOfDay,
    FactId RequiredFactId);

public sealed record FactSharingDefinition(
    EntityId ListenerId,
    FactId FactId);

public sealed record TrustConditionalLocationDefinition(
    EntityId TrustedEntityId,
    LocationId DestinationId,
    TimeSpan TimeOfDay,
    int MinimumTrust);

public sealed record TrustChangeAfterFactSharedDefinition(
    EntityId SpeakerId,
    FactId FactId,
    int Amount,
    string Reason);

public sealed record TradeWhenColocatedDefinition(
    EntityId SellerId,
    ResourceId ResourceId,
    int Quantity,
    int TotalPrice);

public sealed record ScheduledArrivalDefinition(
    LocationId DestinationId,
    DateTimeOffset ScheduledAt);

public sealed record OrderProductionDefinition(
    OrderId OrderId,
    ItemId ProducedItemId,
    LocationId WorkLocationId,
    DateTimeOffset WorkStartsNotBefore,
    TimeSpan ProductionDuration);
