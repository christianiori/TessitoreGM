using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Narration;
using TessitoreGM.World;

namespace TessitoreGM.AiGm;

/// <summary>
/// Rebuilds the AI GM dossier from the canonical event log on every turn.
/// No conversational memory held by an AI provider is trusted as game state.
/// </summary>
public sealed class AiGmContextBuilder
{
    private readonly WorldEventProcessor _processor = new();
    private readonly MemoryEngine _memoryEngine = new();
    private readonly DeterministicNarrator _narrator = new();
    private readonly AiGmContextBuilderOptions _options;

    public AiGmContextBuilder(AiGmContextBuilderOptions? options = null)
    {
        _options = options ?? new AiGmContextBuilderOptions();
        if (_options.MaximumCanonicalEvents <= 0 ||
            _options.MaximumObservedEventsPerActor <= 0 ||
            _options.MaximumSceneExchanges <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "AI GM memory windows must be positive.");
        }
    }

    public AiGmTurnContext Build(
        WorldEventLog eventLog,
        PlayerActionProposal playerAction)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(playerAction);

        var persistedAction = (eventLog.PlayerActions ?? [])
            .SingleOrDefault(action => action.Id == playerAction.Id);
        if (persistedAction is null)
        {
            throw new InvalidOperationException(
                "The AI GM can only answer a persisted human player action.");
        }

        var players = eventLog.Events
            .OfType<PlayerCharacterRegistered>()
            .GroupBy(player => player.EntityId)
            .Select(group => group.Last())
            .ToArray();
        var player = players.SingleOrDefault(
            candidate => candidate.EntityId == persistedAction.PlayerCharacterId)
            ?? throw new InvalidOperationException(
                "The player action does not belong to a registered character.");

        var initialWorld = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            eventLog.InitialWorld.Balances.ToDictionary(
                balance => balance.EntityId,
                balance => balance.Amount),
            eventLog.InitialWorld.ResourceStocks ?? [],
            eventLog.InitialWorld.Weather);
        var world = _processor.Replay(initialWorld, eventLog.Events);

        var names = EntityNames(eventLog, players);
        var locationNames = (eventLog.Simulation?.Locations ?? [])
            .ToDictionary(location => location.LocationId, location => location.Name);
        var resourceNames = (eventLog.Simulation?.Resources ?? [])
            .ToDictionary(resource => resource.ResourceId, resource => resource.Name);
        var needNames = (eventLog.Simulation?.Needs ?? [])
            .ToDictionary(need => need.NeedId, need => need.Name);
        var narrationContext = new NarrationContext(
            names,
            locationNames,
            resourceNames,
            needNames);

        var allActorIds = players.Select(candidate => candidate.EntityId)
            .Concat(eventLog.Simulation?.Npcs.Select(npc => npc.EntityId) ?? [])
            .Distinct()
            .ToArray();
        var playerLocation = world.GetLocation(player.EntityId);
        var actorIds = allActorIds.Where(actorId =>
                actorId == player.EntityId ||
                (playerLocation is not null &&
                    world.GetLocation(actorId) == playerLocation))
            .ToArray();
        var humanIds = players.Select(candidate => candidate.EntityId).ToHashSet();
        var resources = KnownResources(eventLog);

        var actors = actorIds.Select(actorId =>
        {
            var locationId = world.GetLocation(actorId);
            return new AiGmActorState(
                actorId,
                names.GetValueOrDefault(actorId, actorId.ToString()),
                humanIds.Contains(actorId),
                Role(eventLog, actorId, humanIds.Contains(actorId)),
                locationId,
                locationId is { } id
                    ? locationNames.GetValueOrDefault(id, id.ToString())
                    : null,
                world.GetBalance(actorId),
                resources.Select(resourceId => new AiGmResourceState(
                    resourceId,
                    resourceNames.GetValueOrDefault(
                        resourceId,
                        resourceId.ToString()),
                    world.GetResourceQuantity(actorId, resourceId)))
                    .Where(resource => resource.Quantity > 0)
                    .ToArray());
        }).ToArray();

        var canonicalChronicle = Remember(
            eventLog.Events.TakeLast(_options.MaximumCanonicalEvents),
            narrationContext);
        var actorMemories = actorIds.Select(actorId =>
        {
            var memory = _memoryEngine.Replay(actorId, initialWorld, eventLog.Events);
            return new AiGmActorMemory(
                actorId,
                names.GetValueOrDefault(actorId, actorId.ToString()),
                memory.KnownFacts.OrderBy(fact => fact.Value).ToArray(),
                Remember(
                    memory.ObservedEvents.TakeLast(
                        _options.MaximumObservedEventsPerActor),
                    narrationContext));
        }).ToArray();
        var actionsById = (eventLog.PlayerActions ?? [])
            .GroupBy(action => action.Id)
            .ToDictionary(group => group.Key, group => group.Last());
        var sceneHistory = (eventLog.AiGmTurns ?? [])
            .Where(turn =>
                turn.PlayerCharacterId == player.EntityId &&
                turn.Status == AiGmTurnStatus.Completed &&
                turn.Consequences.All(consequence =>
                    consequence.Status == AiGmConsequenceStatus.Applied) &&
                (turn.SceneLocationId is null ||
                    turn.SceneLocationId == playerLocation) &&
                actionsById.ContainsKey(turn.PlayerActionId))
            .OrderBy(turn => turn.CreatedAt)
            .TakeLast(_options.MaximumSceneExchanges)
            .Select(turn => new AiGmSceneExchange(
                turn.PlayerActionId,
                turn.CreatedAt,
                actionsById[turn.PlayerActionId].Description,
                turn.Narration))
            .ToArray();
        var catalogActorIds = allActorIds
            .Concat(eventLog.Simulation?.Entities?.Select(
                entity => entity.EntityId) ?? [])
            .Concat(eventLog.InitialWorld.Balances
                .Select(balance => balance.EntityId))
            .Distinct()
            .ToArray();
        var catalog = BuildCatalog(
            eventLog,
            world,
            catalogActorIds,
            humanIds,
            names,
            locationNames,
            resourceNames,
            needNames,
            actorMemories);

        return new AiGmTurnContext(
            persistedAction.Id,
            persistedAction.PlayerCharacterId,
            player.Name,
            persistedAction.Description,
            new AiGmWorldState(world.CurrentTime, world.Weather, actors),
            new AiGmMemoryDossier(
                canonicalChronicle,
                actorMemories,
                sceneHistory),
            AiGmInvariants.Rules,
            catalog);
    }

    private IReadOnlyList<AiGmRememberedEvent> Remember(
        IEnumerable<IWorldEvent> events,
        NarrationContext context)
    {
        var orderedEvents = events.OrderBy(worldEvent => worldEvent.OccurredAt)
            .ToArray();
        var lines = _narrator.Narrate(orderedEvents, context);

        return orderedEvents.Zip(lines, (worldEvent, line) =>
            new AiGmRememberedEvent(
                worldEvent.OccurredAt,
                worldEvent.GetType().Name,
                line.Text)).ToArray();
    }

    private static Dictionary<EntityId, string> EntityNames(
        WorldEventLog eventLog,
        IEnumerable<PlayerCharacterRegistered> players) =>
        (eventLog.Simulation?.Entities ?? [])
            .ToDictionary(entity => entity.EntityId, entity => entity.Name)
            .Concat(players.Select(player =>
                new KeyValuePair<EntityId, string>(player.EntityId, player.Name)))
            .GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);

    private static string Role(
        WorldEventLog eventLog,
        EntityId actorId,
        bool isHumanPlayer) =>
        isHumanPlayer
            ? "Giocatore umano"
            : eventLog.Simulation?.Npcs
                .FirstOrDefault(npc => npc.EntityId == actorId)?.Role ?? "PNG";

    private static IReadOnlyList<ResourceId> KnownResources(
        WorldEventLog eventLog) =>
        (eventLog.Simulation?.Resources?.Select(resource => resource.ResourceId) ?? [])
            .Concat((eventLog.InitialWorld.ResourceStocks ?? [])
                .Select(stock => stock.ResourceId))
            .Distinct()
            .ToArray();

    private static AiGmCampaignCatalog BuildCatalog(
        WorldEventLog eventLog,
        WorldSnapshot world,
        IReadOnlyList<EntityId> actorIds,
        IReadOnlySet<EntityId> humanIds,
        IReadOnlyDictionary<EntityId, string> names,
        IReadOnlyDictionary<LocationId, string> locationNames,
        IReadOnlyDictionary<ResourceId, string> resourceNames,
        IReadOnlyDictionary<NeedId, string> needNames,
        IReadOnlyList<AiGmActorMemory> actorMemories)
    {
        var locations = (eventLog.Simulation?.Locations?.Select(
                location => location.LocationId) ?? [])
            .Concat(eventLog.Events.OfType<EntityEnteredLocation>()
                .Select(entered => entered.LocationId))
            .Distinct()
            .OrderBy(locationId => locationId.Value)
            .Select(locationId => new AiGmCatalogLocation(
                locationId,
                locationNames.GetValueOrDefault(
                    locationId,
                    locationId.ToString())))
            .ToArray();
        var resources = KnownResources(eventLog)
            .OrderBy(resourceId => resourceId.Value)
            .Select(resourceId => new AiGmCatalogResource(
                resourceId,
                resourceNames.GetValueOrDefault(
                    resourceId,
                    resourceId.ToString())))
            .ToArray();
        var needs = (eventLog.Simulation?.Needs?.Select(
                need => need.NeedId) ?? [])
            .Concat(eventLog.Events.SelectMany(worldEvent => worldEvent switch
            {
                NeedIncreased increased => new[] { increased.NeedId },
                ResourceConsumed consumed => new[] { consumed.NeedId },
                _ => Array.Empty<NeedId>()
            }))
            .Distinct()
            .OrderBy(needId => needId.Value)
            .Select(needId => new AiGmCatalogNeed(
                needId,
                needNames.GetValueOrDefault(needId, needId.ToString())))
            .ToArray();
        var facts = KnownFacts(eventLog, actorMemories)
            .OrderBy(factId => factId.Value)
            .ToArray();
        var characters = actorIds
            .OrderBy(actorId => names.GetValueOrDefault(
                actorId,
                actorId.ToString()), StringComparer.OrdinalIgnoreCase)
            .Select(actorId =>
            {
                var locationId = world.GetLocation(actorId);
                return new AiGmCatalogCharacter(
                    actorId,
                    names.GetValueOrDefault(actorId, actorId.ToString()),
                    humanIds.Contains(actorId),
                    Role(eventLog, actorId, humanIds.Contains(actorId)),
                    locationId,
                    locationId is { } value
                        ? locationNames.GetValueOrDefault(
                            value,
                            value.ToString())
                        : null);
            })
            .ToArray();

        return new AiGmCampaignCatalog(
            characters,
            locations,
            resources,
            needs,
            facts);
    }

    private static IReadOnlySet<FactId> KnownFacts(
        WorldEventLog eventLog,
        IReadOnlyList<AiGmActorMemory> actorMemories)
    {
        var configured = (eventLog.Simulation?.Npcs ?? [])
            .SelectMany(npc =>
                (npc.InitialKnownFacts ?? [])
                    .Concat((npc.KnowledgeConditionalLocations ?? [])
                        .Select(rule => rule.RequiredFactId))
                    .Concat((npc.FactSharings ?? [])
                        .Select(rule => rule.FactId))
                    .Concat((npc.TrustChangesAfterFactSharing ?? [])
                        .Select(rule => rule.FactId)));
        var persisted = eventLog.Events.SelectMany(worldEvent => worldEvent switch
        {
            FactShared shared => new[] { shared.FactId },
            FactRevealed revealed => new[] { revealed.FactId },
            _ => Array.Empty<FactId>()
        });

        return configured
            .Concat(persisted)
            .Concat(actorMemories.SelectMany(memory => memory.KnownFacts))
            .ToHashSet();
    }
}

public sealed record AiGmContextBuilderOptions(
    int MaximumCanonicalEvents = 80,
    int MaximumObservedEventsPerActor = 40,
    int MaximumSceneExchanges = 12);
