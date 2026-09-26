using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// SKILLS (owner, 26 Sep 2026; Rules.SkillGrades): the five skills of the class played (the fourth and fifth locked
    /// until levels 30 and 60), each with its grade, the power it adds, and its next step. A Mastered step reads that skill's own Technique Scroll (70%, then 8 hours of rest; the
    /// step needs 1, 1, 2 .. 9 good reads); a Grand step or Peerless burns an Oathstone and pays Honor (60%). Every try
    /// asks first. Opened from the inventory, a book's card and the hunt's skill names.
    /// </summary>
    public sealed class SkillsPanel : MonoBehaviour
    {
        private sealed class Card
        {
            public Image Back;
            public RawImage Art;
            public Text Name;
            public Image Fill;
            public Text Power;
            public Text Status;
            public Button Train;
            public Text TrainLabel;
            public string Shown;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Text _purse;
        private Text _message;
        private readonly Card[] _cards = new Card[SkillGrades.Slots];
        private bool _busy;
        private int _focus = -1;
        private float _focusUntil;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("SkillsCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Oath");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "SKILLS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _purse = Ui.Label("Purse", canvas, 0.04f, 0.895f, 0.96f, 0.93f, "", 23, TextAnchor.MiddleCenter, Palette.Parchment);
            _purse.supportRichText = true;

            for (int i = 0; i < _cards.Length; i++)
            {
                int slot = i;
                float y1 = 0.89f - i * 0.143f;
                var c = new Card();
                c.Back = Ui.Framed("Skill" + i, canvas, 0.03f, y1 - 0.137f, 0.97f, y1, new Color(0.05f, 0.05f, 0.1f, 0.92f));
                Transform t = c.Back.transform;
                Ui.SlotTile("ArtSlot", t, 0.02f, 0.1f, 0.18f, 0.9f, new Color(0.08f, 0.08f, 0.14f));
                RectTransform artBox = Ui.Rect("ArtBox", t, 0.035f, 0.16f, 0.165f, 0.84f);
                c.Art = Ui.Icon("Art", artBox, 0f, 0f, 1f, 1f, "Weapon");
                c.Name = Ui.Title("Name", t, 0.2f, 0.7f, 0.74f, 0.95f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);
                c.Name.supportRichText = true;
                Ui.Bar("Grade", t, 0.2f, 0.56f, 0.74f, 0.69f, Palette.Sorn, out c.Fill);
                c.Power = Ui.Label("Power", t, 0.2f, 0.4f, 0.74f, 0.56f, "", 20, TextAnchor.MiddleLeft, Palette.Sorn);
                c.Power.supportRichText = true;
                c.Status = Ui.Label("Status", t, 0.2f, 0.05f, 0.74f, 0.4f, "", 18, TextAnchor.UpperLeft, Palette.Parchment);
                c.Status.supportRichText = true;
                c.Train = Ui.Button("Train", t, 0.76f, 0.14f, 0.98f, 0.86f, "", 20, Palette.ButtonForge, () => Ask(slot), out c.TrainLabel);
                _cards[i] = c;
            }

            Ui.Label("Rules", canvas, 0.05f, 0.1f, 0.95f, 0.165f,
                $"M1-M10: read the skill's own Technique Scroll ({SkillGrades.ReadChanceBp / 100}%, then {SkillGrades.ReadCooldownHours} hours of rest). "
                + $"G1-G10 and Peerless: an Oathstone and Honor ({SkillGrades.OathstoneChanceBp / 100}%). Honor comes from Korstones, Pit wins and dungeon Wardens.",
                19, TextAnchor.MiddleCenter, Palette.Muted);
            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.105f, "", 22, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Button("Close", canvas, 0.2f, 0.012f, 0.8f, 0.068f, "BACK", 26, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
            _confirm = new GameObject("SkillsConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        /// <summary>Opens the screen; a slot lights that skill's card for a moment (from a book's card).</summary>
        public void Open(int slot = -1)
        {
            _message.text = _root.Server.Online ? "" : "Offline: skill grades are trained on the server.";
            _focus = slot;
            _focusUntil = Time.unscaledTime + 2.5f;
            _canvas.SetActive(true);
        }

        private static string Rest(long seconds) => seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : $"{Mathf.Max(1, (int)(seconds / 60))}m";

        private int Grade(int book) => book < _root.Session.SkillGradeList.Count ? _root.Session.SkillGradeList[book] : 0;

        private int Progress(int book) => book < _root.Server.SkillProgress.Length ? _root.Server.SkillProgress[book] : 0;

        /// <summary>Why this skill cannot train now (Rules.SkillGrades.Problem), or null.</summary>
        private string Problem(int book)
        {
            if (!_root.Server.Online) return "Offline: skill grades are trained on the server.";
            SkillDef skill = SkillDef.For(_root.Session.Class)[Books.SlotOf(book)];
            if (_root.Session.Level < skill.UnlockLevel) return $"{skill.Name} unlocks at level {skill.UnlockLevel}.";
            long rest = _root.Server.SkillRestLeft(book);
            long since = SkillGrades.ReadCooldownHours * 3600L - rest;
            Inventory inv = _root.Session.Inventory;
            return SkillGrades.Problem(Grade(book), inv.Books[book], inv.Oathstones, _root.Server.Honor, since);
        }

        private void Ask(int slot)
        {
            if (_busy) return;
            int book = Books.Id(_root.Session.Class, slot);
            int grade = Grade(book);
            string skill = Books.SkillName(book);
            if (Problem(book) is string problem)
            {
                _confirm.Show(skill, problem, "OK", Palette.ButtonIdle, null);
                return;
            }
            string next = SkillGrades.Name(grade + 1);
            string body;
            if (SkillGrades.NeedsBooks(grade))
            {
                int need = SkillGrades.ReadsNeeded(grade);
                body = $"One {Books.Name(book)} is spent. {SkillGrades.ReadChanceBp / 100}% its teaching takes: {Progress(book) + 1} of {need} good reads toward {next}"
                       + (Progress(book) + 1 >= need ? ConfirmDialog.Tint($" (the skill rises to {next}, +{SkillGrades.BonusPercent(grade + 1)}% power)", Palette.Good) : "")
                       + $".\n\nWhatever happens, {skill} then rests {SkillGrades.ReadCooldownHours} hours before its next read."
                       + $"\n\nYou hold {_root.Session.Inventory.Books[book]} of this scroll.";
            }
            else
            {
                int honor = SkillGrades.HonorCost(grade);
                body = $"An Oathstone and {honor} Honor are spent either way. {SkillGrades.OathstoneChanceBp / 100}% the skill rises to {next}"
                       + $" (+{SkillGrades.BonusPercent(grade + 1)}% power).\n\nYou hold {_root.Session.Inventory.Oathstones} Oathstones and {_root.Server.Honor:N0} Honor.";
            }
            _confirm.Show($"{skill}: try for {next}?", body, SkillGrades.NeedsBooks(grade) ? "READ IT" : "BURN THE STONE", Palette.ButtonForge, () => Train(slot));
        }

        private void Train(int slot)
        {
            _busy = true;
            _message.text = "";
            StartCoroutine(_root.Server.TrainSkill(slot, (result, error) =>
            {
                _busy = false;
                if (error != null || result == null)
                {
                    _message.color = Palette.Bad;
                    _message.text = error ?? "No answer from the server.";
                    return;
                }
                _message.color = result.success ? Palette.Good : Palette.Warn;
                _message.text = result.message;
                GameAudio.Instance?.Play(result.success ? "LaneLevelUp" : "ForgeClang", 0.9f, 1f, 0f);
                _focus = slot;
                _focusUntil = Time.unscaledTime + 1.5f;
            }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            SkillDef[] skills = SkillDef.For(session.Class);
            _purse.text = $"{session.Class}  ·  Honor {ConfirmDialog.Tint(_root.Server.Honor.ToString("N0"), Palette.Sorn)}  ·  Oathstones {session.Inventory.Oathstones}";
            bool glow = Time.unscaledTime < _focusUntil && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.6f;
            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                int book = Books.Id(session.Class, i);
                int grade = Grade(book);
                if (c.Shown != skills[i].Name)
                {
                    c.Shown = skills[i].Name;
                    Ui.SetIcon(c.Art, "Skills/" + Hud.SkillLetters(skills[i].Name));
                }
                SkillTier tier = SkillGrades.Tier(grade);
                Color tint = tier == SkillTier.Peerless ? new Color(1f, 0.55f, 0.25f) : tier == SkillTier.Grand ? Palette.Sorn : tier == SkillTier.Mastered ? new Color(0.62f, 0.8f, 1f) : Palette.Muted;
                c.Name.text = skills[i].Name.ToUpperInvariant() + "   " + ConfirmDialog.Tint(SkillGrades.Name(grade).ToUpperInvariant(), tint);
                c.Fill.rectTransform.anchorMax = new Vector2(grade / (float)SkillGrades.Max, 1f);
                c.Fill.color = tint;
                c.Back.color = glow && i == _focus ? new Color(0.22f, 0.17f, 0.06f, 0.95f) : new Color(0.05f, 0.05f, 0.1f, 0.92f);
                c.Power.text = grade >= SkillGrades.Max
                    ? $"+{SkillGrades.BonusPercent(grade)}% skill power  ·  the summit"
                    : $"+{SkillGrades.BonusPercent(grade)}% skill power  ·  {SkillGrades.Name(grade + 1)} gives +{SkillGrades.BonusPercent(grade + 1)}%";

                long rest = _root.Server.SkillRestLeft(book);
                int held = session.Inventory.Books[book];
                if (session.Level < skills[i].UnlockLevel)
                {
                    c.Status.text = ConfirmDialog.Tint($"Unlocks at level {skills[i].UnlockLevel} (you are {session.Level}).", Palette.Muted)
                                    + (held > 0 ? $"\nScrolls held: {held}" : "");
                    c.TrainLabel.text = "LEVEL " + skills[i].UnlockLevel;
                    c.Train.interactable = false;
                    c.Art.color = new Color(0.5f, 0.5f, 0.55f, 0.7f);
                    continue;
                }
                c.Art.color = Color.white;
                if (grade >= SkillGrades.Max)
                {
                    c.Status.text = "Peerless: nothing is above it.";
                    c.TrainLabel.text = "PEERLESS";
                    c.Train.interactable = false;
                    continue;
                }
                if (SkillGrades.NeedsBooks(grade))
                {
                    c.Status.text = $"Reads toward {SkillGrades.Name(grade + 1)}: {Progress(book)} of {SkillGrades.ReadsNeeded(grade)}  ·  {SkillGrades.ReadChanceBp / 100}% a read\n"
                                    + (held > 0 ? $"Scrolls held: {held}" : ConfirmDialog.Tint("No scroll of this skill: Warden chests, the shops, the Exchange", Palette.Muted));
                    c.TrainLabel.text = rest > 0 ? "RESTING\n" + Rest(rest) : held > 0 ? "READ A\nSCROLL" : "NO SCROLL";
                }
                else
                {
                    int honor = SkillGrades.HonorCost(grade);
                    bool enough = _root.Server.Honor >= honor && session.Inventory.Oathstones > 0;
                    c.Status.text = $"Next: {SkillGrades.Name(grade + 1)}  ·  an Oathstone and {honor} Honor a try  ·  {SkillGrades.OathstoneChanceBp / 100}%\n"
                                    + (enough ? "Ready." : ConfirmDialog.Tint(session.Inventory.Oathstones == 0 ? "No Oathstone: the Carvers' Archive, the Pit shop" : $"Not enough Honor ({_root.Server.Honor:N0} of {honor})", Palette.Muted));
                    c.TrainLabel.text = "BURN AN\nOATHSTONE";
                }
                c.Train.interactable = !_busy && Problem(book) == null;
            }
        }
    }
}
