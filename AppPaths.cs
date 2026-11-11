using System.IO;

namespace ClaudeUsageWidget;

internal static class AppPaths
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Set when Claude Code was told to keep its files somewhere other than the home folder.</summary>
    private static string? ConfigDirOverride
    {
        get
        {
            string? dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            return string.IsNullOrWhiteSpace(dir) ? null : dir;
        }
    }

    /// <summary>The widget's own settings, log and saved responses.</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageWidget");

    public static string CredentialsFile => Path.Combine(ConfigDirOverride ?? Path.Combine(Home, ".claude"), ".credentials.json");

    /// <summary>Claude Code's state file. It carries a copy of the last usage response Claude Code fetched itself.</summary>
    public static string ClaudeStateFile => Path.Combine(ConfigDirOverride ?? Home, ".claude.json");
}
