using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageWidget;

internal sealed class Settings
{
    public const string Gauges = "gauges";
    public const string Aquarium = "aquarium";
    public const string Road = "road";

    public string Skin { get; set; } = Aquarium;

    /// <summary>The small version of the widget. A click on the widget switches to the large one and back.</summary>
    public bool Compact { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Topmost { get; set; }

    /// <summary>How much of the desktop shows through the widget, 0 for none. The cog and its row stay solid.</summary>
    public double SeeThrough { get; set; }

    /// <summary>The widget is put away in the notification area and does not show on the desktop.</summary>
    public bool InTray { get; set; }

    /// <summary>Set for test renders, which must not move the real widget or change its skin.</summary>
    [JsonIgnore]
    public bool Transient { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static string FilePath => Path.Combine(AppPaths.DataDir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Write($"Nastavení nejde načíst ({e.GetType().Name}), používám výchozí.");
        }
        return new Settings();
    }

    public void Save()
    {
        if (Transient) return;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Nastavení nejde uložit ({e.GetType().Name}).");
        }
    }
}
