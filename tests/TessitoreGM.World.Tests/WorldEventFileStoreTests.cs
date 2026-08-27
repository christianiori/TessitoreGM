using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldEventFileStoreTests
{
    [Fact]
    public void Save_ExistingWorld_CreatesReadableBackupBeforeReplacement()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            var initialTime = At(3, 8);
            var updatedTime = At(3, 9);
            var initial = LogAt(initialTime);
            var updated = new WorldEventLog(
                initial.InitialWorld,
                new IWorldEvent[] { new WorldTimeAdvanced(updatedTime) });
            var store = new WorldEventFileStore();

            store.Save(path, initial);
            store.Save(path, updated);

            var backup = Assert.Single(store.ListBackups(path));
            Assert.Equal(initialTime, store.Load(backup.FullPath).InitialWorld.CurrentTime);
            Assert.Equal(
                updatedTime,
                Assert.IsType<WorldTimeAdvanced>(
                    Assert.Single(store.Load(path).Events)).OccurredAt);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_CorruptedExistingWorld_RefusesToOverwriteIt()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            const string corrupted = "not a world";
            File.WriteAllText(path, corrupted);
            var store = new WorldEventFileStore();

            Assert.Throws<InvalidDataException>(() =>
                store.Save(path, LogAt(At(3, 9))));

            Assert.Equal(corrupted, File.ReadAllText(path));
            Assert.Empty(store.ListBackups(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_PrunesOldBackupsUsingConfiguredLimit()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            var store = new WorldEventFileStore();
            store.Save(path, LogAt(At(3, 8)), maximumBackups: 2);

            store.Save(path, LogAt(At(3, 9)), maximumBackups: 2);
            store.Save(path, LogAt(At(3, 10)), maximumBackups: 2);
            store.Save(path, LogAt(At(3, 11)), maximumBackups: 2);

            Assert.Equal(2, store.ListBackups(path).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_VersionTwoWorld_CreatesOldFormatBackupAndWritesVersionThree()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            var serializer = new WorldEventJsonSerializer();
            var versionTwo = serializer.Serialize(LogAt(At(3, 8)))
                .Replace("\"version\": 3", "\"version\": 2");
            File.WriteAllText(path, versionTwo);
            var store = new WorldEventFileStore(serializer);

            store.Save(path, store.Load(path));

            Assert.Equal(3, serializer.InspectFormat(File.ReadAllText(path)).Version);
            var backup = Assert.Single(store.ListBackups(path));
            Assert.Equal(
                2,
                serializer.InspectFormat(File.ReadAllText(backup.FullPath)).Version);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoreBackup_ReplacesCorruptedWorldAndPreservesIt()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            var store = new WorldEventFileStore();
            store.Save(path, LogAt(At(3, 8)));
            store.Save(path, LogAt(At(3, 9)));
            var backup = Assert.Single(store.ListBackups(path));
            File.WriteAllText(path, "corrupted world");

            var preservedPath = store.RestoreBackup(path, backup.FileName);

            Assert.Equal(At(3, 8), store.Load(path).InitialWorld.CurrentTime);
            Assert.Equal("corrupted world", File.ReadAllText(preservedPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoreBackup_RejectsPathTraversal()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "campaign.json");
            var store = new WorldEventFileStore();
            store.Save(path, LogAt(At(3, 8)));

            Assert.Throws<ArgumentException>(() =>
                store.RestoreBackup(path, "..\\another.json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static WorldEventLog LogAt(DateTimeOffset time) =>
        new(
            new WorldInitialState(time, Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>());

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TessitoreGM-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static DateTimeOffset At(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, TimeSpan.Zero);
}
