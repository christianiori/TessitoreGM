using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed class WorldDefinitionEditor
{
    public WorldEventLog AddLocation(
        WorldEventLog eventLog,
        string id,
        string name)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var normalizedId = ValidId(id, "del luogo");
        var normalizedName = ValidName(name, "del luogo");
        var simulation = Simulation(eventLog);
        var locations = simulation.Locations?.ToList() ?? new();
        EnsureUnique(
            normalizedId,
            normalizedName,
            locations.Select(location =>
                (location.LocationId.ToString(), location.Name)),
            "un luogo");
        locations.Add(new LocationPresentationDefinition(
            new LocationId(normalizedId),
            normalizedName));
        return eventLog with
        {
            Simulation = simulation with { Locations = locations }
        };
    }

    public WorldEventLog AddResource(
        WorldEventLog eventLog,
        string id,
        string name)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var normalizedId = ValidId(id, "della risorsa");
        var normalizedName = ValidName(name, "della risorsa");
        var simulation = Simulation(eventLog);
        var resources = simulation.Resources?.ToList() ?? new();
        EnsureUnique(
            normalizedId,
            normalizedName,
            resources.Select(resource =>
                (resource.ResourceId.ToString(), resource.Name)),
            "una risorsa");
        resources.Add(new ResourcePresentationDefinition(
            new ResourceId(normalizedId),
            normalizedName));
        return eventLog with
        {
            Simulation = simulation with { Resources = resources }
        };
    }

    public WorldEventLog AddNeed(WorldEventLog eventLog, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var normalizedId = ValidId(id, "del bisogno");
        var normalizedName = ValidName(name, "del bisogno");
        var simulation = Simulation(eventLog);
        var needs = simulation.Needs?.ToList() ?? new();
        EnsureUnique(
            normalizedId,
            normalizedName,
            needs.Select(need => (need.NeedId.ToString(), need.Name)),
            "un bisogno");
        needs.Add(new NeedPresentationDefinition(
            new NeedId(normalizedId), normalizedName));
        return eventLog with { Simulation = simulation with { Needs = needs } };
    }

    public WorldEventLog AddNpc(
        WorldEventLog eventLog,
        string id,
        string name,
        string role,
        string initialLocationId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var normalizedId = ValidId(id, "del personaggio");
        var normalizedName = ValidName(name, "del personaggio");
        var normalizedRole = ValidName(role, "del ruolo");
        var normalizedLocationId = ValidId(
            initialLocationId,
            "del luogo iniziale");
        var simulation = Simulation(eventLog);
        var location = (simulation.Locations ??
                Array.Empty<LocationPresentationDefinition>())
            .FirstOrDefault(candidate => candidate.LocationId.ToString().Equals(
                normalizedLocationId,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                "Il luogo iniziale selezionato non esiste.");
        var entities = simulation.Entities?.ToList() ?? new();
        var npcs = simulation.Npcs.ToList();
        var usedIds = entities.Select(entity => entity.EntityId.ToString())
            .Concat(npcs.Select(npc => npc.EntityId.ToString()))
            .Concat(eventLog.InitialWorld.Balances.Select(balance =>
                balance.EntityId.ToString()))
            .Concat((eventLog.InitialWorld.ResourceStocks ??
                Array.Empty<EntityResourceStock>()).Select(stock =>
                stock.EntityId.ToString()))
            .Concat(eventLog.Events.OfType<PlayerCharacterRegistered>().Select(
                player => player.EntityId.ToString()));
        if (usedIds.Any(existing => existing.Equals(
            normalizedId,
            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già un personaggio con identificatore '{normalizedId}'.");
        }

        if (entities.Any(entity => entity.Name.Equals(
            normalizedName,
            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già un personaggio chiamato '{normalizedName}'.");
        }

        var entityId = new EntityId(normalizedId);
        entities.Add(new EntityPresentationDefinition(entityId, normalizedName));
        npcs.Add(new NpcSimulationDefinition(
            entityId,
            normalizedRole,
            Array.Empty<ScheduledArrivalDefinition>(),
            Array.Empty<OrderProductionDefinition>()));
        return eventLog with
        {
            Simulation = simulation with
            {
                Entities = entities,
                Npcs = npcs
            },
            Events = eventLog.Events.Concat(new IWorldEvent[]
            {
                new EntityEnteredLocation(
                    entityId,
                    location.LocationId,
                    occurredAt)
            }).ToArray()
        };
    }

    public WorldEventLog AddNpcDailyRoutine(
        WorldEventLog eventLog,
        string npcId,
        string destinationId,
        TimeSpan timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeOfDay),
                "L'orario della routine deve appartenere alla giornata.");
        }

        var normalizedNpcId = ValidId(npcId, "del personaggio");
        var normalizedDestinationId = ValidId(
            destinationId,
            "del luogo di destinazione");
        var simulation = eventLog.Simulation
            ?? throw new InvalidOperationException(
                "La campagna non contiene ancora una simulazione.");
        var npcs = simulation.Npcs.ToArray();
        var npcIndex = Array.FindIndex(npcs, npc =>
            npc.EntityId.ToString().Equals(
                normalizedNpcId,
                StringComparison.OrdinalIgnoreCase));
        if (npcIndex < 0)
        {
            throw new ArgumentException("Il personaggio selezionato non esiste.");
        }

        var destination = (simulation.Locations ??
                Array.Empty<LocationPresentationDefinition>())
            .FirstOrDefault(location => location.LocationId.ToString().Equals(
                normalizedDestinationId,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                "Il luogo di destinazione selezionato non esiste.");
        var routines = npcs[npcIndex].DailyLocationRoutines?.ToList() ?? new();
        if (routines.Any(routine => routine.TimeOfDay == timeOfDay))
        {
            throw new InvalidOperationException(
                "Il personaggio ha già una routine configurata a questo orario.");
        }

        routines.Add(new DailyLocationRoutineDefinition(
            destination.LocationId,
            timeOfDay));
        routines.Sort((left, right) => left.TimeOfDay.CompareTo(right.TimeOfDay));
        npcs[npcIndex] = npcs[npcIndex] with
        {
            DailyLocationRoutines = routines
        };
        return eventLog with
        {
            Simulation = simulation with { Npcs = npcs }
        };
    }

    public WorldEventLog ConfigureNpcInitialState(
        WorldEventLog eventLog,
        string npcId,
        int coins,
        string? resourceId,
        int resourceQuantity,
        string? needId,
        int needLevel,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        if (coins < 0 || resourceQuantity < 0 || needLevel is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coins), "Monete e scorte non possono essere negative; il bisogno deve essere tra 0 e 100.");
        }

        var simulation = Simulation(eventLog);
        var entityId = ExistingNpc(simulation, npcId).EntityId;
        if (HasStateHistory(eventLog.Events, entityId))
        {
            throw new InvalidOperationException(
                "Il personaggio possiede già una cronologia economica o dei bisogni. Lo stato iniziale non può più essere modificato.");
        }

        var balances = eventLog.InitialWorld.Balances
            .Where(balance => balance.EntityId != entityId).ToList();
        balances.Add(new EntityBalance(entityId, coins));
        var stocks = (eventLog.InitialWorld.ResourceStocks ??
            Array.Empty<EntityResourceStock>()).ToList();
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            var resource = ExistingResource(simulation, resourceId);
            stocks.RemoveAll(stock => stock.EntityId == entityId &&
                stock.ResourceId == resource.ResourceId);
            stocks.Add(new EntityResourceStock(
                entityId, resource.ResourceId, resourceQuantity));
        }

        var events = eventLog.Events.ToList();
        if (!string.IsNullOrWhiteSpace(needId))
        {
            var need = ExistingNeed(simulation, needId);
            if (events.OfType<NeedIncreased>().Any(item =>
                item.EntityId == entityId && item.NeedId == need.NeedId) ||
                events.OfType<ResourceConsumed>().Any(item =>
                    item.EntityId == entityId && item.NeedId == need.NeedId))
            {
                throw new InvalidOperationException(
                    "Il bisogno selezionato ha già una cronologia e non può essere reimpostato dall'editor.");
            }

            if (needLevel > 0)
            {
                events.Add(new NeedIncreased(
                    entityId, need.NeedId, needLevel, occurredAt));
            }
        }

        return eventLog with
        {
            InitialWorld = eventLog.InitialWorld with
            {
                Balances = balances,
                ResourceStocks = stocks
            },
            Events = events
        };
    }

    public WorldEventLog AddNpcInitialKnowledge(
        WorldEventLog eventLog,
        string npcId,
        string factId)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var simulation = Simulation(eventLog);
        var npcs = simulation.Npcs.ToArray();
        var npc = ExistingNpc(simulation, npcId);
        var index = Array.IndexOf(npcs, npc);
        var fact = new FactId(ValidId(factId, "del fatto"));
        var facts = npc.InitialKnownFacts?.ToList() ?? new();
        if (facts.Contains(fact))
        {
            throw new InvalidOperationException(
                "Il personaggio possiede già questa conoscenza iniziale.");
        }

        facts.Add(fact);
        npcs[index] = npc with { InitialKnownFacts = facts };
        return eventLog with
        {
            Simulation = simulation with { Npcs = npcs }
        };
    }

    public WorldEventLog UpdateNpcProfile(
        WorldEventLog eventLog,
        string npcId,
        string name,
        string role)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        var simulation = Simulation(eventLog);
        var npc = ExistingNpc(simulation, npcId);
        var normalizedName = ValidName(name, "del personaggio");
        var normalizedRole = ValidName(role, "del ruolo");
        var entities = simulation.Entities?.ToArray() ??
            Array.Empty<EntityPresentationDefinition>();
        var entityIndex = Array.FindIndex(entities, entity =>
            entity.EntityId == npc.EntityId);
        if (entityIndex < 0)
        {
            throw new InvalidOperationException(
                "Il personaggio non possiede una presentazione modificabile.");
        }

        if (entities.Any(entity => entity.EntityId != npc.EntityId &&
            entity.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già un personaggio chiamato '{normalizedName}'.");
        }

        entities[entityIndex] = entities[entityIndex] with { Name = normalizedName };
        var npcs = simulation.Npcs.ToArray();
        npcs[Array.IndexOf(npcs, npc)] = npc with { Role = normalizedRole };
        return eventLog with
        {
            Simulation = simulation with { Entities = entities, Npcs = npcs }
        };
    }

    public WorldEventLog UpdateNpcDailyRoutine(
        WorldEventLog eventLog,
        string npcId,
        TimeSpan previousTime,
        string destinationId,
        TimeSpan newTime)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        if (newTime < TimeSpan.Zero || newTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(newTime));
        }

        var simulation = Simulation(eventLog);
        var npc = ExistingNpc(simulation, npcId);
        var destination = ExistingLocation(simulation, destinationId);
        var routines = npc.DailyLocationRoutines?.ToList() ?? new();
        var index = routines.FindIndex(routine => routine.TimeOfDay == previousTime);
        if (index < 0)
        {
            throw new ArgumentException("La routine selezionata non esiste.");
        }

        if (routines.Where((_, candidate) => candidate != index)
            .Any(routine => routine.TimeOfDay == newTime))
        {
            throw new InvalidOperationException(
                "Il personaggio ha già una routine configurata a questo orario.");
        }

        routines[index] = new DailyLocationRoutineDefinition(
            destination.LocationId, newTime);
        routines.Sort((left, right) => left.TimeOfDay.CompareTo(right.TimeOfDay));
        var npcs = simulation.Npcs.ToArray();
        npcs[Array.IndexOf(npcs, npc)] = npc with { DailyLocationRoutines = routines };
        return eventLog with { Simulation = simulation with { Npcs = npcs } };
    }

    private static NpcSimulationDefinition ExistingNpc(
        WorldSimulationDefinition simulation,
        string id)
    {
        var normalized = ValidId(id, "del personaggio");
        return simulation.Npcs.FirstOrDefault(npc => npc.EntityId.ToString()
            .Equals(normalized, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException("Il personaggio selezionato non esiste.");
    }

    private static LocationPresentationDefinition ExistingLocation(
        WorldSimulationDefinition simulation,
        string id)
    {
        var normalized = ValidId(id, "del luogo");
        return (simulation.Locations ?? Array.Empty<LocationPresentationDefinition>())
            .FirstOrDefault(item => item.LocationId.ToString().Equals(
                normalized, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException("Il luogo selezionato non esiste.");
    }

    private static ResourcePresentationDefinition ExistingResource(
        WorldSimulationDefinition simulation,
        string id)
    {
        var normalized = ValidId(id, "della risorsa");
        return (simulation.Resources ?? Array.Empty<ResourcePresentationDefinition>())
            .FirstOrDefault(item => item.ResourceId.ToString().Equals(
                normalized, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException("La risorsa selezionata non esiste.");
    }

    private static NeedPresentationDefinition ExistingNeed(
        WorldSimulationDefinition simulation,
        string id)
    {
        var normalized = ValidId(id, "del bisogno");
        return (simulation.Needs ?? Array.Empty<NeedPresentationDefinition>())
            .FirstOrDefault(item => item.NeedId.ToString().Equals(
                normalized, StringComparison.OrdinalIgnoreCase)) ??
            throw new ArgumentException("Il bisogno selezionato non esiste.");
    }

    private static bool HasStateHistory(
        IEnumerable<IWorldEvent> events,
        EntityId entityId) => events.Any(worldEvent => worldEvent switch
        {
            CoinsTransferred item => item.PayerId == entityId || item.PayeeId == entityId,
            ResourceAcquired item => item.EntityId == entityId,
            ResourceProduced item => item.ProducerId == entityId,
            ResourceTransferred item => item.SourceId == entityId || item.DestinationId == entityId,
            ResourceConsumed item => item.EntityId == entityId,
            ResourceLost item => item.EntityId == entityId,
            TradeCompleted item => item.BuyerId == entityId || item.SellerId == entityId,
            NeedIncreased item => item.EntityId == entityId,
            _ => false
        });

    private static WorldSimulationDefinition Simulation(WorldEventLog eventLog) =>
        eventLog.Simulation ?? new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>());

    private static string ValidId(string value, string kind)
    {
        var id = value?.Trim() ?? string.Empty;
        if (id.Length is < 1 or > 64 || id.Any(character =>
            !char.IsAsciiLetterOrDigit(character) &&
            character is not '-' and not '_'))
        {
            throw new ArgumentException(
                $"L'identificatore {kind} deve contenere da 1 a 64 caratteri: lettere non accentate, numeri, trattino o trattino basso.");
        }

        return id.ToLowerInvariant();
    }

    private static string ValidName(string value, string kind)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100)
        {
            throw new ArgumentException(
                $"Il nome {kind} deve contenere da 1 a 100 caratteri.");
        }

        return name;
    }

    private static void EnsureUnique(
        string id,
        string name,
        IEnumerable<(string Id, string Name)> existing,
        string kind)
    {
        if (existing.Any(item =>
            item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già {kind} con identificatore '{id}'.");
        }

        if (existing.Any(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già {kind} chiamata '{name}'.");
        }
    }
}
