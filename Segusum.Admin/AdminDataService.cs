using System.Xml.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Seg;

namespace Segusum.Admin;

public sealed record AuditQuery(int? GameId, string Search, string Category, bool ShowIgnored, int MinSeenCount, string Sort, bool Desc);
public sealed record PastQuery(int? GameId, string Search, string UserName, int? UserId, string Type, int MinAttempts, bool SeparateExplanations, bool OnlyUnhandled = true);
public sealed record PastAttempt(int UserId, string UserName, int GameId, DateTime Time, string Type, string FirstId, string SecondId, string? Explanation, bool? HandlerCalled, string Details);
public sealed record PastSummary(int GameId, string Type, string FirstId, string SecondId, string? Explanation, int Attempts, int Users, DateTime LastAttempt, bool AnyUnhandled);
public sealed record AdminUser(int Id, string Name, int? GameId, DateTime? LastAccess, DateTime? LastSave);
public sealed record AdminUserDetails(AdminUser User, IReadOnlyList<PastAttempt> Actions, IReadOnlyList<CycleInfo> Cycles);
public sealed record CycleInfo(string Id, int Count, DateTime? LastExecution);
public sealed record AdminMessageSummary(long Id, int UserId, string Category, string FirstId, string SecondId, string? ExplanationId, string Text, DateTime CreatedAtUtc, DateTime? DeliveredAtUtc, DateTime? SeenAtUtc, bool Cancelled);

public sealed class AdminDataService
{
    private readonly IDbContextFactory<segusumDb> factory;
    public AdminDataService(IDbContextFactory<segusumDb> factory) => this.factory = factory;

    public async Task<List<AdminMessageSummary>> FindAdminMessagesAsync(int? userId, int? gameId, bool pendingOnly)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.adminNarrativeMessage.AsNoTracking().AsQueryable();
        if (userId.HasValue) q = q.Where(x => x.userId == userId);
        if (gameId.HasValue) q = q.Where(x => x.gameId == gameId);
        if (pendingOnly) q = q.Where(x => !x.cancelled && !x.seenAtUtc.HasValue);
        var rows = await q.OrderByDescending(x => x.id).ToListAsync();
        return rows.Select(x => new AdminMessageSummary(x.id, x.userId, x.category, x.firstId, x.secondId,
            x.explanationId, string.Join(" / ", ParseMessageTexts(x.narTextsJson)), x.createdAtUtc,
            x.deliveredAtUtc, x.seenAtUtc, x.cancelled)).ToList();
    }

    public async Task<long> QueueAdminMessageAsync(int userId, int gameId, string category, string firstId, string secondId, string? explanationId, string text)
    {
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException("Il messaggio non può essere vuoto.", nameof(text));
        await using var db = await factory.CreateDbContextAsync();
        var row = new AdminNarrativeMessage { userId = userId, gameId = gameId, category = category ?? "",
            firstId = firstId ?? "", secondId = secondId ?? "", explanationId = explanationId,
            narTextsJson = JsonSerializer.Serialize(parts), createdAtUtc = DateTime.UtcNow };
        db.adminNarrativeMessage.Add(row);
        await db.SaveChangesAsync();
        return row.id;
    }

    public async Task CancelAdminMessageAsync(long id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.adminNarrativeMessage.SingleOrDefaultAsync(x => x.id == id);
        if (row != null && !row.seenAtUtc.HasValue) { row.cancelled = true; await db.SaveChangesAsync(); }
    }

    private static string[] ParseMessageTexts(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    public async Task<List<UnhandledCombination>> FindUnhandledAsync(AuditQuery filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.unhandledCombination.AsNoTracking().Where(x => (!filter.GameId.HasValue || x.gameId == filter.GameId) &&
            (filter.ShowIgnored || !x.isIgnored) && x.seenCount >= filter.MinSeenCount);
        if (!string.IsNullOrWhiteSpace(filter.Category)) q = q.Where(x => x.category == filter.Category);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(x => x.category.Contains(s) || (x.firstCodeName ?? "").Contains(s) || (x.secondCodeName ?? "").Contains(s) ||
                x.firstId.Contains(s) || x.secondId.Contains(s) || x.firstName.Contains(s) || x.secondName.Contains(s));
        }
        var rows = await q.ToListAsync();
        return (filter.Sort == "count" ? (filter.Desc ? rows.OrderByDescending(x => x.seenCount) : rows.OrderBy(x => x.seenCount)) :
            filter.Desc ? rows.OrderByDescending(x => x.lastSeenUtc) : rows.OrderBy(x => x.lastSeenUtc)).ThenBy(x => x.id).ToList();
    }

    public async Task SetIgnoredAsync(long id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Il motivo non può essere vuoto.", nameof(reason));
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.unhandledCombination.SingleAsync(x => x.id == id);
        row.isIgnored = true; row.ignoreReason = reason.Trim(); row.ignoredAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ReactivateAsync(long id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.unhandledCombination.SingleAsync(x => x.id == id);
        row.isIgnored = false; row.ignoreReason = null; row.ignoredAtUtc = null;
        await db.SaveChangesAsync();
    }

    public async Task<List<AdminUser>> FindUsersAsync(string search)
    {
        await using var db = await factory.CreateDbContextAsync();
        var users = await db.user.AsNoTracking().ToListAsync();
        var saves = await db.savegame.AsNoTracking().Where(x => x.savegameTitle == "").ToListAsync();
        return users.Where(x => string.IsNullOrWhiteSpace(search) || x.uname.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => new AdminUser(x.id, x.uname, x.gameId, x.dateLastAccess, saves.Where(s => s.idUser == x.id).Max(s => s.dateModified)))
            .OrderBy(x => x.Name).ToList();
    }

    public async Task<AdminUserDetails?> GetUserAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.user.AsNoTracking().SingleOrDefaultAsync(x => x.id == id);
        if (user == null) return null;
        var save = await db.savegame.AsNoTracking().Where(x => x.idUser == id && x.savegameTitle == "").OrderByDescending(x => x.dateModified).FirstOrDefaultAsync();
        var adminUser = new AdminUser(user.id, user.uname, user.gameId, user.dateLastAccess, save?.dateModified);
        return new AdminUserDetails(adminUser, save == null ? Array.Empty<PastAttempt>() : ParseTimelineActions(save.savegameXml, user), save == null ? Array.Empty<CycleInfo>() : ParseCycles(save.savegameXml));
    }

    public async Task<List<PastSummary>> FindPastActionsAsync(PastQuery filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        var users = await db.user.AsNoTracking().Where(x => (!filter.GameId.HasValue || x.gameId == filter.GameId) &&
            (!filter.UserId.HasValue || x.id == filter.UserId) && (string.IsNullOrWhiteSpace(filter.UserName) || x.uname.Contains(filter.UserName))).ToListAsync();
        var saves = await db.savegame.AsNoTracking().Where(x => x.savegameTitle == "").ToListAsync();
        var attempts = users.SelectMany(u => saves.Where(s => s.idUser == u.id).OrderByDescending(s => s.dateModified).Take(1).SelectMany(s => ParseActions(s.savegameXml, u)))
            .Where(x => string.IsNullOrWhiteSpace(filter.Type) || x.Type == filter.Type)
            .Where(x => !filter.OnlyUnhandled || x.HandlerCalled == false)
            .Where(x => string.IsNullOrWhiteSpace(filter.Search) || x.Details.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) || x.FirstId.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) || x.SecondId.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        var grouped = attempts.GroupBy(x => new { x.GameId, x.Type, x.FirstId, x.SecondId, Explanation = filter.SeparateExplanations ? x.Explanation : null });
        return grouped.Select(g => new PastSummary(g.Key.GameId, g.Key.Type, g.Key.FirstId, g.Key.SecondId, g.Key.Explanation, g.Count(), g.Select(x => x.UserId).Distinct().Count(), g.Max(x => x.Time), g.Any(x => x.HandlerCalled == false)))
            .Where(x => x.Attempts >= filter.MinAttempts).OrderByDescending(x => x.LastAttempt).ToList();
    }

    public static List<PastAttempt> ParseActions(string xml, user user)
    {
        if (string.IsNullOrWhiteSpace(xml)) return new();
        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root == null) return new();
            return root.Elements().Where(x => x.Name.LocalName is "past_action_use_with" or "past_action_use_for").Select(x =>
            {
                var with = x.Name.LocalName == "past_action_use_with";
                var first = x.Attribute(with ? "lo1Id" : "loId")?.Value ?? "";
                var second = x.Attribute(with ? "lo2Id" : "objId")?.Value ?? "";
                var called = x.Attribute("handler_called")?.Value switch { "Y" => true, "N" => false, _ => (bool?)null };
                var time = DateTime.TryParse(x.Attribute("time")?.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt.ToUniversalTime() : DateTime.MinValue;
                return new PastAttempt(user.id, user.uname, user.gameId ?? 0, time, with ? "useWith" : "useFor", first, second, x.Attribute("expl")?.Value, called, $"{first} { (with ? "+" : "per") } {second}");
            }).Where(x => x.FirstId != "" && x.SecondId != "").ToList();
        }
        catch { return new(); }
    }

    public static List<PastAttempt> ParseTimelineActions(string xml, user user)
    {
        if (string.IsNullOrWhiteSpace(xml)) return new();
        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root == null) return new();
            return root.Elements().Where(x => x.Name.LocalName.StartsWith("past_action_", StringComparison.Ordinal))
                .Select(x =>
                {
                    var name = x.Name.LocalName[12..];
                    var with = name == "use_with";
                    var useFor = name == "use_for";
                    var first = x.Attribute(with ? "lo1Id" : useFor ? "loId" : "loId")?.Value ?? "";
                    var second = x.Attribute(with ? "lo2Id" : useFor ? "objId" : "roomId")?.Value ?? "";
                    var called = x.Attribute("handler_called")?.Value switch { "Y" => true, "N" => false, _ => (bool?)null };
                    var time = DateTime.TryParse(x.Attribute("time")?.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt.ToUniversalTime() : DateTime.MinValue;
                    var details = string.Join(", ", x.Attributes().Where(a => a.Name.LocalName != "time").Select(a => $"{a.Name.LocalName}={a.Value}"));
                    return new PastAttempt(user.id, user.uname, user.gameId ?? 0, time, name, first, second, x.Attribute("expl")?.Value, called, details);
                }).OrderBy(x => x.Time).ToList();
        }
        catch { return new(); }
    }

    public static List<CycleInfo> ParseCycles(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return new();
        try { return XDocument.Parse(xml).Root?.Elements("cycleElem").Select(x => new CycleInfo(x.Attribute("id")?.Value ?? x.Attribute("name")?.Value ?? "", int.TryParse(x.Attribute("howMany")?.Value, out var n) ? n : 0, DateTime.TryParse(x.Attribute("lastTime")?.Value, out var d) ? d : null)).Where(x => x.Id != "").ToList() ?? new(); }
        catch { return new(); }
    }
}
