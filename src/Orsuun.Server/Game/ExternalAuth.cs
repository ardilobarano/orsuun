using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Orsuun.Server.Game;

/// <summary>A verified identity from Google or Apple (or the dev stand-in on a Development server).</summary>
public sealed record ExternalIdentity(string Provider, string Subject, string? Email);

/// <summary>
/// Sign in with Google and Apple (owner, 24 Sep 2026). The game opens the provider's sign-in page in the system
/// browser (an in-app sheet on iOS); the provider sends the player back to this server, which verifies the signed ID
/// token against the provider's published keys and hands the app a one-time ticket through an orsuun:// link. The
/// ticket is only good for the device that started the flow, so a sign-in link sent by someone else cannot move a
/// hero. Flows and tickets live in memory for a few minutes (one server).
///
/// Settings: Auth:PublicUrl (https://host), Auth:Google:ClientId / ClientSecret (a Google Cloud "Web application"
/// OAuth client with redirect URI {PublicUrl}/auth/google/callback), Auth:Apple:ServicesId (a Services ID with return
/// URL {PublicUrl}/auth/apple/callback) and Auth:Apple:BundleId (for tokens from the native iOS button later).
/// A provider without settings is simply not offered. On a Development server the "dev" provider stands in.
/// </summary>
public sealed class ExternalAuth
{
    public static readonly TimeSpan FlowLife = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TicketLife = TimeSpan.FromMinutes(5);
    public const string AppScheme = "orsuun";

    public sealed record Flow(string Id, string Provider, Guid AccountId, string DeviceToken, string State, string Nonce, string Verifier, DateTime Expires);
    public sealed record Ticket(string Id, Guid AccountId, string DeviceToken, ExternalIdentity Identity, DateTime Expires);

    private readonly ConcurrentDictionary<string, Flow> _flows = new();
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new();
    private readonly IHttpClientFactory _http;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _google;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _apple;

    public string PublicUrl { get; }
    public string GoogleClientId { get; }
    private readonly string _googleSecret;
    private readonly string[] _googleAudiences;
    public string AppleServicesId { get; }
    private readonly string[] _appleAudiences;
    public bool DevEnabled { get; }

    public ExternalAuth(IConfiguration config, IHostEnvironment env, IHttpClientFactory http)
    {
        _http = http;
        PublicUrl = (config["Auth:PublicUrl"] ?? "http://localhost:5080").TrimEnd('/');
        GoogleClientId = config["Auth:Google:ClientId"] ?? "";
        _googleSecret = config["Auth:Google:ClientSecret"] ?? "";
        // Tokens from native Google buttons carry their own client ids; list them in Auth:Google:Audiences.
        _googleAudiences = new[] { GoogleClientId }.Concat((config["Auth:Google:Audiences"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(a => a.Length > 0).ToArray();
        AppleServicesId = config["Auth:Apple:ServicesId"] ?? "";
        _appleAudiences = new[] { AppleServicesId, config["Auth:Apple:BundleId"] ?? "" }.Where(a => a.Length > 0).ToArray();
        DevEnabled = env.IsDevelopment();
        _google = new ConfigurationManager<OpenIdConnectConfiguration>("https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
        _apple = new ConfigurationManager<OpenIdConnectConfiguration>("https://appleid.apple.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
    }

    public bool Enabled(string provider) => provider switch
    {
        "google" => GoogleClientId.Length > 0 && _googleSecret.Length > 0,
        "apple" => AppleServicesId.Length > 0,
        "dev" => DevEnabled,
        _ => false,
    };

    public string[] Providers => new[] { "apple", "google", "dev" }.Where(Enabled).ToArray();

    public static string Name(string provider) => provider switch { "google" => "Google", "apple" => "Apple", _ => "test" };

    private static string Random(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private void Sweep()
    {
        DateTime now = DateTime.UtcNow;
        foreach (var f in _flows) if (f.Value.Expires < now) _flows.TryRemove(f.Key, out _);
        foreach (var t in _tickets) if (t.Value.Expires < now) _tickets.TryRemove(t.Key, out _);
    }

    public Flow Begin(string provider, Guid accountId, string deviceToken)
    {
        if (!Enabled(provider)) throw new GameException("provider_off", Name(provider) + " sign-in is not set up yet.");
        Sweep();
        var flow = new Flow(Random(18), provider, accountId, deviceToken, Random(24), Random(24), Random(32), DateTime.UtcNow + FlowLife);
        _flows[flow.Id] = flow;
        return flow;
    }

    public Flow? FlowById(string? id) => id != null && _flows.TryGetValue(id, out Flow? f) && f.Expires > DateTime.UtcNow ? f : null;

    /// <summary>The flow a provider's answer belongs to, taken so a state works once.</summary>
    public Flow? TakeByState(string? state)
    {
        if (string.IsNullOrEmpty(state)) return null;
        foreach (var pair in _flows)
            if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pair.Value.State), Encoding.UTF8.GetBytes(state)))
                return _flows.TryRemove(pair.Key, out Flow? f) && f.Expires > DateTime.UtcNow ? f : null;
        return null;
    }

    /// <summary>Where the browser goes to sign in.</summary>
    public string AuthorizeUrl(Flow flow)
    {
        string E(string s) => Uri.EscapeDataString(s);
        switch (flow.Provider)
        {
            case "google":
                string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(flow.Verifier)));
                return "https://accounts.google.com/o/oauth2/v2/auth?response_type=code&scope=openid%20email&prompt=select_account"
                       + $"&client_id={E(GoogleClientId)}&redirect_uri={E(PublicUrl + "/auth/google/callback")}&state={E(flow.State)}&nonce={E(flow.Nonce)}"
                       + $"&code_challenge={E(challenge)}&code_challenge_method=S256";
            case "apple":
                return "https://appleid.apple.com/auth/authorize?response_type=code%20id_token&response_mode=form_post&scope=email"
                       + $"&client_id={E(AppleServicesId)}&redirect_uri={E(PublicUrl + "/auth/apple/callback")}&state={E(flow.State)}&nonce={E(flow.Nonce)}";
            default:
                return $"{PublicUrl}/auth/dev/authorize?state={E(flow.State)}";
        }
    }

    /// <summary>Google: trades the code for tokens (with the PKCE verifier) and returns the ID token.</summary>
    public async Task<string> GoogleIdTokenAsync(string code, Flow flow, CancellationToken ct)
    {
        using HttpClient client = _http.CreateClient();
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = GoogleClientId, ["client_secret"] = _googleSecret, ["redirect_uri"] = PublicUrl + "/auth/google/callback",
            ["grant_type"] = "authorization_code", ["code_verifier"] = flow.Verifier,
        }), ct);
        string body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new GameException("provider_failed", "Google did not accept the sign-in. Try again.");
        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id_token", out JsonElement token) ? token.GetString() ?? "" : throw new GameException("provider_failed", "Google sent no identity.");
    }

    /// <summary>
    /// Checks a provider's ID token: signature against its published keys, issuer, audience (our client ids), lifetime,
    /// and the nonce (as sent, or its SHA-256 hex, which native Apple buttons use).
    /// </summary>
    public async Task<ExternalIdentity> VerifyAsync(string provider, string idToken, string? nonce, CancellationToken ct)
    {
        if (provider == "dev")
        {
            if (!DevEnabled) throw new GameException("provider_off", "Unknown sign-in.");
            string[] parts = idToken.Split('|');
            if (parts.Length < 2 || parts[0] != "dev" || parts[1].Length == 0) throw new GameException("bad_token", "Bad test identity.");
            return new ExternalIdentity("dev", parts[1], parts.Length > 2 ? parts[2] : null);
        }
        if (!Enabled(provider) && !(provider == "apple" && _appleAudiences.Length > 0)) throw new GameException("provider_off", Name(provider) + " sign-in is not set up yet.");
        ConfigurationManager<OpenIdConnectConfiguration> manager = provider == "google" ? _google : _apple;
        OpenIdConnectConfiguration config = await manager.GetConfigurationAsync(ct);
        var parameters = new TokenValidationParameters
        {
            ValidIssuers = provider == "google" ? new[] { "https://accounts.google.com", "accounts.google.com" } : new[] { "https://appleid.apple.com" },
            ValidAudiences = provider == "google" ? _googleAudiences : _appleAudiences,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid) throw new GameException("bad_token", Name(provider) + " sign-in could not be verified. Try again.");
        string? Claim(string type) => result.Claims.TryGetValue(type, out object? v) ? v?.ToString() : null;
        if (nonce != null)
        {
            string got = Claim("nonce") ?? "";
            string hashed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce))).ToLowerInvariant();
            if (got != nonce && got != hashed) throw new GameException("bad_token", "That sign-in was not started here.");
        }
        string subject = Claim("sub") ?? throw new GameException("bad_token", "No account id in the sign-in.");
        return new ExternalIdentity(provider, subject, Claim("email"));
    }

    public string IssueTicket(Flow flow, ExternalIdentity identity)
    {
        var ticket = new Ticket(Random(24), flow.AccountId, flow.DeviceToken, identity, DateTime.UtcNow + TicketLife);
        _tickets[ticket.Id] = ticket;
        return ticket.Id;
    }

    /// <summary>A ticket, once, and only for the device that started its flow.</summary>
    public Ticket? Redeem(string? id, string? deviceToken)
    {
        if (string.IsNullOrEmpty(id) || !_tickets.TryRemove(id, out Ticket? ticket) || ticket.Expires < DateTime.UtcNow) return null;
        return string.Equals(ticket.DeviceToken, deviceToken, StringComparison.Ordinal) ? ticket : null;
    }

    /// <summary>The page the provider lands the browser on: it hands the ticket to the app through an orsuun:// link.</summary>
    public static string ReturnPage(string? ticket, string? error)
    {
        string link = ticket != null ? $"{AppScheme}://auth?ticket={Uri.EscapeDataString(ticket)}" : $"{AppScheme}://auth?error={Uri.EscapeDataString(error ?? "failed")}";
        string text = ticket != null ? "Signed in. Returning to Orsuun..." : System.Net.WebUtility.HtmlEncode(error ?? "Sign-in failed.");
        return "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
               + $"<meta http-equiv=\"refresh\" content=\"0;url={System.Net.WebUtility.HtmlEncode(link)}\"><title>Orsuun</title>"
               + "<style>body{background:#12111c;color:#f2e8d2;font:17px Georgia,serif;text-align:center;padding:18vh 20px}"
               + "a{display:inline-block;margin-top:24px;padding:14px 26px;border:1px solid #c28f45;border-radius:8px;color:#ffd66b;text-decoration:none}</style></head>"
               + $"<body><p>{text}</p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Back to Orsuun</a></body></html>";
    }
}
