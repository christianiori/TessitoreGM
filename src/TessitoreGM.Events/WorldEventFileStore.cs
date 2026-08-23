namespace TessitoreGM.Events;

public sealed class WorldEventFileStore
{
    private const int DefaultMaximumBackups = 20;
    private readonly WorldEventJsonSerializer _serializer;

    public WorldEventFileStore(WorldEventJsonSerializer? serializer = null)
    {
        _serializer = serializer ?? new WorldEventJsonSerializer();
    }

    public WorldEventLog Load(string path)
    {
        var fullPath = ValidatedPath(path);
        return _serializer.Deserialize(File.ReadAllText(fullPath));
    }

    public void Save(
        string path,
        WorldEventLog eventLog,
        int maximumBackups = DefaultMaximumBackups)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        if (maximumBackups <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBackups));
        }

        var fullPath = ValidatedPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                "The world file has no parent directory.",
                nameof(path));
        Directory.CreateDirectory(directory);

        var serialized = _serializer.Serialize(eventLog);
        _ = _serializer.Deserialize(serialized);

        if (File.Exists(fullPath))
        {
            _ = Load(fullPath);
            CreateBackup(fullPath);
        }

        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") +
            ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, serialized);
            _ = Load(temporaryPath);
            File.Move(temporaryPath, fullPath, overwrite: true);
            PruneBackups(fullPath, maximumBackups);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public IReadOnlyList<WorldEventBackup> ListBackups(string path)
    {
        var fullPath = ValidatedPath(path);
        var directory = BackupDirectory(fullPath);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<WorldEventBackup>();
        }

        return Directory.EnumerateFiles(
                directory,
                BackupPattern(fullPath),
                SearchOption.TopDirectoryOnly)
            .Select(backupPath => new FileInfo(backupPath))
            .OrderByDescending(file => file.CreationTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .Select(file => new WorldEventBackup(
                file.Name,
                file.FullName,
                file.CreationTimeUtc))
            .ToArray();
    }

    public string RestoreBackup(string path, string backupFileName)
    {
        var fullPath = ValidatedPath(path);
        if (string.IsNullOrWhiteSpace(backupFileName) ||
            Path.GetFileName(backupFileName) != backupFileName ||
            !backupFileName.StartsWith(
                BackupStem(fullPath) + ".",
                StringComparison.Ordinal) ||
            !backupFileName.EndsWith(
                ".backup.json",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The backup file name is invalid.",
                nameof(backupFileName));
        }

        var backupDirectory = BackupDirectory(fullPath);
        var backupPath = Path.Combine(backupDirectory, backupFileName);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException(
                "The selected backup does not exist.",
                backupPath);
        }

        var restored = Load(backupPath);
        var serialized = _serializer.Serialize(restored);
        var preservedPath = PreserveBeforeRestore(fullPath, backupDirectory);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") +
            ".restore.tmp";
        try
        {
            File.WriteAllText(temporaryPath, serialized);
            _ = Load(temporaryPath);
            File.Move(temporaryPath, fullPath, overwrite: true);
            _ = Load(fullPath);
            return preservedPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ValidatedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "The world file path cannot be empty.",
                nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private void CreateBackup(string fullPath)
    {
        var backupDirectory = BackupDirectory(fullPath);
        Directory.CreateDirectory(backupDirectory);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var backupPath = Path.Combine(
            backupDirectory,
            $"{BackupStem(fullPath)}.{timestamp}-{suffix}.backup.json");
        File.Copy(fullPath, backupPath);
        _ = Load(backupPath);
    }

    private void PruneBackups(string fullPath, int maximumBackups)
    {
        foreach (var backup in ListBackups(fullPath).Skip(maximumBackups))
        {
            File.Delete(backup.FullPath);
        }
    }

    private static string PreserveBeforeRestore(
        string fullPath,
        string backupDirectory)
    {
        Directory.CreateDirectory(backupDirectory);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var preservedPath = Path.Combine(
            backupDirectory,
            $"{BackupStem(fullPath)}.{timestamp}-{suffix}.before-restore.json");
        if (File.Exists(fullPath))
        {
            File.Copy(fullPath, preservedPath);
        }
        else
        {
            File.WriteAllText(
                preservedPath,
                "The world file did not exist before this restore.");
        }

        return preservedPath;
    }

    private static string BackupDirectory(string fullPath) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            "Backups");

    private static string BackupStem(string fullPath) =>
        Path.GetFileNameWithoutExtension(fullPath);

    private static string BackupPattern(string fullPath) =>
        BackupStem(fullPath) + ".*.backup.json";
}

public sealed record WorldEventBackup(
    string FileName,
    string FullPath,
    DateTime CreatedAtUtc);
