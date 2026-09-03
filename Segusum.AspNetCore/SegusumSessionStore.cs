using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Segusum.AspNetCore;

/// <summary>
/// Sessioni di gameplay effimere. Il token non è un'identità persistente e
/// tutte le sessioni vengono perse quando il processo viene riavviato.
/// </summary>
public sealed class SegusumSessionStore
{
    private readonly ConcurrentDictionary<string, SessionData> sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, DateTimeOffset> persistedAccess = new();
    private readonly TimeSpan idleTimeout;

    public SegusumSessionStore(TimeSpan? configuredIdleTimeout = null)
    {
        var configuredMinutes = Environment.GetEnvironmentVariable("SEGUSUM_SESSION_IDLE_MINUTES");
        idleTimeout = configuredIdleTimeout ?? (int.TryParse(configuredMinutes, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : TimeSpan.FromHours(1));
    }

    public string Create(int userId, string username, bool isTextMode, bool isCasualMode, int? gameId)
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow;
        sessions[token] = new SessionData(token, userId, username, now, now, isTextMode, isCasualMode, gameId);
        return token;
    }

    public bool TryGet(string token, out SessionData session)
    {
        session = null!;
        if (string.IsNullOrWhiteSpace(token) || !sessions.TryGetValue(token, out session!))
            return false;

        var now = DateTimeOffset.UtcNow;
        if (now - session.LastUsedUtc > idleTimeout)
        {
            sessions.TryRemove(new KeyValuePair<string, SessionData>(token, session));
            session = null!;
            return false;
        }

        var refreshed = session with { LastUsedUtc = now };
        sessions[token] = refreshed;
        session = refreshed;
        return true;
    }

    public bool Remove(string token) => !string.IsNullOrWhiteSpace(token) && sessions.TryRemove(token, out _);

    public bool ShouldPersistAccess(int userId, DateTimeOffset now)
    {
        while (true)
        {
            if (persistedAccess.TryGetValue(userId, out var previous) && now - previous < TimeSpan.FromSeconds(60))
                return false;
            if (!persistedAccess.TryGetValue(userId, out previous))
            {
                if (persistedAccess.TryAdd(userId, now)) return true;
            }
            else if (persistedAccess.TryUpdate(userId, now, previous)) return true;
        }
    }

    public bool UpdateCasualMode(string token, bool casualMode)
    {
        if (!sessions.TryGetValue(token, out var session)) return false;
        sessions[token] = session with { IsCasualMode = casualMode };
        return true;
    }

    public int Count => sessions.Count;

    public sealed record SessionData(
        string Token,
        int UserId,
        string Username,
        DateTimeOffset CreatedUtc,
        DateTimeOffset LastUsedUtc,
        bool IsTextMode,
        bool IsCasualMode,
        int? GameId);
}
