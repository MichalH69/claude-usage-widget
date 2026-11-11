using Microsoft.Win32;

namespace ClaudeUsageWidget;

/// <summary>Starting with Windows, through the current user's Run key.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeUsageWidget";

    /// <summary>True when the Run key points at this very exe. An entry for an older copy elsewhere does not count.</summary>
    public static bool IsOn
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string command
                && string.Equals(command.Trim('"'), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Set(bool on)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on && Environment.ProcessPath is { } exe) key.SetValue(ValueName, $"\"{exe}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
