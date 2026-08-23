using TessitoreGM.AiGm;

namespace TessitoreGM.World.Tests;

public sealed class AiGmModeSettingsStoreTests
{
    [Fact]
    public void Get_NewCampaign_IsDisabledWithoutCreatingAFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsFile = Path.Combine(directory, "config", "ai-gm.json");
            var campaignFile = Path.Combine(directory, "campaign.json");
            var store = new AiGmModeSettingsStore(settingsFile);

            var settings = store.Get(campaignFile);

            Assert.False(settings.Enabled);
            Assert.False(settings.ProviderConfigured);
            Assert.False(File.Exists(settingsFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SetEnabled_PersistsOutsideCampaignWithoutChangingIt()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsFile = Path.Combine(directory, "config", "ai-gm.json");
            var campaignFile = Path.Combine(directory, "campaign.json");
            const string campaignContents = "canonical campaign data";
            File.WriteAllText(campaignFile, campaignContents);
            var store = new AiGmModeSettingsStore(settingsFile);

            store.SetEnabled(campaignFile, enabled: true);
            var reloaded = new AiGmModeSettingsStore(settingsFile)
                .Get(campaignFile);

            Assert.True(reloaded.Enabled);
            Assert.Equal(campaignContents, File.ReadAllText(campaignFile));
            Assert.True(File.Exists(settingsFile));
            Assert.NotEqual(
                Path.GetFullPath(campaignFile),
                Path.GetFullPath(settingsFile));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SetProvider_StoresOnlyProviderMetadata_NotCredentials()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settingsFile = Path.Combine(directory, "config", "ai-gm.json");
            var campaignFile = Path.Combine(directory, "campaign.json");
            var store = new AiGmModeSettingsStore(settingsFile);

            var configured = store.SetProvider(
                campaignFile,
                "provider-example",
                "model-example");
            var json = File.ReadAllText(settingsFile);

            Assert.True(configured.ProviderConfigured);
            Assert.Contains("provider-example", json);
            Assert.Contains("model-example", json);
            Assert.False(json.Contains(
                "apiKey",
                StringComparison.OrdinalIgnoreCase));
            Assert.False(json.Contains(
                "secret",
                StringComparison.OrdinalIgnoreCase));
            Assert.False(json.Contains(
                "token",
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Settings_AreIndependentForEachCampaign()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new AiGmModeSettingsStore(
                Path.Combine(directory, "config", "ai-gm.json"));
            var firstCampaign = Path.Combine(directory, "first.json");
            var secondCampaign = Path.Combine(directory, "second.json");

            store.SetEnabled(firstCampaign, enabled: true);

            Assert.True(store.Get(firstCampaign).Enabled);
            Assert.False(store.Get(secondCampaign).Enabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TessitoreGM-ai-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
