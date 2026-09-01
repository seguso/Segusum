using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Seg
{
    internal static class FilePersistence
    {
        private static readonly object Sync = new();
        private static bool loaded;
        private static bool loading;

        // Il file storico resta leggibile per migrare le installazioni esistenti.
        // Dopo la prima scrittura, i salvataggi vengono invece spezzati per evitare
        // di riscrivere tutti gli XML a ogni mossa.
        private static string ShardedDirectory => StorageOptions.FilePath + ".d";
        private static string ManifestPath => Path.Combine(ShardedDirectory, "manifest.json");
        private static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true };

        private sealed class Snapshot
        {
            public List<UserRow> Users { get; set; } = new();
            public List<IpRow> Ips { get; set; } = new();
            public List<SavegameRow> Savegames { get; set; } = new();
        }

        private sealed class UserRow
        {
            public int Id { get; set; }
            public string Uname { get; set; }
            public string Pwd { get; set; }
            public string TempToken { get; set; }
            public DateTime? DateCreated { get; set; }
            public DateTime? DateLastAccess { get; set; }
            public string Email { get; set; }
            public bool? CanPlayGraphicsMode { get; set; }
            public int? GameId { get; set; }
            public bool? IsCasualMode { get; set; }
        }

        private sealed class IpRow
        {
            public int Id { get; set; }
            public int IdUser { get; set; }
            public string Ip { get; set; }
            public DateTime? DateLastUsed { get; set; }
        }

        private sealed class SavegameRow
        {
            public int Id { get; set; }
            public string SavegameXml { get; set; }
            public int IdUser { get; set; }
            public int IdStory { get; set; }
            public string SavegameTitle { get; set; }
            public DateTime? DateModified { get; set; }
        }

        private sealed class Manifest
        {
            public List<UserRow> Users { get; set; } = new();
            public List<IpRow> Ips { get; set; } = new();
            public List<SavegameMeta> Savegames { get; set; } = new();
        }

        private sealed class SavegameMeta
        {
            public int Id { get; set; }
            public int IdUser { get; set; }
            public int IdStory { get; set; }
            public string SavegameTitle { get; set; }
            public DateTime? DateModified { get; set; }
        }

        private static SavegameMeta ToMeta(savegame r) => new()
        {
            Id = r.id,
            IdUser = r.idUser,
            IdStory = r.idStory,
            SavegameTitle = r.savegameTitle,
            DateModified = r.dateModified
        };

        private static string SavegamePath(int id) => Path.Combine(ShardedDirectory, $"savegame-{id}.json");

        private static void WriteAtomically(string path, string contents)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            File.Move(temp, path, true);
        }

        public static void Load(segusumDb db)
        {
            if (!StorageOptions.IsFile)
                return;
            lock (Sync)
            {
                if (loaded) return;
                loaded = true;

                if (File.Exists(ManifestPath))
                {
                    var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath));
                    if (manifest != null)
                    {
                        loading = true;
                        foreach (var r in manifest.Users)
                            db.user.Add(new user { id = r.Id, uname = r.Uname, pwd = r.Pwd, tempToken = r.TempToken, dateCreated = r.DateCreated, dateLastAccess = r.DateLastAccess, email = r.Email, canPlayGraphicsMode = r.CanPlayGraphicsMode, gameId = r.GameId, isCasualMode = r.IsCasualMode });
                        foreach (var r in manifest.Ips)
                            db.ips.Add(new ips { id = r.Id, idUser = r.IdUser, ip = r.Ip, dateLastUsed = r.DateLastUsed });
                        foreach (var meta in manifest.Savegames)
                        {
                            var savePath = SavegamePath(meta.Id);
                            if (!File.Exists(savePath)) continue;
                            var save = JsonSerializer.Deserialize<SavegameRow>(File.ReadAllText(savePath));
                            if (save != null)
                                db.savegame.Add(new savegame { id = save.Id, savegameXml = save.SavegameXml, idUser = save.IdUser, idStory = save.IdStory, savegameTitle = save.SavegameTitle, dateModified = save.DateModified });
                        }
                        db.SaveChanges();
                        loading = false;
                        return;
                    }
                }

                if (!File.Exists(StorageOptions.FilePath)) return;
                var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(StorageOptions.FilePath));
                if (snapshot == null) return;
                loading = true;
                foreach (var r in snapshot.Users)
                    db.user.Add(new user { id = r.Id, uname = r.Uname, pwd = r.Pwd, tempToken = r.TempToken, dateCreated = r.DateCreated, dateLastAccess = r.DateLastAccess, email = r.Email, canPlayGraphicsMode = r.CanPlayGraphicsMode, gameId = r.GameId, isCasualMode = r.IsCasualMode });
                foreach (var r in snapshot.Ips)
                    db.ips.Add(new ips { id = r.Id, idUser = r.IdUser, ip = r.Ip, dateLastUsed = r.DateLastUsed });
                foreach (var r in snapshot.Savegames)
                    db.savegame.Add(new savegame { id = r.Id, savegameXml = r.SavegameXml, idUser = r.IdUser, idStory = r.IdStory, savegameTitle = r.SavegameTitle, dateModified = r.DateModified });
                db.SaveChanges();
                loading = false;

                // Migrazione trasparente: la partita esistente resta disponibile,
                // ma dalla prossima richiesta il runtime userà i file separati.
                Persist(db, snapshot.Savegames.Select(r => r.Id).ToArray());
            }
        }

        public static void Persist(segusumDb db, IReadOnlyCollection<int> changedSavegameIds)
        {
            if (!StorageOptions.IsFile || loading) return;
            lock (Sync)
            {
                Directory.CreateDirectory(ShardedDirectory);

                var saveRows = db.savegame.AsNoTracking().Select(r => new SavegameRow { Id = r.id, SavegameXml = r.savegameXml, IdUser = r.idUser, IdStory = r.idStory, SavegameTitle = r.savegameTitle, DateModified = r.dateModified }).ToList();
                var currentIds = saveRows.Select(r => r.Id).ToHashSet();
                var hasManifest = File.Exists(ManifestPath);

                // La prima volta migra il vecchio JSON. In seguito tocca solo i
                // salvataggi realmente coinvolti nella transazione EF.
                var idsToWrite = hasManifest ? changedSavegameIds : currentIds;
                foreach (var row in saveRows.Where(r => idsToWrite.Contains(r.Id)))
                    WriteAtomically(SavegamePath(row.Id), JsonSerializer.Serialize(row, JsonOptions));

                if (hasManifest)
                {
                    foreach (var oldId in Directory.EnumerateFiles(ShardedDirectory, "savegame-*.json")
                                 .Select(path => Path.GetFileNameWithoutExtension(path).Substring("savegame-".Length))
                                 .Where(id => int.TryParse(id, out _)).Select(int.Parse).Where(id => !currentIds.Contains(id)))
                        File.Delete(SavegamePath(oldId));
                }

                var manifest = new Manifest
                {
                    Users = db.user.AsNoTracking().Select(r => new UserRow { Id = r.id, Uname = r.uname, Pwd = r.pwd, TempToken = r.tempToken, DateCreated = r.dateCreated, DateLastAccess = r.dateLastAccess, Email = r.email, CanPlayGraphicsMode = r.canPlayGraphicsMode, GameId = r.gameId, IsCasualMode = r.isCasualMode }).ToList(),
                    Ips = db.ips.AsNoTracking().Select(r => new IpRow { Id = r.id, IdUser = r.idUser, Ip = r.ip, DateLastUsed = r.dateLastUsed }).ToList(),
                    Savegames = saveRows.Select(r => new SavegameMeta { Id = r.Id, IdUser = r.IdUser, IdStory = r.IdStory, SavegameTitle = r.SavegameTitle, DateModified = r.DateModified }).ToList()
                };
                WriteAtomically(ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            }
        }
    }

    public partial class segusumDb
    {
        public override int SaveChanges()
        {
            var changedSavegames = ChangeTracker.Entries<savegame>()
                .Where(e => e.State != EntityState.Unchanged)
                .Select(e => e.Entity)
                .ToList();
            var result = base.SaveChanges();
            FilePersistence.Persist(this, changedSavegames.Select(e => e.id).ToArray());
            return result;
        }
    }
}
