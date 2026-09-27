namespace Orsuun.Rules
{
    /// <summary>
    /// The mailbox (owner, 27 Sep 2026: "Mailbox"): each hero's letters. A letter is a notice, or holds sorn, goods,
    /// Technique Scrolls or a piece until the hero takes it. The Salt Exchange pays its sales and hands back what did not
    /// sell by letter; the Pits' season writes its rewards. A letter with something still in it stays until taken; the rest
    /// go KeepDays after they came.
    /// </summary>
    public static class Mail
    {
        /// <summary>Days a letter with nothing left to take is kept.</summary>
        public const int KeepDays = 30;
        /// <summary>The newest letters shown (older ones wait behind them).</summary>
        public const int MaxShown = 100;
        public const int TitleMax = 80;
        public const int BodyMax = 400;
    }
}
