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

    /// <summary>Points the device at the login and a character (Guid.Empty: the character screen) with a fresh session.</summary>
    private async Task<string> BindDeviceAsync(string deviceToken, Guid loginId, Guid accountId, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        Device? device = await _db.Devices.FirstOrDefaultAsync(d => d.Token == deviceToken, ct);
        if (device == null)
        {
            device = new Device { Token = deviceToken, CreatedUtc = now };
            _db.Devices.Add(device);
        }
        device.LoginId = loginId;
        device.AccountId = accountId;
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

        Login? login = await _db.Logins.SingleOrDefaultAsync(l => l.Email == email, ct);
        if (login?.PasswordHash == null || !Passwords.Verify(request.Password ?? "", login.PasswordHash))
        {
            RecordFailure("email:" + email);
            RecordFailure(ipKey);
            throw new GameException("bad_login", "Wrong email or password.");
        }
        // The device moves to this login, on its most recently played character (clients with the character screen show
        // it anyway; older ones need a hero at once).
        Guid last = await LastPlayedAsync(login.Id, ct);
        string session = await BindDeviceAsync(request.DeviceToken, login.Id, last, ct);
        await _db.SaveChangesAsync(ct);
        int characters = await _db.Accounts.CountAsync(a => a.LoginId == login.Id, ct);
        return new GuestLoginResponse(last, session, false, login.Id, characters);
    }

    /// <summary>Sign up: an email and password for the login (the character screen or the game, 25 Sep 2026).</summary>
    public async Task RegisterAsync(Login login, RegisterRequest request, CancellationToken ct)
    {
        if (login.Email != null) throw new GameException("registered", "This account already has an email: " + login.Email);
        string email = AccountRules.NormaliseEmail(request.Email);
        if (AccountRules.EmailProblem(email) is string emailProblem) throw new GameException("bad_email", emailProblem);
        if (AccountRules.PasswordProblem(request.Password) is string passwordProblem) throw new GameException("bad_password", passwordProblem);
        if (await _db.Logins.AnyAsync(l => l.Email == email, ct))
            throw new GameException("email_taken", "An account with that email exists. Sign in instead.");
        login.Email = email;
        login.PasswordHash = Passwords.Hash(request.Password);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new GameException("email_taken", "An account with that email exists. Sign in instead."); }
    }

    /// <summary>Sign up from the game (older clients): the character's login gets the email.</summary>
    public async Task<StateDto> RegisterAsync(Account account, RegisterRequest request, CancellationToken ct)
    {
        await RegisterAsync(_login ?? throw new GameException("no_login", "Sign in again."), request, ct);
        _db.Ledger.Add(Entry(account.Id, null, "register", "email saved", 0, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account);
    }

    private List<string>? _logins;
    private Guid _loginsFor;

    /// <summary>The Google / Apple logins linked to the character's login (loaded with the account per request).</summary>
    private async Task LoadLoginsAsync(Account account, CancellationToken ct)
    {
        _logins = await _db.ExternalLogins.AsNoTracking().Where(l => l.LoginId == account.LoginId).Select(l => l.Provider).ToListAsync(ct);
        _loginsFor = account.Id;
    }


    private string[]? LoginsOf(Account account) => _loginsFor == account.Id ? _logins?.ToArray() : null;

    /// <summary>The device token behind a session (for a native sign-in, which moves this device).</summary>
    public async Task<string?> DeviceTokenForSessionAsync(string? session, CancellationToken ct) =>
        string.IsNullOrEmpty(session) ? null : await _db.Devices.AsNoTracking().Where(d => d.SessionToken == session).Select(d => d.Token).FirstOrDefaultAsync(ct);

    /// <summary>
    /// A verified Google or Apple identity arrives for the login signed in on a device. Already linked to a login: the
    /// device goes to that login's character screen. Not linked yet: it is linked to this login. A login has at most
    /// one link per provider.
    /// </summary>
    public async Task<ExternalLoginResultDto> LinkOrLoginAsync(Guid currentLoginId, string deviceToken, ExternalIdentity identity, CancellationToken ct)
    {
        ExternalLogin? existing = await _db.ExternalLogins.FirstOrDefaultAsync(l => l.Provider == identity.Provider && l.Subject == identity.Subject, ct);
        if (existing != null)
        {
            if (identity.Email != null) existing.Email = identity.Email;
            bool switched = existing.LoginId != currentLoginId;
            Device? device = await _db.Devices.FirstOrDefaultAsync(d => d.Token == deviceToken, ct);
            Guid last = switched || device == null ? await LastPlayedAsync(existing.LoginId, ct) : device.AccountId;
            string session = switched || device?.SessionToken == null
                ? await BindDeviceAsync(deviceToken, existing.LoginId, last, ct)
                : device.SessionToken;
            await _db.SaveChangesAsync(ct);
            return new ExternalLoginResultDto(last, session, switched, false, identity.Provider);
        }
        if (currentLoginId == Guid.Empty || !await _db.Logins.AnyAsync(l => l.Id == currentLoginId, ct))
            throw new GameException("no_account", "Start the game once, then sign in.");
        if (await _db.ExternalLogins.AnyAsync(l => l.LoginId == currentLoginId && l.Provider == identity.Provider, ct))
            throw new GameException("linked_other", $"This account is already linked to another {ExternalAuth.Name(identity.Provider)} account.");
        _db.ExternalLogins.Add(new ExternalLogin { Provider = identity.Provider, Subject = identity.Subject, LoginId = currentLoginId, Email = identity.Email, CreatedUtc = DateTime.UtcNow });
        Device? mine = await _db.Devices.FirstOrDefaultAsync(d => d.Token == deviceToken, ct);
        string kept = mine?.SessionToken ?? await BindDeviceAsync(deviceToken, currentLoginId, Guid.Empty, ct);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new GameException("linked_other", "That sign-in was just linked elsewhere. Try again."); }
        return new ExternalLoginResultDto(mine?.AccountId ?? Guid.Empty, kept, false, true, identity.Provider);
    }

    /// <summary>The login's most recently played character, or Guid.Empty when it has none.</summary>
    private async Task<Guid> LastPlayedAsync(Guid loginId, CancellationToken ct) =>
        await _db.Accounts.AsNoTracking().Where(a => a.LoginId == loginId).OrderByDescending(a => a.LastHeartbeatUtc).Select(a => a.Id).FirstOrDefaultAsync(ct);

    /// <summary>Ends this device's session; the client then starts a new guest with a new device token.</summary>
    public async Task SignOutAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return;
        await _db.Devices.Where(d => d.SessionToken == sessionToken).ExecuteDeleteAsync(ct);
        await _db.Accounts.Where(a => a.SessionToken == sessionToken).ExecuteUpdateAsync(s => s.SetProperty(a => a.SessionToken, (string?)null), ct);
    }
}
