using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.World.Tests;

public sealed class WorldPluginLoaderTests
{
    [Fact]
    public void Load_DisabledManifest_DoesNotLoadAssembly()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            WriteManifest(directory, enabled: false, assembly: "missing.dll");

            var result = new WorldPluginLoader().Load(directory);

            Assert.Empty(result.Plugins);
            Assert.Empty(result.Issues);
            Assert.Equal(1, result.DisabledCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_EnabledCompatiblePlugin_RegistersItsRules()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            const string assemblyName = "loader-test-plugin.dll";
            File.Copy(
                typeof(WorldPluginLoaderTests).Assembly.Location,
                Path.Combine(directory, assemblyName));
            WriteManifest(directory, enabled: true, assembly: assemblyName);

            var result = new WorldPluginLoader().Load(directory);

            var plugin = Assert.Single(result.Plugins);
            Assert.Equal("tests:loader", plugin.Id);
            Assert.Equal("1.0.0", plugin.Version);
            Assert.Equal(1, plugin.RuleCount);
            Assert.Equal("tests:idle", Assert.Single(result.Rules).Id);
            Assert.Empty(result.Issues);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_BrokenEnabledPlugin_ReportsIssueWithoutStoppingCatalog()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            WriteManifest(directory, enabled: true, assembly: "missing.dll");

            var result = new WorldPluginLoader().Load(directory);

            Assert.Empty(result.Plugins);
            var issue = Assert.Single(result.Issues);
            Assert.Contains("non esiste", issue.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_AssemblyOutsidePluginDirectory_IsRejected()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            WriteManifest(directory, enabled: true, assembly: "..\\outside.dll");

            var result = new WorldPluginLoader().Load(directory);

            Assert.Empty(result.Plugins);
            Assert.Contains(
                "cartella Plugins",
                Assert.Single(result.Issues).Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteManifest(
        string directory,
        bool enabled,
        string assembly) => File.WriteAllText(
            Path.Combine(directory, "loader-test.plugin.json"),
            $$"""
            {
              "id": "tests:loader",
              "version": "1.0.0",
              "assembly": "{{assembly.Replace("\\", "\\\\")}}",
              "enabled": {{enabled.ToString().ToLowerInvariant()}}
            }
            """);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TessitoreGM-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

public sealed class LoaderTestPlugin : IWorldPlugin
{
    public string Id => "tests:loader";

    public string Version => "1.0.0";

    public void Register(WorldRuleRegistry rules) =>
        rules.Register("tests:idle", new LoaderTestRule());

    private sealed class LoaderTestRule : IWorldRule
    {
        public IWorldEvent? ProposeNext(
            WorldSnapshot world,
            DateTimeOffset until) => null;
    }
}
