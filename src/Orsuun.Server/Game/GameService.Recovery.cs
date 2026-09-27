using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Password reset and email verification by emailed codes (owner, 27 Sep 2026: "Password reset by email").
public sealed partial class GameService
{
    private static readonly TimeSpan ResetFor = TimeSpan.FromMinutes(30), VerifyFor = TimeSpan.FromHours(24);
    private const int MaxCodeTries = 5, MaxCodesAsked = 3;

    private static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string CodeHash(Guid loginId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(loginId.ToString("N") + ":" + code.Trim())));

    private static bool CodeMatches(Guid loginId, string? code, string? hash) =>
        hash != null && !string.IsNullOrWhiteSpace(code)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(CodeHash(loginId, code)), Encoding.ASCII.GetBytes(hash));

    /// <summary>Emails a reset code. The answer is the same whether or not the email has an account.</summary>
    public async Task<MessageDto> ForgotPasswordAsync(ForgotRequest request, string? clientIp, CancellationToken ct)
    {
        string email = AccountRules.NormaliseEmail(request.Email);
        if (AccountRules.EmailProblem(email) is string problem) throw new GameException("bad_email", problem);
        if (!_mail.CanSend) throw new GameException("mail_off", "Email is not set up on this server yet.");
        string ipKey = "forgot-ip:" + (clientIp ?? "?");
        if (RecentFailures("forgot:" + email) >= MaxCodesAsked || RecentFailures(ipKey) >= MaxCodesAsked * 4)
            throw new GameException("login_wait", "Too many codes asked for. Wait a few minutes and try again.");
        RecordFailure("forgot:" + email);
        RecordFailure(ipKey);
        Login? login = await _db.Logins.SingleOrDefaultAsync(l => l.Email == email, ct);
        if (login != null)
        {
            string code = NewCode();
            login.ResetHash = CodeHash(login.Id, code);
            login.ResetUntilUtc = DateTime.UtcNow + ResetFor;
            login.ResetTries = 0;
            await _db.SaveChangesAsync(ct);
            await _mail.SendAsync(email, "Your Orsuun password code",
                $"Your code to set a new password: {code}\n\nIt works for 30 minutes. If you did not ask for it, ignore this email: your password stays as it is.", ct);
        }
        return new MessageDto("If that email has an account, a code is on its way. It works for 30 minutes.");
    }

    /// <summary>A new password with the emailed code; this device then signs in to the account.</summary>
    public async Task<GuestLoginResponse> ResetPasswordAsync(ResetRequest request, string? clientIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceToken) || request.DeviceToken.Length > 128)
            throw new GameException("bad_device_token", "Device token missing or too long.");
        if (AccountRules.PasswordProblem(request.Password) is string passwordProblem) throw new GameException("bad_password", passwordProblem);
        string email = AccountRules.NormaliseEmail(request.Email);
        string ipKey = "ip:" + (clientIp ?? "?");
        if (RecentFailures(ipKey) >= MaxFailedLogins * 4) throw new GameException("login_wait", "Too many tries. Wait a few minutes and try again.");
        Login? login = await _db.Logins.SingleOrDefaultAsync(l => l.Email == email, ct);
        const string wrong = "That code is wrong or has run out. Ask for a new one.";
        if (login == null || login.ResetHash == null || login.ResetUntilUtc < DateTime.UtcNow || login.ResetTries >= MaxCodeTries)
        {
            RecordFailure(ipKey);
            throw new GameException("bad_code", wrong);
        }
        if (!CodeMatches(login.Id, request.Code, login.ResetHash))
        {
            login.ResetTries++;
            await _db.SaveChangesAsync(ct);
            RecordFailure(ipKey);
            throw new GameException("bad_code", wrong);
        }
        login.PasswordHash = Passwords.Hash(request.Password);
        login.ResetHash = null;
        login.ResetUntilUtc = null;
        login.EmailVerified = true;   // the code proved the mailbox is theirs
        Guid last = await LastPlayedAsync(login.Id, ct);
        string session = await BindDeviceAsync(request.DeviceToken, login.Id, last, ct);
        await _db.SaveChangesAsync(ct);
        int characters = await _db.Accounts.CountAsync(a => a.LoginId == login.Id, ct);
        return new GuestLoginResponse(last, session, false, login.Id, characters);
    }

    /// <summary>Emails a code that proves the login's email is the player's.</summary>
    public async Task<string> SendVerifyCodeAsync(Login login, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(login.Email)) throw new GameException("no_email", "Save an email to the account first.");
        if (login.EmailVerified) return "Your email is already verified.";
        if (RecentFailures("verify:" + login.Id) >= MaxCodesAsked) throw new GameException("login_wait", "Too many codes asked for. Wait a few minutes and try again.");
        RecordFailure("verify:" + login.Id);
        string code = NewCode();
        login.VerifyHash = CodeHash(login.Id, code);
        login.VerifyUntilUtc = DateTime.UtcNow + VerifyFor;
        await _db.SaveChangesAsync(ct);
        await _mail.SendAsync(login.Email, "Your Orsuun email code",
            $"Your code to verify this email: {code}\n\nEnter it on the ACCOUNT screen. It works for a day. With a verified email you can always set a new password.", ct);
        return "A code is on its way to " + login.Email + ".";
    }

    public async Task<string> VerifyEmailAsync(Login login, VerifyRequest request, CancellationToken ct)
    {
        if (login.EmailVerified) return "Your email is already verified.";
        if (login.VerifyHash == null || login.VerifyUntilUtc < DateTime.UtcNow || !CodeMatches(login.Id, request.Code, login.VerifyHash))
        {
            RecordFailure("verify-try:" + login.Id);
            if (RecentFailures("verify-try:" + login.Id) >= MaxCodeTries) { login.VerifyHash = null; await _db.SaveChangesAsync(ct); }
            throw new GameException("bad_code", "That code is wrong or has run out. Ask for a new one.");
        }
        login.EmailVerified = true;
        login.VerifyHash = null;
        await _db.SaveChangesAsync(ct);
        return "Email verified.";
    }

    /// <summary>After an email is saved, its code goes out when mail can (a server without email just skips it).</summary>
    private async Task TrySendVerifyAsync(Login login, CancellationToken ct)
    {
        if (!_mail.CanSend) return;
        try { await SendVerifyCodeAsync(login, ct); }
        catch (Exception ex) when (ex is GameException or System.Net.Mail.SmtpException) { }
    }

    /// <summary>The character's login (in the game) or the lobby's (at the character screen).</summary>
    public async Task<MessageDto> SendVerifyAsync(Login? login, CancellationToken ct)
    {
        login ??= _login ?? throw new GameException("no_login", "Sign in again.");
        return new MessageDto(await SendVerifyCodeAsync(login, ct), login.EmailVerified);
    }

    public async Task<MessageDto> VerifyAsync(Login? login, VerifyRequest request, CancellationToken ct)
    {
        login ??= _login ?? throw new GameException("no_login", "Sign in again.");
        return new MessageDto(await VerifyEmailAsync(login, request, ct), login.EmailVerified);
    }
}
