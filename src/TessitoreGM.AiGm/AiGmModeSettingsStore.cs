using System.Text.Json;

namespace TessitoreGM.AiGm;

public sealed record AiGmCampaignModeSettings(
    string CampaignPath,
    bool Enabled = false,
    string? ProviderId = null,
    string? Model = null)
{
    public bool ProviderConfigured =>
        !string.IsNullOrWhiteSpace(ProviderId) &&
        !string.IsNullOrWhiteSpace(Model);
}

/// <summary>
/// Stores mode and provider metadata outside campaign saves. Authentication
/// secrets are deliberately not represented by this model and must be supplied
/// to a future provider adapter through the process environment.
/// </summary>
public sealed class AiGmModeSettingsStore
{
    private const int CurrentVersion = 1;
    private readonly string _settingsFile;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AiGmModeSettingsStore(string settingsFile)
    {
        if (string.IsNullOrWhiteSpace(settingsFile))
        {
            throw new ArgumentException(
                "The AI GM settings path cannot be empty.",
                nameof(settingsFile));
        }

        _settingsFile = Path.GetFullPath(settingsFile);
    }

    public string SettingsFile => _settingsFile;

    public AiGmCampaignModeSettings Get(string campaignPath)
    {
        var normalizedPath = NormalizeCampaignPath(campaignPath);
        return Load().Campaigns.FirstOrDefault(campaign =>
                campaign.CampaignPath.Equals(
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
            ?? new AiGmCampaignModeSettings(normalizedPath);
    }

    public AiGmCampaignModeSettings SetEnabled(
        string campaignPath,
        bool enabled)
    {
        var current = Get(campaignPath);
        var updated = current with { Enabled = enabled };
        SaveCampaign(updated);
        return updated;
    }

    public AiGmCampaignModeSettings SetProvider(
        string campaignPath,
        string providerId,
        string model)
    {
        var normalizedProvider = RequiredIdentifier(providerId, nameof(providerId));
        var normalizedModel = RequiredIdentifier(model, nameof(model));
        var current = Get(campaignPath);
        var updated = current with
        {
            ProviderId = normalizedProvider,
            Model = normalizedModel
        };
        SaveCampaign(updated);
        return updated;
    }

    private void SaveCampaign(AiGmCampaignModeSettings updated)
    {
        var document = Load();
        var campaigns = document.Campaigns
            .Where(campaign => !campaign.CampaignPath.Equals(
                updated.CampaignPath,
                StringComparison.OrdinalIgnoreCase))
            .Append(updated)
            .OrderBy(campaign => campaign.CampaignPath,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Save(new AiGmSettingsDocument(CurrentVersion, campaigns));
    }

    private AiGmSettingsDocument Load()
    {
        if (!File.Exists(_settingsFile))
        {
            return new AiGmSettingsDocument(
                CurrentVersion,
                Array.Empty<AiGmCampaignModeSettings>());
        }

        AiGmSettingsDocument document;
        try
        {
            document = JsonSerializer.Deserialize<AiGmSettingsDocument>(
                File.ReadAllText(_settingsFile),
                JsonOptions)
                ?? throw new InvalidDataException(
                    "The AI GM settings are invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The AI GM settings contain invalid JSON.",
                exception);
        }

        Validate(document);
        return document;
    }

    private void Save(AiGmSettingsDocument document)
    {
        Validate(document);
        var directory = Path.GetDirectoryName(_settingsFile)
            ?? throw new InvalidOperationException(
                "The AI GM settings file has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryFile = _settingsFile + "." +
            Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(document, JsonOptions);
            File.WriteAllText(temporaryFile, json);
            var written = JsonSerializer.Deserialize<AiGmSettingsDocument>(
                File.ReadAllText(temporaryFile),
                JsonOptions)
                ?? throw new InvalidDataException(
                    "The written AI GM settings are invalid.");
            Validate(written);
            File.Move(temporaryFile, _settingsFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static void Validate(AiGmSettingsDocument document)
    {
        if (document.Version != CurrentVersion ||
            document.Campaigns is null ||
            document.Campaigns.Any(campaign =>
                campaign is null ||
                string.IsNullOrWhiteSpace(campaign.CampaignPath) ||
                (campaign.ProviderId is not null &&
                    string.IsNullOrWhiteSpace(campaign.ProviderId)) ||
                (campaign.Model is not null &&
                    string.IsNullOrWhiteSpace(campaign.Model)) ||
                campaign.ProviderId?.Length > 100 ||
                campaign.Model?.Length > 100 ||
                (campaign.ProviderId is null) != (campaign.Model is null)) ||
            document.Campaigns.Select(campaign => campaign.CampaignPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                document.Campaigns.Count)
        {
            throw new InvalidDataException("The AI GM settings are invalid.");
        }
    }

    private static string NormalizeCampaignPath(string campaignPath)
    {
        if (string.IsNullOrWhiteSpace(campaignPath))
        {
            throw new ArgumentException(
                "The campaign path cannot be empty.",
                nameof(campaignPath));
        }

        return Path.GetFullPath(campaignPath);
    }

    private static string RequiredIdentifier(string value, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
        {
            throw new ArgumentException(
                "Provider identifiers must contain from 1 to 100 characters.",
                parameterName);
        }

        return normalized;
    }

    private sealed record AiGmSettingsDocument(
        int Version,
        IReadOnlyList<AiGmCampaignModeSettings> Campaigns);
}
