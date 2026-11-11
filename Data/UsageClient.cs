using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClaudeUsageWidget.Data;

public enum FetchStatus
{
    Ok,
    /// <summary>No credentials file, or no token in it: Claude Code is not signed in.</summary>
    NoCredentials,
    /// <summary>The token's own expiry time has passed, so it was not sent at all.</summary>
    TokenExpired,
    /// <summary>The token has expired and so has the refresh token behind it: only signing in to Claude Code again helps.</summary>
    LoginEnded,
    Unauthorized,
    RateLimited,
    Offline,
    ServerError,
    UnknownFormat,
}

public sealed record FetchResult(FetchStatus Status, UsageReading? Reading = null, TimeSpan? RetryAfter = null, string? Body = null);

/// <summary>
/// Asks Anthropic how much of the subscription's limits is used, with the token Claude Code keeps on disk.
/// The token is read fresh for every request, goes only to the host below, and is never written anywhere.
/// </summary>
public sealed class UsageClient
{
    private static readonly Uri Endpoint = new("https://api.anthropic.com/api/oauth/usage");

    // Redirects stay off so the Authorization header cannot follow one to another host.
    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public async Task<FetchResult> FetchAsync()
    {
#if DEMO
        // The test build never reads the login and never asks the server anything.
        await Task.CompletedTask;
        return new FetchResult(FetchStatus.Offline);
#else
        if (ReadToken() is not { } credentials) return new FetchResult(FetchStatus.NoCredentials);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (credentials.ExpiresAt is { } expiry && expiry <= now)
        {
            bool ended = credentials.LoginEndsAt is { } end && end <= now;
            return new FetchResult(ended ? FetchStatus.LoginEnded : FetchStatus.TokenExpired);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
            request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", "claude-usage-widget/0.1");

            using HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                UsageReading? reading = UsageParser.Parse(body);
                return reading is null
                    ? new FetchResult(FetchStatus.UnknownFormat, Body: body)
                    : new FetchResult(FetchStatus.Ok, reading, Body: body);
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new FetchResult(FetchStatus.Unauthorized);
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new FetchResult(FetchStatus.RateLimited, RetryAfter: ReadRetryAfter(response));
            }
            Log.Write($"Server odpověděl HTTP {(int)response.StatusCode}.");
            return new FetchResult(FetchStatus.ServerError);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return new FetchResult(FetchStatus.Offline);
        }
#endif
    }

    /// <summary>When the stored token stops being valid, or null when that cannot be read.</summary>
    public static DateTimeOffset? ReadTokenExpiry() => ReadToken()?.ExpiresAt;

    private static (string Token, DateTimeOffset? ExpiresAt, DateTimeOffset? LoginEndsAt)? ReadToken()
    {
        try
        {
            if (!File.Exists(AppPaths.CredentialsFile)) return null;
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(AppPaths.CredentialsFile));
            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out JsonElement oauth)) return null;
            if (!oauth.TryGetProperty("accessToken", out JsonElement tokenElement)) return null;

            string? token = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(token)) return null;

            return (token, ReadTime(oauth, "expiresAt"), ReadTime(oauth, "refreshTokenExpiresAt"));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            // Claude Code may be rewriting the file at this very moment. The next poll reads it again.
            return null;
        }
    }

    private static DateTimeOffset? ReadTime(JsonElement oauth, string name) =>
        oauth.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out long ms) && ms > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : null;

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is { } date) return date - DateTimeOffset.UtcNow;
        return null;
    }
}
