using Microsoft.EntityFrameworkCore;
using Seg;
using Segusum.Persistence;

namespace Segusum.Persistence.Tests;

public class FilePersistenceTests
{
    [Fact]
    public void FileModePersistsAndReloadsUserIpAndSave()
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-public-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "nested", "game.json");
        try
        {
            var options = new SegusumStorageOptions().UseFile(file, "test-" + Guid.NewGuid().ToString("N"));
            StorageOptions.Configure(options);
            ResetFilePersistence();
            var dbOptions = new DbContextOptionsBuilder<segusumDb>()
                .UseInMemoryDatabase(options.InMemoryDatabaseName)
                .Options;

            using (var db = new segusumDb(dbOptions))
            {
                var now = DateTime.UtcNow;
                var u = new user { uname = "author", pwd = "secret", tempToken = "none", dateCreated = now, dateLastAccess = now, canPlayGraphicsMode = false, isCasualMode = false };
                db.user.Add(u);
                db.SaveChanges();
                db.ips.Add(new ips { idUser = u.id, ip = "127.0.0.1", dateLastUsed = now });
                db.savegame.Add(new savegame { idUser = u.id, idStory = 0, savegameTitle = "main", savegameXml = "<world />", dateModified = now });
                db.SaveChanges();
            }

            Assert.True(File.Exists(file + ".d/manifest.json"));
            ResetFilePersistence();
            var reloadOptions = new DbContextOptionsBuilder<segusumDb>()
                .UseInMemoryDatabase(options.InMemoryDatabaseName + "-restart")
                .Options;
            using var reloaded = new segusumDb(reloadOptions);
            Assert.Single(reloaded.user);
            Assert.Single(reloaded.ips);
            Assert.Equal("<world />", Assert.Single(reloaded.savegame).savegameXml);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void ResetFilePersistence()
    {
        var type = typeof(segusumDb).Assembly.GetType("Seg.FilePersistence")!;
        type.GetField("loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, false);
        type.GetField("loading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, false);
    }
}
