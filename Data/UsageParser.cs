using System.Globalization;
using System.Text.Json;

namespace ClaudeUsageWidget.Data;

public sealed record LimitReading(double Percent, DateTimeOffset? ResetsAt);

/// <summary>The three limits the widget shows. A limit the response did not mention is null.</summary>
public sealed record UsageReading(LimitReading? Session, LimitReading? Weekly, LimitReading? Fable)
{
    public bool HasAny => Session is not null || Weekly is not null || Fable is not null;
}

/// <summary>
/// Reads the usage endpoint's response. The endpoint is undocumented, so property names are matched
/// without regard to case and every field is optional.
/// </summary>
public static class UsageParser
{
    /// <summary>Returns null when the text holds none of the known limits.</summary>
    public static UsageReading? Parse(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads the copy of the last response that Claude Code keeps in its state file.</summary>
    public static (UsageReading Reading, DateTimeOffset FetchedAt)? ParseStateFile(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!TryGet(doc.RootElement, "cachedUsageUtilization", out JsonElement cache)) return null;
            if (!TryGet(cache, "utilization", out JsonElement usage)) return null;
            if (!TryGet(cache, "fetchedAtMs", out JsonElement stamp) || ReadTime(stamp) is not { } fetchedAt) return null;

            UsageReading? reading = Parse(usage);
            return reading is null ? null : (reading, fetchedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UsageReading? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        LimitReading? session = null, weekly = null, fable = null;

        if (TryGet(root, "limits", out JsonElement limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in limits.EnumerateArray())
            {
                if (ReadLimit(item) is not { } limit) continue;
                string kind = TryGet(item, "kind", out JsonElement k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : "";

                if (Same(kind, "session")) session ??= limit;
                else if (Same(kind, "weekly_all")) weekly ??= limit;
                else if (Same(kind, "weekly_scoped") && TryGet(item, "scope", out JsonElement scope) && MentionsFable(scope.GetRawText())) fable ??= limit;
            }
        }

        // The older shape: one object per window at the top level.
        session ??= TryGet(root, "five_hour", out JsonElement fiveHour) ? ReadLimit(fiveHour) : null;
        weekly ??= TryGet(root, "seven_day", out JsonElement sevenDay) ? ReadLimit(sevenDay) : null;
        if (fable is null)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (MentionsFable(property.Name) && ReadLimit(property.Value) is { } limit)
                {
                    fable = limit;
                    break;
                }
            }
        }

        var reading = new UsageReading(session, weekly, fable);
        return reading.HasAny ? reading : null;
    }

    private static LimitReading? ReadLimit(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        double? percent = TryGet(item, "percent", out JsonElement p) ? ReadNumber(p) : null;
        percent ??= TryGet(item, "utilization", out JsonElement u) ? ReadNumber(u) : null;
        if (percent is not { } value) return null;

        DateTimeOffset? resetsAt = TryGet(item, "resets_at", out JsonElement r) ? ReadTime(r) : null;
        return new LimitReading(Math.Max(0, value), resetsAt);
    }

    private static double? ReadNumber(JsonElement element)
    {
        double value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetDouble(out value):
                break;
            case JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value):
                break;
            default:
                return null;
        }
        return double.IsFinite(value) ? value : null;
    }

    /// <summary>Accepts an ISO 8601 text or a Unix time in seconds or milliseconds.</summary>
    private static DateTimeOffset? ReadTime(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
                ? parsed
                : null;
        }
        if (ReadNumber(element) is not { } number || number <= 0) return null;

        // Seconds stay below 1e11 until the year 5138. Anything larger is milliseconds.
        double ms = number < 1e11 ? number * 1000 : number;
        return ms < 2.5e14 ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms) : null;
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in obj.EnumerateObject())
            {
                if (Same(property.Name, name))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool MentionsFable(string text) => text.Contains("fable", StringComparison.OrdinalIgnoreCase);
}
