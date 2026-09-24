using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Sign up and sign in (owner, 24 Sep 2026: "a sign up sign in screen"). Every device logs in as a guest by its
/// device token; SIGN UP saves an email and password to the account being played, SIGN IN points this device at
/// another account (the next guest login lands there), SIGN OUT lets the client start over with a new device token.
/// Each device has its own session, so one account can be played on two phones.
/// </summary>
public sealed partial class GameService
{
    public const int MaxFailedLogins = 8;
    public static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> FailedLogins = new();

    /// <summary>Password hashes: PBKDF2-SHA256, 210,000 iterations, 16-byte salt, stored as "pbkdf2-sha256$iter$salt$hash".</summary>
    public static class Passwords
    {
        private const int Iterations = 210_000;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
            return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out int iterations)) return false;
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }

    private static int RecentFailures(string key)
    {
        if (!FailedLogins.TryGetValue(key, out var queue)) return 0;
        DateTime cutoff = DateTime.UtcNow - FailedLoginWindow;
        while (queue.TryPeek(out DateTime first) && first < cutoff) queue.TryDequeue(out _);
        return queue.Count;
    }

    private static void RecordFailure(string key) => FailedLogins.GetOrAdd(key, _ => new ConcurrentQueue<DateTime>()).Enqueue(DateTime.UtcNow);

    /// <summary>Points the device at the account with a fresh session for it.</summary>
    private async Task<string> BindDeviceAsync(string deviceToken, Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        Device? device = await _db.Devices.FirstOrDefaultAsync(d => d.Token == deviceToken, ct);
        if (device == null)
        {
            device = new Device { Token = deviceToken, CreatedUtc = now };
            _db.Devices.Add(device);
        }
        device.AccountId = account.Id;
        device.SessionToken = NewToken();
        device.LastSeenUtc = now;
        return device.SessionToken;
    }

    public async Task<GuestLoginResponse> LoginAsync(LoginRequest request, string? clientIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceToken) || request.DeviceToken.Length > 128)
            throw new GameException("bad_device_token", "Device token missing or too long.");
        string email = AccountRules.NormaliseEmail(request.Email);
        string ipKey = "ip:" + (clientIp ?? "?");
        if (RecentFailures("email:" + email) >= MaxFailedLogins || RecentFailures(ipKey) >= MaxFailedLogins * 4)
            throw new GameException("login_wait", "Too many tries. Wait a few minutes and try again.");

        Account? account = await _db.Accounts.SingleOrDefaultAsync(a => a.Email == email, ct);
        if (account?.PasswordHash == null || !Passwords.Verify(request.Password ?? "", account.PasswordHash))
        {
            RecordFailure("email:" + email);
            RecordFailure(ipKey);
            throw new GameException("bad_login", "Wrong email or password.");
        }
        if (account.GuildId is Guid guildId) _guild = await _db.Guilds.FindAsync(new object[] { guildId }, ct);
        string session = await BindDeviceAsync(request.DeviceToken, account, ct);
        _db.Ledger.Add(Entry(account.Id, null, "login", "email sign in", 0, Guid.NewGuid().ToString("N")));
        await _db.SaveChangesAsync(ct);
        return new GuestLoginResponse(account.Id, session, false);
    }

    public async Task<StateDto> RegisterAsync(Account account, RegisterRequest request, CancellationToken ct)
    {
        if (account.Email != null) throw new GameException("registered", "This account already has an email: " + account.Email);
        string email = AccountRules.NormaliseEmail(request.Email);
        if (AccountRules.EmailProblem(email) is string emailProblem) throw new GameException("bad_email", emailProblem);
        if (AccountRules.PasswordProblem(request.Password) is string passwordProblem) throw new GameException("bad_password", passwordProblem);
        if (await _db.Accounts.AnyAsync(a => a.Email == email, ct))
            throw new GameException("email_taken", "An account with that email exists. Sign in instead.");
        account.Email = email;
        account.PasswordHash = Passwords.Hash(request.Password);
        _db.Ledger.Add(Entry(account.Id, null, "register", "email saved", 0, Guid.NewGuid().ToString("N")));
        try { await SaveAsync(ct); }
        catch (DbUpdateException) { throw new GameException("email_taken", "An account with that email exists. Sign in instead."); }
        return ToState(account);
    }

    /// <summary>Ends this device's session; the client then starts a new guest with a new device token.</summary>
    public async Task SignOutAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return;
        await _db.Devices.Where(d => d.SessionToken == sessionToken).ExecuteDeleteAsync(ct);
        await _db.Accounts.Where(a => a.SessionToken == sessionToken).ExecuteUpdateAsync(s => s.SetProperty(a => a.SessionToken, (string?)null), ct);
    }
}
