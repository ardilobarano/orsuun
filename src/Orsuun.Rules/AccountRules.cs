#nullable enable
using System.Linq;

namespace Orsuun.Rules
{
    /// <summary>
    /// Sign up and sign in (owner, 24 Sep 2026): an email and a password saved to the account the player is on, so the
    /// hero survives a new phone. Guests keep playing without one.
    /// </summary>
    public static class AccountRules
    {
        public const int MinPassword = 8;
        public const int MaxPassword = 128;
        public const int MaxEmail = 254;

        public static string NormaliseEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

        public static string? EmailProblem(string email)
        {
            if (email.Length == 0) return "Enter your email.";
            if (email.Length > MaxEmail) return "That email is too long.";
            int at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || email.Any(char.IsWhiteSpace)) return "That does not look like an email.";
            string domain = email.Substring(at + 1);
            int dot = domain.LastIndexOf('.');
            if (dot <= 0 || dot == domain.Length - 1) return "That does not look like an email.";
            return null;
        }

        public static string? PasswordProblem(string? password)
        {
            string p = password ?? "";
            if (p.Length < MinPassword) return $"A password has at least {MinPassword} characters.";
            if (p.Length > MaxPassword) return "That password is too long.";
            if (p.All(char.IsDigit) || p.Distinct().Count() < 4) return "Choose a stronger password.";
            return null;
        }
    }
}
