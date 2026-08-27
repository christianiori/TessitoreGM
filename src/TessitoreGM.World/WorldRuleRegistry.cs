namespace TessitoreGM.World;

/// <summary>
/// Collects world rules in a deterministic order and creates a simulator that
/// validates every proposed event through TessitoreGM's event processor.
/// </summary>
public sealed class WorldRuleRegistry
{
    private readonly List<WorldRuleRegistration> _registrations = new();

    public IReadOnlyList<WorldRuleRegistration> Registrations =>
        _registrations.AsReadOnly();

    public WorldRuleRegistry Register(string id, IWorldRule rule)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "A world rule identifier cannot be empty.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(rule);
        var normalizedId = id.Trim();
        if (_registrations.Any(registration =>
            registration.Id.Equals(
                normalizedId,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"World rule '{normalizedId}' is already registered.");
        }

        _registrations.Add(new WorldRuleRegistration(normalizedId, rule));
        return this;
    }

    public WorldSimulator CreateSimulator(int maximumEvents = 1000) =>
        new(
            _registrations.Select(registration => registration.Rule),
            maximumEvents);
}

public sealed record WorldRuleRegistration(string Id, IWorldRule Rule);
