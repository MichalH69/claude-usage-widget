using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClaudeUsageWidget.Data;
using ClaudeUsageWidget.Model;

namespace ClaudeUsageWidget.ViewModels;

/// <summary>What the skins bind to: the three limits and a word about how fresh they are.</summary>
public sealed class WidgetViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);
    private static readonly string[] Days = { "ne", "po", "út", "st", "čt", "pá", "so" };

    private LimitView session = LimitView.Empty, weekly = LimitView.Empty, fable = LimitView.Empty;
    private string updatedText = "načítám…", statusLine = "Načítám využití…";
    private string? problem;
    private bool isStale = true;

    public LimitView Session { get => session; private set => Set(ref session, value); }
    public LimitView Weekly { get => weekly; private set => Set(ref weekly, value); }
    public LimitView Fable { get => fable; private set => Set(ref fable, value); }

    /// <summary>A few words for the skin: the time of the last reading, or why there is none.</summary>
    public string UpdatedText { get => updatedText; private set => Set(ref updatedText, value); }

    /// <summary>The same as a full sentence, for the tooltip.</summary>
    public string StatusLine { get => statusLine; private set => Set(ref statusLine, value); }

    /// <summary>What is wrong, for the tooltip. Null while everything works, so no tooltip covers the widget.</summary>
    public string? Problem { get => problem; private set => Set(ref problem, value); }

    /// <summary>The values are old enough that they should not be trusted at a glance.</summary>
    public bool IsStale { get => isStale; private set => Set(ref isStale, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(UsagePoller poller) =>
        Update(poller.Reading, poller.ReadAt, poller.Status, poller.HasPolled, DateTimeOffset.Now);

    public void Update(UsageReading? reading, DateTimeOffset? readAt, FetchStatus status, bool hasPolled, DateTimeOffset now)
    {
        Session = View(reading?.Session, readAt, now, SessionWindow);
        Weekly = View(reading?.Weekly, readAt, now, WeekWindow);
        Fable = View(reading?.Fable, readAt, now, WeekWindow);
        IsStale = readAt is not { } at || now - at > StaleAfter;

        string? time = readAt?.ToLocalTime().ToString("H:mm");
        string since = time is null ? "" : $" Poslední data z {time}.";
        (UpdatedText, StatusLine) = (hasPolled ? status : FetchStatus.Ok) switch
        {
            FetchStatus.Ok when time is null => ("načítám…", "Načítám využití…"),
            FetchStatus.Ok => (time!, $"Aktualizováno v {time}."),
            FetchStatus.TokenExpired or FetchStatus.Unauthorized =>
                ("čeká na Claude Code", "Přihlášení vypršelo. Widget ho na pozadí obnovuje přes Claude Code a zkouší to znovu." + since),
            FetchStatus.LoginEnded =>
                ("přihlas se v Claude Code", "Přihlášení Claude Code skončilo. Otevři Claude Code a přihlas se znovu." + since),
            FetchStatus.NoCredentials => ("Claude Code není přihlášený", "Claude Code není přihlášený, není odkud číst." + since),
            FetchStatus.Offline => ("bez připojení", "Bez připojení k internetu." + since),
            FetchStatus.UnknownFormat => ("formát se změnil", "Odpověď serveru má jiný tvar než dřív, widget potřebuje úpravu." + since),
            FetchStatus.RateLimited => (time ?? "čekám na server", "Server omezil dotazy, zkusím to později." + since),
            _ => (time ?? "server neodpovídá", "Server vrátil chybu, zkusím to znovu." + since),
        };
        Problem = hasPolled && status != FetchStatus.Ok ? StatusLine : null;
    }

    /// <summary>How long each limit's window runs from one reset to the next.</summary>
    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5), WeekWindow = TimeSpan.FromDays(7);

    private static LimitView View(LimitReading? reading, DateTimeOffset? readAt, DateTimeOffset now, TimeSpan window)
    {
        if (reading is null) return LimitView.Empty;
        if (reading.ResetsAt is not { } resetsAt) return new LimitView(reading.Percent, "");
        if (resetsAt > now)
        {
            return new LimitView(reading.Percent, ResetText(resetsAt, now))
            {
                Countdown = Countdown(resetsAt, now),
                ResetClock = Clock(resetsAt, now),
                TimeLeft = Math.Clamp((resetsAt - now) / window, 0, 1),
            };
        }

        // The window ran out after the reading was taken, so the limit is empty until something draws on it again.
        return readAt is { } at && at < resetsAt
            ? new LimitView(0, "po resetu", IsEstimate: true) { Countdown = "po resetu", TimeLeft = 1 }
            : new LimitView(reading.Percent, "");
    }

    internal static string ResetText(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        TimeSpan left = resetsAt - now;
        if (left < TimeSpan.FromMinutes(1)) return "za chvíli";
        if (left < TimeSpan.FromHours(12)) return "za " + Countdown(resetsAt, now);

        DateTime local = resetsAt.ToLocalTime().DateTime;
        return left < TimeSpan.FromDays(6) ? DayAndTime(local) : DateAndTime(local);
    }

    /// <summary>The time left, rounded up: "8 min", "4 h 12 min", and from a day on "2 d 5 h".</summary>
    internal static string Countdown(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        TimeSpan left = resetsAt - now;
        if (left < TimeSpan.FromMinutes(1)) return "za chvíli";

        int minutes = (int)Math.Ceiling(left.TotalMinutes);
        if (minutes >= 24 * 60)
        {
            int hours = (int)Math.Ceiling(left.TotalHours);
            return hours % 24 == 0 ? $"{hours / 24} d" : $"{hours / 24} d {hours % 24} h";
        }

        int h = minutes / 60, m = minutes % 60;
        return h == 0 ? $"{m} min" : m == 0 ? $"{h} h" : $"{h} h {m} min";
    }

    /// <summary>What the clock will say at the reset. The day is named only when the bare time could be taken for today's.</summary>
    internal static string Clock(DateTimeOffset resetsAt, DateTimeOffset now)
    {
        DateTime local = resetsAt.ToLocalTime().DateTime;
        TimeSpan left = resetsAt - now;
        if (left < TimeSpan.FromHours(12) || local.Date == now.ToLocalTime().Date) return $"{local:H:mm}";
        return left < TimeSpan.FromDays(7) ? DayAndTime(local) : DateAndTime(local);
    }

    private static string DayAndTime(DateTime local) => $"{Days[(int)local.DayOfWeek]} {local:H:mm}";

    private static string DateAndTime(DateTime local) => $"{local.Day}. {local.Month}. {local:H:mm}";

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
