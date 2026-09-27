using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;

namespace Orsuun.Server.Game;

/// <summary>
/// The server's email (password reset and email codes, 27 Sep 2026): SMTP from Mail:Host, Mail:Port (587, STARTTLS),
/// Mail:User, Mail:Password and Mail:From (deploy/.env: MAIL_HOST ... MAIL_FROM; e.g. Resend's smtp.resend.com with user
/// "resend" and an API key, or a Gmail app password). Not set up, a Development server writes each message to the log and
/// keeps the last one per address for /v1/dev/mail; any other server says email is not set up.
/// </summary>
public sealed class MailSender
{
    private readonly string? _host, _user, _password, _from;
    private readonly int _port;
    private readonly bool _development;
    private readonly ILogger<MailSender> _log;

    /// <summary>Development without SMTP: the last message to each address.</summary>
    public ConcurrentDictionary<string, string> Kept { get; } = new(StringComparer.OrdinalIgnoreCase);

    public MailSender(IConfiguration config, IHostEnvironment env, ILogger<MailSender> log)
    {
        _host = config["Mail:Host"];
        _port = int.TryParse(config["Mail:Port"], out int port) ? port : 587;
        _user = config["Mail:User"];
        _password = config["Mail:Password"];
        _from = string.IsNullOrEmpty(config["Mail:From"]) ? _user : config["Mail:From"];
        _development = env.IsDevelopment();
        _log = log;
    }

    public bool Configured => !string.IsNullOrEmpty(_host) && !string.IsNullOrEmpty(_from);

    /// <summary>Whether a message can go (or, in Development, be kept) at all.</summary>
    public bool CanSend => Configured || _development;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!Configured)
        {
            if (!_development) throw new GameException("mail_off", "Email is not set up on this server yet.");
            Kept[to] = subject + "\n" + body;
            _log.LogInformation("Mail (not sent, no SMTP) to {To}: {Subject}", to, subject);
            return;
        }
        using var client = new SmtpClient(_host, _port) { EnableSsl = true, Credentials = new NetworkCredential(_user, _password) };
        using var message = new MailMessage(_from!, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
