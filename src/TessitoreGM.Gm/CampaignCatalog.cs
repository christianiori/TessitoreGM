using System.Text;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Gm;

internal sealed class CampaignCatalog
{
    private readonly string _directory;
    private readonly WorldEventJsonSerializer _serializer = new();
    private readonly WorldEventFileStore _fileStore = new();

    public CampaignCatalog(string directory)
    {
        _directory = Path.GetFullPath(directory);
    }

    public IReadOnlyList<CampaignEntry> Discover() =>
        Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryRead)
            .Where(entry => entry is not null)
            .Cast<CampaignEntry>()
            .OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public string Select(string fileName)
    {
        var path = ResolveSafeFileName(fileName);
        if (!File.Exists(path))
        {
            throw new ArgumentException("Campagna non trovata.");
        }

        _ = _serializer.Deserialize(File.ReadAllText(path));
        return path;
    }

    public string Create(string name, string templatePath)
    {
        var fileName = Slug(name) + ".save.json";
        var path = ResolveSafeFileName(fileName);
        if (File.Exists(path))
        {
            throw new ArgumentException(
                "Esiste già una campagna con questo nome.");
        }

        var source = _serializer.Deserialize(File.ReadAllText(templatePath));
        var fresh = new WorldCampaignTemplate().CreateFresh(source);
        _fileStore.Save(path, fresh);
        return path;
    }

    private CampaignEntry? TryRead(string path)
    {
        try
        {
            var log = _serializer.Deserialize(File.ReadAllText(path));
            return new CampaignEntry(
                Path.GetFileName(path),
                log.InitialWorld.CurrentTime,
                log.Events.Count);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            ArgumentException)
        {
            return null;
        }
    }

    private string ResolveSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName ||
            !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Nome del salvataggio non valido.");
        }

        return Path.Combine(_directory, fileName);
    }

    private static string Slug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Inserisci il nome della campagna.");
        }

        var slug = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var value = slug.ToString().Trim('-');
        if (value.Length is 0 or > 80)
        {
            throw new ArgumentException("Nome della campagna non valido.");
        }

        return value;
    }
}

internal sealed record CampaignEntry(
    string FileName,
    DateTimeOffset InitialTime,
    int EventCount);
