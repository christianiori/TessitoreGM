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
