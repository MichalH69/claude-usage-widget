using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace ClaudeUsageWidget.Data;

/// <summary>Keeps the latest reading current: one request every five minutes, slower when the server asks.</summary>
public sealed class UsagePoller
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Normal = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ManualGap = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LongestWait = TimeSpan.FromHours(6);

    private readonly UsageClient client = new();
    private readonly DispatcherTimer timer = new() { Interval = Tick };
    private DateTimeOffset lastAttempt = DateTimeOffset.MinValue;
    private DateTime stateFileSeen;
    private int rateLimitStreak;
    private bool busy;

    public UsagePoller()
    {
        LoadLastReading();
        timer.Tick += async (_, _) =>
        {
            if (DateTimeOffset.UtcNow >= NextPoll) await PollAsync();
            else Changed?.Invoke();
        };
    }

    public UsageReading? Reading { get; private set; }

    /// <summary>When <see cref="Reading"/> was taken, by this widget or by Claude Code.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public FetchStatus Status { get; private set; } = FetchStatus.Ok;
    public bool HasPolled { get; private set; }
    public DateTimeOffset NextPoll { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>Raised after every poll and on every tick, so texts that count down stay current.</summary>
    public event Action? Changed;

    public void Start()
    {
        timer.Start();
        _ = PollAsync();
    }

    public void Stop() => timer.Stop();

    /// <summary>False while a request runs, for a minute after one, and while the server asks for a pause.</summary>
    public bool CanRefresh
    {
        get
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (busy || now - lastAttempt < ManualGap) return false;
            return Status != FetchStatus.RateLimited || now >= NextPoll;
        }
    }

    /// <summary>A refresh the user asked for. Does nothing unless <see cref="CanRefresh"/>.</summary>
    public void RefreshNow()
    {
        if (CanRefresh) _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            lastAttempt = DateTimeOffset.UtcNow;
            FetchResult result = await client.FetchAsync();
            // Even a login that looks ended gets a try: the date in the file is Claude Code's note, not the server's word.
            if (result.Status is FetchStatus.TokenExpired or FetchStatus.LoginEnded or FetchStatus.Unauthorized
                && await ClaudeCli.TryRenewAsync())
            {
                result = await client.FetchAsync();
            }
            Apply(result);
        }
        catch (Exception e)
        {
            // Nothing awaits this task, so an exception escaping here would vanish. Record it and try again later.
            Log.Write($"Dotaz selhal nečekaně: {e.GetType().Name}.");
            NextPoll = DateTimeOffset.UtcNow + Normal;
        }
        finally
        {
            busy = false;
            HasPolled = true;
            Changed?.Invoke();
        }
    }

    private void Apply(FetchResult result)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool changed = result.Status != Status || !HasPolled;
        Status = result.Status;

        TimeSpan wait;
        switch (result.Status)
        {
            case FetchStatus.Ok:
                Reading = result.Reading;
                ReadAt = now;
                rateLimitStreak = 0;
                wait = Normal;
                SaveLastReading(result.Body, now);
                Log.Write($"Dotaz v pořádku: session {Show(Reading?.Session)}, týden {Show(Reading?.Weekly)}, Fable {Show(Reading?.Fable)}.");
                break;

            case FetchStatus.RateLimited:
                wait = TimeSpan.FromMinutes(10 * Math.Pow(2, Math.Min(rateLimitStreak, 2)));
                if (result.RetryAfter is { } asked) wait = asked > Normal ? asked : Normal;
                if (wait > LongestWait) wait = LongestWait;
                rateLimitStreak++;
                Log.Write($"Server omezil dotazy (429), další pokus za {wait.TotalMinutes:0} min.");
                break;

            case FetchStatus.UnknownFormat:
                wait = TimeSpan.FromMinutes(30);
                if (changed) Log.Write("Odpověď má neznámý tvar, uložena do last-response.json.");
                SaveBody(result.Body);
                break;

            default:
                // An expired or missing token costs only a look at a local file, so it can be rechecked often.
                wait = result.Status switch
                {
                    FetchStatus.Unauthorized => TimeSpan.FromMinutes(10),
                    FetchStatus.ServerError => Normal,
                    _ => TimeSpan.FromMinutes(1),
                };
                if (changed) Log.Write($"Stav: {result.Status}.");
                break;
        }

        NextPoll = now + wait;
        if (result.Status != FetchStatus.Ok) UseStateFileIfNewer(now);
    }

    /// <summary>Claude Code saves each usage response it fetches. When this widget cannot ask, that copy may be fresher.</summary>
    private void UseStateFileIfNewer(DateTimeOffset now)
    {
        try
        {
            var file = new FileInfo(AppPaths.ClaudeStateFile);
            if (!file.Exists || file.LastWriteTimeUtc == stateFileSeen || file.Length > 64 * 1024 * 1024) return;
            stateFileSeen = file.LastWriteTimeUtc;

            if (UsageParser.ParseStateFile(File.ReadAllText(file.FullName)) is not { } copy) return;
            if (copy.FetchedAt > now.AddMinutes(5) || (ReadAt is { } have && copy.FetchedAt <= have)) return;

            Reading = copy.Reading;
            ReadAt = copy.FetchedAt;
            Log.Write($"Použita kopie odpovědi z Claude Code z {copy.FetchedAt.ToLocalTime():HH:mm}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Claude Code is rewriting the file. The next poll looks again.
        }
    }

    // ---- the last good reading, kept across restarts ----

    private static string LastReadingFile => Path.Combine(AppPaths.DataDir, "last-reading.json");

    private sealed record SavedReading(DateTimeOffset FetchedAt, string Body);

    /// <summary>
    /// A restart starts from the last reading this widget took. Without it, a restart while the server is limiting
    /// requests falls back on Claude Code's copy, which may be hours old and from before a reset.
    /// </summary>
    private void LoadLastReading()
    {
        try
        {
            if (!File.Exists(LastReadingFile)) return;
            var saved = JsonSerializer.Deserialize<SavedReading>(File.ReadAllText(LastReadingFile));
            if (saved is null || saved.FetchedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return;
            if (UsageParser.Parse(saved.Body) is not { } reading) return;
            (Reading, ReadAt) = (reading, saved.FetchedAt);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Write($"Uložené využití nejde načíst ({e.GetType().Name}).");
        }
    }

    private static void SaveLastReading(string? body, DateTimeOffset at)
    {
        if (body is null) return;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(LastReadingFile, JsonSerializer.Serialize(new SavedReading(at, body)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Využití nejde uložit ({e.GetType().Name}).");
        }
    }

    private static void SaveBody(string? body)
    {
        if (body is null) return;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(Path.Combine(AppPaths.DataDir, "last-response.json"), body);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Odpověď nejde uložit ({e.GetType().Name}).");
        }
    }

    private static string Show(LimitReading? limit) =>
        limit is null ? "—" : limit.Percent.ToString("0.#", CultureInfo.InvariantCulture) + " %";
}
