namespace ClaudeUsageWidget.Model;

/// <summary>
/// One limit as a skin shows it. Percent is NaN while there is no data.
/// IsEstimate marks a value worked out rather than read: zero after a reset the widget only saw on the clock.
/// ResetText is the next reset in the aquarium's words: a countdown on the last half day, a day and time before that.
/// </summary>
public sealed record LimitView(double Percent, string ResetText, bool IsEstimate = false)
{
    /// <summary>The time left until the reset, such as "4 h 12 min". The gauges show it.</summary>
    public string Countdown { get; init; } = "";

    /// <summary>What the clock will say at the reset, such as "2:00" or "čt 15:00". The gauges show it under the pointer.</summary>
    public string ResetClock { get; init; } = "";

    /// <summary>How much of the limit's window is still to run before the reset, from 1 just after one to 0 at the next. NaN when unknown.</summary>
    public double TimeLeft { get; init; } = double.NaN;

    public const double WarnAt = 75;
    public const double CriticalAt = 90;

    public static LimitView Empty { get; } = new(double.NaN, "");

    /// <summary>"ok", "warn" or "critical". Skins switch colours on it.</summary>
    public string Level => Percent >= CriticalAt ? "critical" : Percent >= WarnAt ? "warn" : "ok";

    public string PercentText =>
        double.IsNaN(Percent) ? "—" : $"{(IsEstimate ? "~" : "")}{Math.Round(Percent)} %";
}

/// <summary>Fixed values for mockups. The live widget binds skins to WidgetViewModel, which has the same properties.</summary>
public sealed class WidgetState
{
    public required LimitView Session { get; init; }
    public required LimitView Weekly { get; init; }
    public required LimitView Fable { get; init; }
    public string UpdatedText { get; init; } = "";
    public bool IsStale { get; init; }
}
