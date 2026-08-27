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
        var normalizedId = ValidId(id, "luogo");
        var normalizedName = ValidName(name, "luogo");
        var simulation = Simulation(eventLog);
        var locations = simulation.Locations?.ToList() ?? new();
        EnsureUnique(
            normalizedId,
            normalizedName,
            locations.Select(location =>
                (location.LocationId.ToString(), location.Name)),
            "luogo");
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
        var normalizedId = ValidId(id, "risorsa");
        var normalizedName = ValidName(name, "risorsa");
        var simulation = Simulation(eventLog);
        var resources = simulation.Resources?.ToList() ?? new();
        EnsureUnique(
            normalizedId,
            normalizedName,
            resources.Select(resource =>
                (resource.ResourceId.ToString(), resource.Name)),
            "risorsa");
        resources.Add(new ResourcePresentationDefinition(
            new ResourceId(normalizedId),
            normalizedName));
        return eventLog with
        {
            Simulation = simulation with { Resources = resources }
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
                $"L'identificatore della {kind} deve contenere da 1 a 64 caratteri: lettere non accentate, numeri, trattino o trattino basso.");
        }

        return id.ToLowerInvariant();
    }

    private static string ValidName(string value, string kind)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100)
        {
            throw new ArgumentException(
                $"Il nome della {kind} deve contenere da 1 a 100 caratteri.");
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
                $"Esiste già una {kind} con identificatore '{id}'.");
        }

        if (existing.Any(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Esiste già una {kind} chiamata '{name}'.");
        }
    }
}
