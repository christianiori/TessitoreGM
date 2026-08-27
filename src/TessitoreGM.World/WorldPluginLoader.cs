using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace TessitoreGM.World;

public sealed class WorldPluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WorldPluginLoadResult Load(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "The plugin directory cannot be empty.",
                nameof(directory));
        }

        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            return WorldPluginLoadResult.Empty;
        }

        var registry = new WorldRuleRegistry();
        var plugins = new List<WorldPluginInfo>();
        var issues = new List<WorldPluginIssue>();
        var disabled = 0;

        foreach (var manifestPath in Directory.EnumerateFiles(
            root,
            "*.plugin.json",
            SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                var manifest = ReadManifest(manifestPath);
                if (!manifest.Enabled)
                {
                    disabled++;
                    continue;
                }

                LoadEnabledPlugin(root, manifestPath, manifest, registry, plugins);
            }
            catch (Exception exception) when (
                exception is IOException or JsonException or
                InvalidDataException or InvalidOperationException or
                ArgumentException or ReflectionTypeLoadException or
                BadImageFormatException)
            {
                issues.Add(new WorldPluginIssue(
                    Path.GetFileName(manifestPath),
                    exception.Message));
            }
        }

        return new WorldPluginLoadResult(
            plugins,
            registry.Registrations,
            issues,
            disabled);
    }

    private static WorldPluginManifest ReadManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<WorldPluginManifest>(
            File.ReadAllText(path),
            JsonOptions) ?? throw new InvalidDataException(
                "Il manifesto del plugin è vuoto.");
        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            throw new InvalidDataException(
                "Il manifesto deve indicare id, version e assembly.");
        }

        return manifest;
    }

    private static void LoadEnabledPlugin(
        string root,
        string manifestPath,
        WorldPluginManifest manifest,
        WorldRuleRegistry registry,
        List<WorldPluginInfo> plugins)
    {
        var assemblyPath = Path.GetFullPath(Path.Combine(root, manifest.Assembly));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!assemblyPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
            !assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "L'assembly del plugin deve essere un file DLL nella cartella Plugins.");
        }

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                "L'assembly indicato dal plugin non esiste.",
                assemblyPath);
        }

        var context = new PluginLoadContext(assemblyPath);
        var assembly = context.LoadPluginAssembly(assemblyPath);
        var pluginTypes = assembly.GetTypes()
            .Where(type =>
                typeof(IWorldPlugin).IsAssignableFrom(type) &&
                type is { IsClass: true, IsAbstract: false })
            .ToArray();
        if (pluginTypes.Length != 1)
        {
            throw new InvalidDataException(
                "L'assembly deve contenere esattamente un IWorldPlugin pubblico e concreto.");
        }

        var plugin = Activator.CreateInstance(pluginTypes[0]) as IWorldPlugin
            ?? throw new InvalidDataException(
                "Non riesco a creare il plugin. È richiesto un costruttore senza parametri.");
        if (!plugin.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase) ||
            !plugin.Version.Equals(manifest.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Identità o versione del plugin non corrispondono al manifesto.");
        }

        var proposedRules = new WorldRuleRegistry();
        plugin.Register(proposedRules);
        var duplicate = proposedRules.Registrations.FirstOrDefault(candidate =>
            registry.Registrations.Any(existing =>
                existing.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase)));
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"La regola '{duplicate.Id}' è già registrata da un altro plugin.");
        }

        foreach (var registration in proposedRules.Registrations)
        {
            registry.Register(registration.Id, registration.Rule);
        }

        plugins.Add(new WorldPluginInfo(
            plugin.Id,
            plugin.Version,
            Path.GetFileName(manifestPath),
            proposedRules.Registrations.Count));
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string mainAssemblyPath)
            : base(isCollectible: false) =>
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        public Assembly LoadPluginAssembly(string path)
        {
            using var stream = File.OpenRead(path);
            return LoadFromStream(stream);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith(
                "TessitoreGM.",
                StringComparison.Ordinal) == true)
            {
                return null;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }

    private sealed record WorldPluginManifest(
        string Id,
        string Version,
        string Assembly,
        bool Enabled = false);
}

public sealed record WorldPluginInfo(
    string Id,
    string Version,
    string ManifestFile,
    int RuleCount);

public sealed record WorldPluginIssue(string ManifestFile, string Message);

public sealed record WorldPluginLoadResult(
    IReadOnlyList<WorldPluginInfo> Plugins,
    IReadOnlyList<WorldRuleRegistration> Rules,
    IReadOnlyList<WorldPluginIssue> Issues,
    int DisabledCount)
{
    public static WorldPluginLoadResult Empty { get; } = new(
        Array.Empty<WorldPluginInfo>(),
        Array.Empty<WorldRuleRegistration>(),
        Array.Empty<WorldPluginIssue>(),
        0);
}
