using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Phone pushes (owner, 27 Sep 2026: "Phone notifications": an Exchange piece sold, a letter arrived, the guild's raid
/// boss fell). Everything owed to a hero who is not the request's own comes by letter (the Exchange pays, a listing comes
/// home, a raid pays its fighters), so a letter is what pushes: GameService queues one after its save, and this sends it in
/// the background to every phone of the hero's login that registered a token (/v1/push-token), at most one a kind every
/// ten minutes. Apple through APNs (token auth: Push:Apple:TeamId / KeyId / PrivateKey or PrivateKeyFile / BundleId),
/// Android through Firebase Cloud Messaging (Push:Google:ServiceAccountJson or ServiceAccountJsonFile). With no keys a
/// push is dropped; a token the service no longer knows is deleted.
/// </summary>
public sealed class PushSender : BackgroundService
{
    public sealed record Push(Guid AccountId, string Kind, string Title, string Body);

    private readonly Channel<Push> _queue = Channel.CreateBounded<Push>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly ConcurrentDictionary<string, DateTime> _last = new();
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<PushSender> _log;
    private string? _appleJwt, _googleToken;
    private DateTime _appleUntil, _googleUntil;

    public PushSender(IServiceScopeFactory scopes, IConfiguration config, IHttpClientFactory http, ILogger<PushSender> log)
    {
        _scopes = scopes;
        _config = config;
        _http = http.CreateClient("push");
        _log = log;
    }

    private string? Setting(string key)
    {
        if (!string.IsNullOrWhiteSpace(_config[key])) return _config[key];
        string? file = _config[key + "File"];
        return !string.IsNullOrWhiteSpace(file) && File.Exists(file) ? File.ReadAllText(file) : null;
    }

    private bool AppleReady => Setting("Push:Apple:TeamId") != null && Setting("Push:Apple:KeyId") != null && Setting("Push:Apple:PrivateKey") != null;
    private bool GoogleReady => Setting("Push:Google:ServiceAccountJson") != null;

    /// <summary>Queues a push (after the request's save); a kind already pushed to that hero in the last ten minutes waits.</summary>
    public void Queue(Push push)
    {
        if (!AppleReady && !GoogleReady) return;
        string key = push.AccountId.ToString("N") + push.Kind;
        DateTime now = DateTime.UtcNow;
        if (_last.TryGetValue(key, out DateTime last) && now - last < TimeSpan.FromMinutes(10)) return;
        _last[key] = now;
        _queue.Writer.TryWrite(push);
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await foreach (Push push in _queue.Reader.ReadAllAsync(stop))
        {
            try { await SendAsync(push, stop); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "push failed"); }
        }
    }

    private async Task SendAsync(Push push, CancellationToken ct)
    {
        using IServiceScope scope = _scopes.CreateScope();
        GameDb db = scope.ServiceProvider.GetRequiredService<GameDb>();
        Guid? login = await db.Accounts.Where(a => a.Id == push.AccountId).Select(a => (Guid?)a.LoginId).FirstOrDefaultAsync(ct);
        if (login == null) return;
        List<PushToken> tokens = await db.PushTokens.Where(t => t.LoginId == login).ToListAsync(ct);
        foreach (PushToken token in tokens)
        {
            bool known = token.Platform == "ios" ? await AppleAsync(token, push, ct) : await GoogleAsync(token, push, ct);
            if (!known) db.PushTokens.Remove(token);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>APNs over HTTP/2; false when Apple says the token is gone. A token from a development build lives on the
    /// sandbox: a production "bad token" is tried there once and remembered.</summary>
    private async Task<bool> AppleAsync(PushToken token, Push push, CancellationToken ct)
    {
        if (!AppleReady) return true;
        string body = JsonSerializer.Serialize(new { aps = new { alert = new { title = push.Title, body = push.Body }, sound = "default" } });
        foreach (bool sandbox in token.Sandbox ? new[] { true } : new[] { false, true })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://{(sandbox ? "api.sandbox.push.apple.com" : "api.push.apple.com")}/3/device/{token.Token}")
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", AppleJwt());
            request.Headers.Add("apns-topic", Setting("Push:Apple:BundleId") ?? "com.orsuun.warofbanners");
            request.Headers.Add("apns-push-type", "alert");
            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                token.Sandbox = sandbox;
                return true;
            }
            string reason = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.Gone || reason.Contains("Unregistered")) return false;
            if (!reason.Contains("BadDeviceToken")) return true;   // a passing fault: the token stays
        }
        return false;
    }

    private string AppleJwt()
    {
        if (_appleJwt != null && DateTime.UtcNow < _appleUntil) return _appleJwt;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _appleJwt = Jwt.Es256(Setting("Push:Apple:PrivateKey")!, new Dictionary<string, object> { ["alg"] = "ES256", ["kid"] = Setting("Push:Apple:KeyId")! },
            new Dictionary<string, object> { ["iss"] = Setting("Push:Apple:TeamId")!, ["iat"] = now });
        _appleUntil = DateTime.UtcNow.AddMinutes(40);   // APNs wants a new one within the hour
        return _appleJwt;
    }

    /// <summary>Firebase Cloud Messaging (HTTP v1); false when Firebase says the token is gone.</summary>
    private async Task<bool> GoogleAsync(PushToken token, Push push, CancellationToken ct)
    {
        if (!GoogleReady) return true;
        string json = Setting("Push:Google:ServiceAccountJson")!;
        string project;
        using (JsonDocument doc = JsonDocument.Parse(json)) project = doc.RootElement.GetProperty("project_id").GetString()!;
        if (_googleToken == null || DateTime.UtcNow >= _googleUntil)
        {
            _googleToken = await Jwt.GoogleTokenAsync(_http, json, "https://www.googleapis.com/auth/firebase.messaging", ct);
            _googleUntil = DateTime.UtcNow.AddMinutes(50);
        }
        string body = JsonSerializer.Serialize(new { message = new { token = token.Token, notification = new { title = push.Title, body = push.Body } } });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{project}/messages:send")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _googleToken);
        using HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return true;
        string reason = await response.Content.ReadAsStringAsync(ct);
        return !(response.StatusCode == HttpStatusCode.NotFound || reason.Contains("UNREGISTERED"));
    }
}
