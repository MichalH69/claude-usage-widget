using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ClaudeUsageWidget.Data;

/// <summary>
/// Lets Claude Code renew its own token, so Claude Code never has to be opened for the widget's sake.
/// The widget never renews the token itself: the refresh token changes on use, and two programs
/// renewing at once would sign Claude Code out.
/// </summary>
/// <remarks>
/// Measured with Claude Code 2.1.318 against a made-up login behind a proxy that refused every
/// connection: "claude auth status" asks the token server for a new token once the stored one has
/// under about five minutes left (3 min: yes, 9 min: no), and leaves a fresher one alone.
/// </remarks>
internal static class ClaudeCli
{
    // The wait after the first, second, third and every later failed try in a row. The first ones are
    // short because the usual cause is a network that is not up yet after the PC wakes; a login that is
    // really gone gets one try an hour.
    private static readonly TimeSpan[] Gaps =
    {
        TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1),
    };

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static DateTimeOffset lastRun = DateTimeOffset.MinValue;
    private static int failures;

    private static TimeSpan Gap => Gaps[Math.Clamp(failures - 1, 0, Gaps.Length - 1)];

    /// <summary>
    /// Runs "claude auth status" out of sight. It sends no message, so it uses nothing and starts no
    /// session window. Returns true when the stored token's expiry moved forward.
    /// </summary>
    public static async Task<bool> TryRenewAsync()
    {
#if DEMO
        // The test build never starts Claude Code.
        await Task.CompletedTask;
        return false;
#endif
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - lastRun < Gap) return false;
        lastRun = now;

        DateTimeOffset? before = UsageClient.ReadTokenExpiry();
        bool renewed = false;
        if (await RunAuthStatusAsync())
        {
            DateTimeOffset? after = UsageClient.ReadTokenExpiry();
            renewed = after is { } expiry && expiry > DateTimeOffset.UtcNow && after != before;
        }

        failures = renewed ? 0 : failures + 1;
        Log.Write(renewed
            ? "Claude Code token obnovil, expirace se posunula."
            : $"Claude Code token neobnovil, další pokus za {Gap.TotalMinutes:0} min.");
        return renewed;
    }

    /// <summary>True when the command ran to its end, whatever it reported.</summary>
    private static async Task<bool> RunAuthStatusAsync()
    {
        string? exe = Find();
        if (exe is null)
        {
            Log.Write("Claude Code CLI nenalezeno, token nemá kdo obnovit.");
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("auth");
            start.ArgumentList.Add("status");

            // A widget started from inside a Claude Code session carries that session's variables.
            // The helper must not take itself for a part of that session or for another kind of login,
            // so only the setting that says where the login file lives is passed on.
            foreach (string name in start.Environment.Keys.ToList())
            {
                bool claudes = name.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("ANTHROPIC", StringComparison.OrdinalIgnoreCase);
                if (claudes && !name.Equals("CLAUDE_CONFIG_DIR", StringComparison.OrdinalIgnoreCase))
                {
                    start.Environment.Remove(name);
                }
            }

            using Process? process = Process.Start(start);
            if (process is null) return false;
            process.StandardInput.Close();

            // The output names the account and is of no use here, so it is drained and dropped.
            Task drained = Task.WhenAll(
                process.StandardOutput.BaseStream.CopyToAsync(Stream.Null),
                process.StandardError.BaseStream.CopyToAsync(Stream.Null));

            using var timeout = new CancellationTokenSource(Patience);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await drained;
                return true;
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Log.Write("Claude Code CLI neodpovědělo do 30 s, ukončeno.");
                return false;
            }
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            Log.Write($"Claude Code CLI nejde spustit ({e.GetType().Name}).");
            return false;
        }
    }

    private static string? Find()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string native = Path.Combine(home, ".local", "bin", "claude.exe");
        if (File.Exists(native)) return native;

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), "claude.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry. Skip it.
            }
        }
        return null;
    }
}
