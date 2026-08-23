namespace TessitoreGM.Gm;

internal static class StandaloneWorkspace
{
    private const string ApplicationDirectoryName = "TessitoreGM";
    private const string CampaignDirectoryName = "Campagne";
    private const string DemoFileName = "villaggio-demo.json";
    private const string TemplateFileName = "village.json";

    public static string PrepareDefaultCampaign(
        string launchDirectory,
        string applicationDirectory)
    {
        if (string.IsNullOrWhiteSpace(launchDirectory))
        {
            throw new ArgumentException(
                "The launch directory cannot be empty.",
                nameof(launchDirectory));
        }
        if (string.IsNullOrWhiteSpace(applicationDirectory))
        {
            throw new ArgumentException(
                "The application directory cannot be empty.",
                nameof(applicationDirectory));
        }

        var documents = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            documents = launchDirectory;
        }

        var campaignDirectory = Path.Combine(
            documents,
            ApplicationDirectoryName,
            CampaignDirectoryName);
        Directory.CreateDirectory(campaignDirectory);

        var campaignPath = Path.Combine(campaignDirectory, DemoFileName);
        if (File.Exists(campaignPath))
        {
            return campaignPath;
        }

        var templatePath = FindTemplate(
            launchDirectory,
            applicationDirectory);
        File.Copy(templatePath, campaignPath);
        return campaignPath;
    }

    private static string FindTemplate(
        string launchDirectory,
        string applicationDirectory)
    {
        var publishedTemplate = Path.Combine(
            applicationDirectory,
            TemplateFileName);
        if (File.Exists(publishedTemplate))
        {
            return publishedTemplate;
        }

        for (var directory = new DirectoryInfo(launchDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                TemplateFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "La campagna dimostrativa non è presente nel pacchetto.",
            publishedTemplate);
    }
}
