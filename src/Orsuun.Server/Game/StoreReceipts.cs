using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orsuun.Server.Game;

/// <summary>A purchase the store confirmed: its own transaction id (once per store), the product, and whether it was a test.</summary>
public sealed record StoreReceipt(string Store, string TransactionId, string ProductId, bool Sandbox, string? Account = null);

/// <summary>
/// Checks a phone's purchase with the store that sold it (owner, 27 Sep 2026: "Amber store purchases"), so the server alone
/// decides what was paid for:
///   apple:  the phone sends the StoreKit transaction id; the App Store Server API (signed with the In-App Purchase key)
///           returns the transaction, production first, then the sandbox.
///   google: the phone sends the purchase token; the Google Play Developer API (a service account) returns the purchase,
///           which is then consumed so the pack can be bought again.
///   test:   a Development server only: "test-..." receipts, for smoke tests and the editor's fake store.
/// Settings: Store:Apple:BundleId / IssuerId / KeyId / PrivateKey (the .p8 text) or PrivateKeyFile, Store:Google:PackageName /
/// ServiceAccountJson (the key file's text) or ServiceAccountJsonFile. A store with no settings says so ("store_closed").
/// </summary>
public sealed class StoreReceipts
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly bool _development;
    private string? _googleToken;
    private DateTime _googleTokenUntil;

    public StoreReceipts(IConfiguration config, IHostEnvironment env, IHttpClientFactory http)
    {
        _config = config;
        _http = http.CreateClient("stores");
        _development = env.IsDevelopment();
    }

    /// <summary>A setting's text, or the text of the file its "...File" twin names (keys too long for an env variable).</summary>
    private string? Setting(string key)
    {
        if (!string.IsNullOrWhiteSpace(_config[key])) return _config[key];
        string? file = _config[key + "File"];
        return !string.IsNullOrWhiteSpace(file) && File.Exists(file) ? File.ReadAllText(file) : null;
    }

    public bool AppleReady => Setting("Store:Apple:IssuerId") != null && Setting("Store:Apple:KeyId") != null && Setting("Store:Apple:PrivateKey") != null;
    public bool GoogleReady => Setting("Store:Google:ServiceAccountJson") != null;

    public async Task<StoreReceipt> CheckAsync(string store, string productId, string receipt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 8000) throw new GameException("bad_receipt", "That purchase could not be read.");
        switch (store)
        {
            case "test":
                if (!_development) throw new GameException("bad_store", "Unknown store.");
                if (!receipt.StartsWith("test-", StringComparison.Ordinal)) throw new GameException("bad_receipt", "A test receipt starts with test-.");
                return new StoreReceipt("test", receipt, productId, true);
            case "apple":
                if (!AppleReady) throw new GameException("store_closed", "App Store purchases are not set up on this server yet.");
                return await AppleAsync(receipt.Trim(), productId, ct);
            case "google":
                if (!GoogleReady) throw new GameException("store_closed", "Google Play purchases are not set up on this server yet.");
                return await GoogleAsync(receipt.Trim(), productId, ct);
            default:
                throw new GameException("bad_store", "Unknown store.");
        }
    }

    // ---- Apple: App Store Server API ----

    private async Task<StoreReceipt> AppleAsync(string transactionId, string productId, CancellationToken ct)
    {
        if (!transactionId.All(char.IsDigit)) throw new GameException("bad_receipt", "That purchase could not be read.");
        string jwt = AppleToken();
        foreach ((string host, bool sandbox) in new[] { ("api.storekit.itunes.apple.com", false), ("api.storekit-sandbox.itunes.apple.com", true) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/inApps/v1/transactions/{transactionId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) continue;   // a sandbox purchase is not in production
            if (!response.IsSuccessStatusCode) throw new GameException("store_error", "The App Store did not answer. Try again in a moment.");
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string signed = body.RootElement.GetProperty("signedTransactionInfo").GetString() ?? "";
            // Apple's answer over TLS to our signed request: its payload is read as it stands.
            using JsonDocument info = JsonDocument.Parse(JwsPayload(signed));
            JsonElement t = info.RootElement;
            string bundle = t.GetProperty("bundleId").GetString() ?? "";
            if (bundle != (Setting("Store:Apple:BundleId") ?? "com.orsuun.warofbanners")) throw new GameException("bad_receipt", "That purchase is for another app.");
            if (t.TryGetProperty("revocationDate", out _)) throw new GameException("bad_receipt", "That purchase was refunded.");
            string product = t.GetProperty("productId").GetString() ?? "";
            if (product != productId) throw new GameException("bad_receipt", "That purchase is for another product.");
            // The phone names the login it buys for (applicationUsername, a UUID, comes back as appAccountToken).
            string? account = t.TryGetProperty("appAccountToken", out JsonElement token) ? token.GetString() : null;
            return new StoreReceipt("apple", t.GetProperty("transactionId").GetString() ?? transactionId, product, sandbox, account);
        }
        throw new GameException("bad_receipt", "The App Store does not know that purchase.");
    }

    /// <summary>The App Store Server API's bearer token: ES256 with the In-App Purchase key, good for 20 minutes.</summary>
    private string AppleToken()
    {
        var header = new Dictionary<string, object> { ["alg"] = "ES256", ["kid"] = Setting("Store:Apple:KeyId")!, ["typ"] = "JWT" };
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Setting("Store:Apple:IssuerId")!, ["iat"] = now, ["exp"] = now + 1200, ["aud"] = "appstoreconnect-v1",
            ["bid"] = Setting("Store:Apple:BundleId") ?? "com.orsuun.warofbanners",
        };
        return Jwt.Es256(Setting("Store:Apple:PrivateKey")!, header, claims);
    }

    // ---- Google: Play Developer API ----

    private async Task<StoreReceipt> GoogleAsync(string purchaseToken, string productId, CancellationToken ct)
    {
        string package = Setting("Store:Google:PackageName") ?? "com.orsuun.warofbanners";
        string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{package}/purchases/products/"
                     + $"{Uri.EscapeDataString(productId)}/tokens/{Uri.EscapeDataString(purchaseToken)}";
        string access = await GoogleAccessAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using HttpResponseMessage response = await _http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) throw new GameException("bad_receipt", "Google Play does not know that purchase.");
        if (!response.IsSuccessStatusCode) throw new GameException("store_error", "Google Play did not answer. Try again in a moment.");
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement p = body.RootElement;
        // purchaseState 0 = purchased (1 cancelled, 2 pending); purchaseType 0 = a licence tester's test purchase.
        if (!p.TryGetProperty("purchaseState", out JsonElement state) || state.GetInt32() != 0)
            throw new GameException("bad_receipt", "That purchase is not complete.");
        bool test = p.TryGetProperty("purchaseType", out JsonElement type) && type.GetInt32() == 0;
        string order = p.TryGetProperty("orderId", out JsonElement o) && !string.IsNullOrEmpty(o.GetString()) ? o.GetString()! : "token:" + Hash(purchaseToken);
        // Consumed, so the same pack can be bought again; a consume that fails leaves it for the next check (the grant is
        // once per order either way).
        using var consume = new HttpRequestMessage(HttpMethod.Post, url + ":consume");
        consume.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using HttpResponseMessage _ = await _http.SendAsync(consume, ct);
        string? account = p.TryGetProperty("obfuscatedExternalAccountId", out JsonElement acc) ? acc.GetString() : null;
        return new StoreReceipt("google", order, productId, test, account);
    }

    /// <summary>A Play Developer API access token from the service account (JWT bearer grant), reused for 50 minutes.</summary>
    private async Task<string> GoogleAccessAsync(CancellationToken ct)
    {
        if (_googleToken != null && DateTime.UtcNow < _googleTokenUntil) return _googleToken;
        _googleToken = await Jwt.GoogleTokenAsync(_http, Setting("Store:Google:ServiceAccountJson")!, "https://www.googleapis.com/auth/androidpublisher", ct);
        _googleTokenUntil = DateTime.UtcNow.AddMinutes(50);
        return _googleToken!;
    }

    // ---- JWT pieces ----

    public static string JwsPayload(string jws)
    {
        string[] parts = jws.Split('.');
        if (parts.Length != 3) throw new GameException("bad_receipt", "That purchase could not be read.");
        string b64 = parts[1].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
}
