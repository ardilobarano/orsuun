using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orsuun.Server.Game;

/// <summary>The JWT pieces the store checks (StoreReceipts) and the push services (PushSender) sign their calls with.</summary>
public static class Jwt
{
    public static string Sign(Dictionary<string, object> header, Dictionary<string, object> claims, Func<byte[], byte[]> sign)
    {
        string head = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        return head + "." + Base64Url(sign(Encoding.ASCII.GetBytes(head)));
    }

    /// <summary>An ES256 token for Apple (App Store Server API, APNs) with a .p8 key's PEM text.</summary>
    public static string Es256(string pem, Dictionary<string, object> header, Dictionary<string, object> claims)
    {
        using ECDsa key = ECDsa.Create();
        key.ImportFromPem(pem.Replace("\\n", "\n"));
        return Sign(header, claims, data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    /// <summary>A Google API access token for a service account's JSON key and a scope (the JWT bearer grant).</summary>
    public static async Task<string> GoogleTokenAsync(HttpClient http, string serviceAccountJson, string scope, CancellationToken ct)
    {
        using JsonDocument account = JsonDocument.Parse(serviceAccountJson);
        string email = account.RootElement.GetProperty("client_email").GetString()!;
        string pem = account.RootElement.GetProperty("private_key").GetString()!;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using RSA key = RSA.Create();
        key.ImportFromPem(pem);
        string assertion = Sign(new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" },
            new Dictionary<string, object> { ["iss"] = email, ["scope"] = scope, ["aud"] = "https://oauth2.googleapis.com/token", ["iat"] = now, ["exp"] = now + 3600 },
            data => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = assertion,
        });
        using HttpResponseMessage response = await http.PostAsync("https://oauth2.googleapis.com/token", form, ct);
        if (!response.IsSuccessStatusCode) throw new GameException("store_error", "Google did not answer. Try again in a moment.");
        using JsonDocument token = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return token.RootElement.GetProperty("access_token").GetString()!;
    }

    public static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
