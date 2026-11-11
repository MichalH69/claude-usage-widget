using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClaudeUsageWidget.Data;

/// <summary>A newer version on GitHub: its number and where its exe lies.</summary>
internal sealed record UpdateOffer(Version Version, Uri Download, string? Sha256);

/// <summary>
/// Looks on GitHub for a newer release of the widget and, when asked to, puts it in place of the running exe.
/// Nothing is installed without the user's click. The download comes only from the repository's own release, over
/// HTTPS, and is checked against the SHA-256 GitHub lists for it before it replaces anything.
/// </summary>
internal static class Updater
{
    /// <summary>The GitHub repository the releases come from, as owner/name.</summary>
    public const string Repository = "MichalH69/claude-usage-widget";

    private const string AssetName = "ClaudeUsageWidget.exe";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        // GitHub's API turns away requests that do not name their client.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("claude-usage-widget");
        return http;
    }

    /// <summary>This build's version, as the project file states it.</summary>
    public static Version Current { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    /// <summary>The latest release if it is newer than this build, otherwise null. Never throws.</summary>
    public static async Task<UpdateOffer?> CheckAsync()
    {
#if DEMO
        // The test build never goes on the network.
        await Task.CompletedTask;
        return null;
#else
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement release = doc.RootElement;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out Version? version)) return null;
            version = Normalize(version);
            if (version <= Current) return null;

            foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != AssetName) continue;
                var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "");
                if (url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com") return null;

                string? sha = asset.TryGetProperty("digest", out JsonElement digest) && digest.GetString() is { } d
                    && d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? d[7..] : null;
                return new UpdateOffer(version, url, sha);
            }
            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException
            or KeyNotFoundException or InvalidOperationException or UriFormatException)
        {
            Log.Write($"Kontrola aktualizací se nepovedla ({e.GetType().Name}).");
            return null;
        }
#endif
    }

    /// <summary>
    /// Downloads the new exe beside the running one, checks it, swaps the two and starts the new one, which waits
    /// for this one to end. Returns true when the new exe has started and this one should close.
    /// </summary>
    public static async Task<bool> InstallAsync(UpdateOffer offer)
    {
#if DEMO
        await Task.CompletedTask;
        return false;
#else
        string? exe = Environment.ProcessPath;
        if (exe is null) return false;
        string folder = Path.GetDirectoryName(exe)!;
        string fresh = Path.Combine(folder, "ClaudeUsageWidget.new.exe");
        string old = exe + ".old";

        try
        {
            byte[] bytes = await Http.GetByteArrayAsync(offer.Download);
            if (bytes.Length < 64 * 1024 || bytes[0] != 'M' || bytes[1] != 'Z')
            {
                Log.Write("Stažená aktualizace nevypadá jako program, nepoužita.");
                return false;
            }
            if (offer.Sha256 is { } expected
                && !Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                Log.Write("Stažená aktualizace nesouhlasí s kontrolním součtem, nepoužita.");
                return false;
            }
            await File.WriteAllBytesAsync(fresh, bytes);

            // A running exe cannot be overwritten, but it can be renamed out of the way.
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            File.Move(fresh, exe);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            Log.Write($"Aktualizace se nepovedla ({e.GetType().Name}).");
            TryDelete(fresh);
            if (!File.Exists(exe) && File.Exists(old)) File.Move(old, exe);
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(exe) { UseShellExecute = false };
            start.ArgumentList.Add("--replaces");
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(start);
            Log.Write($"Aktualizováno na verzi {Format(offer.Version)}, spouštím ji.");
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // The new exe will not start: put the old one back.
            Log.Write($"Nová verze nejde spustit ({e.GetType().Name}), vracím původní.");
            TryDelete(exe);
            File.Move(old, exe);
            return false;
        }
#endif
    }

    /// <summary>
    /// Run first thing by a freshly updated exe: waits for the version it replaced to end, so the two never run at
    /// once, and clears away the old file.
    /// </summary>
    public static void FinishReplacing(string[] args)
    {
        int at = Array.IndexOf(args, "--replaces");
        if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int pid))
        {
            try
            {
                using Process previous = Process.GetProcessById(pid);
                previous.WaitForExit(15_000);
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }
        if (Environment.ProcessPath is { } exe) TryDelete(exe + ".old");
    }

    public static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Still in use or locked; it is tried again on the next start.
        }
    }
}
