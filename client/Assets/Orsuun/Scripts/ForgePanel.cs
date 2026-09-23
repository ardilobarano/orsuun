using System.Collections;
using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The Forge screen. The lane keeps running behind it. Attempts to +7 and above play the
    /// 2.5 second anvil sequence that cannot be skipped (GDD section 6, Presentation).
    /// Any equipped item can go on the anvil (the slot row at the top); every item follows the weapon's rules.
    /// </summary>
    public sealed class ForgePanel : MonoBehaviour
    {
        private const float LongSequence = 2.5f;
        private const float ShortSequence = 0.8f;

        private static readonly string[] DorunLines =
        {
            "\"Hold still. I'm talking to the metal, not to you.\"",
            "\"It swore once. It can swear again.\"",
            "\"Don't watch the hammer. Watch the ember.\"",
            "\"If it breaks, I apologize to the blade. Not to you.\"",
        };

        private GameRoot _root;
        private GameObject _canvas;
        private Text _weapon;
        private Text _stats;
        private Text _etchings;
        private Text _attemptInfo;
        private Text _result;
        private Text _turnLabel;
        private Text[] _methodLabels;
        private Button[] _methodButtons;
        private Button _turnButton;
        private Button _closeButton;
        private RectTransform _anvilFill;
        private Button[] _slotButtons;
        private Text[] _slotLabels;

        private static readonly string[] SlotNames = { "WEAPON", "ARMOR", "HELM", "SHIELD", "BRACER", "NECK", "EARS", "BOOTS" };

        private static readonly ForgeMethod[] Methods = { ForgeMethod.ForgeAlone, ForgeMethod.ScrollOfMercy, ForgeMethod.KhansAlloy };

        public bool Busy { get; private set; }
        public bool IsOpen => _canvas.activeSelf;
        public ForgeResult? LastResult { get; private set; }

        public static Color LevelColor(int level) =>
            level >= 9 ? new Color(1f, 0.45f, 0.2f) : level >= 7 ? Palette.Warn : Color.white;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("ForgeCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0.04f, 0.04f, 0.05f, 0.94f));
            Ui.Label("Title", canvas, 0.05f, 0.93f, 0.95f, 0.975f, "THE FORGE  ·  Forgemaster Dorun", 40, TextAnchor.MiddleCenter, Palette.Warn);

            // What goes on the anvil: one button per equipment slot, empty slots disabled.
            _slotButtons = new Button[SlotNames.Length];
            _slotLabels = new Text[SlotNames.Length];
            for (int i = 0; i < SlotNames.Length; i++)
            {
                EquipSlot slot = (EquipSlot)i;
                float x0 = 0.02f + i * 0.12f;
                _slotButtons[i] = Ui.Button("Slot" + i, canvas, x0, 0.87f, x0 + 0.115f, 0.925f, "", 18, Palette.PanelDark, () => PutOnAnvil(slot), out _slotLabels[i]);
            }

            _weapon = Ui.Label("Weapon", canvas, 0.05f, 0.80f, 0.95f, 0.865f, "", 60, TextAnchor.MiddleCenter, Color.white);
            _stats = Ui.Label("Stats", canvas, 0.05f, 0.765f, 0.95f, 0.81f, "", 30, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Panel("EtchingsBack", canvas, 0.06f, 0.575f, 0.94f, 0.755f, Palette.PanelDark);
            _etchings = Ui.Label("Etchings", canvas, 0.09f, 0.58f, 0.91f, 0.75f, "", 28, TextAnchor.MiddleLeft, Color.white);

            // Turnstones: one turn, or a Bulk Turn of 10 / 50 that stops when the chosen etching reaches the chosen tier.
            _turnButton = Ui.Button("Turn", canvas, 0.06f, 0.515f, 0.34f, 0.57f, "", 24, Palette.ButtonIdle, () => Turn(1), out _turnLabel);
            Ui.Button("Turn10", canvas, 0.35f, 0.515f, 0.58f, 0.57f, "TURN x10", 24, Palette.ButtonIdle, () => Turn(10), out _);
            Ui.Button("Turn50", canvas, 0.59f, 0.515f, 0.94f, 0.57f, "TURN x50", 24, Palette.ButtonIdle, () => Turn(50), out _);
            Ui.Button("StopEntry", canvas, 0.06f, 0.475f, 0.66f, 0.51f, "", 20, Palette.PanelDark, () => _stopEntry = (_stopEntry + 2) % 17 - 1, out _stopEntryLabel);
            Ui.Button("StopTier", canvas, 0.67f, 0.475f, 0.94f, 0.51f, "", 20, Palette.PanelDark, () => _stopTier = _stopTier % 5 + 1, out _stopTierLabel);

            _attemptInfo = Ui.Label("AttemptInfo", canvas, 0.05f, 0.385f, 0.95f, 0.465f, "", 32, TextAnchor.MiddleCenter, Color.white);

            Color[] colors = { Palette.Danger, Palette.Safe, Palette.Alloy };
            _methodButtons = new Button[Methods.Length];
            _methodLabels = new Text[Methods.Length];
            for (int i = 0; i < Methods.Length; i++)
            {
                ForgeMethod method = Methods[i];
                float x0 = 0.04f + i * 0.31f;
                _methodButtons[i] = Ui.Button("Method" + i, canvas, x0, 0.25f, x0 + 0.30f, 0.375f, "", 28, colors[i],
                    () => StartAttempt(method), out _methodLabels[i]);
            }

            Ui.Panel("AnvilBack", canvas, 0.06f, 0.20f, 0.94f, 0.235f, Color.black);
            _anvilFill = Ui.Panel("AnvilFill", canvas, 0.06f, 0.20f, 0.06f, 0.235f, Palette.Warn).rectTransform;
            _result = Ui.Label("Result", canvas, 0.04f, 0.085f, 0.96f, 0.19f, "", 52, TextAnchor.MiddleCenter, Color.white);
            _closeButton = Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);

            _canvas.SetActive(false);
        }

        public void Open()
        {
            _result.text = "";
            _canvas.SetActive(true);
        }

        public void Close()
        {
            if (!Busy) _canvas.SetActive(false);
        }

        private void PutOnAnvil(EquipSlot slot)
        {
            if (Busy || _root.Session.Equipped(slot) == null) return;
            _root.Session.PutOnAnvil(slot);
            _result.text = "";
        }

        public void StartAttempt(ForgeMethod method)
        {
            if (Busy) return;

            string blocker = _root.Session.ForgeBlocker(method);
            if (blocker != null)
            {
                ShowResult(blocker, Palette.Muted);
                return;
            }

            StartCoroutine(AttemptSequence(method));
        }

        private IEnumerator AttemptSequence(ForgeMethod method)
        {
            Busy = true;
            int target = _root.Session.OnAnvil.UpgradeLevel + 1;
            EquipSlot anvilSlot = _root.Session.AnvilSlot;
            string pieceName = _root.Session.OnAnvil.DisplayName;
            float duration = target >= ForgeRules.PatienceFromTarget ? LongSequence : ShortSequence;
            ShowResult(DorunLines[Random.Range(0, DorunLines.Length)], Palette.Muted);

            // Unscaled time: the speed button must never shorten the wait.
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAnvil(t / duration);
                yield return null;
            }
            SetAnvil(0f);

            ForgeResult? result = null;
            if (_root.Server.Online)
            {
                // The server rolled; the anvil sequence above only hid the round trip.
                string failure = null;
                yield return _root.Server.Forge(method, anvilSlot, (dto, error) =>
                {
                    failure = error;
                    if (dto != null)
                        result = new ForgeResult((ForgeOutcome)System.Enum.Parse(typeof(ForgeOutcome), dto.outcome), dto.chanceBp, dto.levelBefore, dto.levelAfter);
                });
                if (result == null)
                {
                    ShowResult(failure ?? "No answer from the server.", Palette.Muted);
                    Busy = false;
                    yield break;
                }
            }
            else
            {
                result = _root.Session.Forge(method);
            }

            LastResult = result;
            switch (result.Value.Outcome)
            {
                case ForgeOutcome.Success:
                    ShowResult(result.Value.LevelAfter == ItemState.MaxUpgradeLevel
                        ? "+9!  The whole server hears the hammer."
                        : $"SUCCESS  ·  +{result.Value.LevelAfter}", Palette.Good);
                    break;
                case ForgeOutcome.LevelLost:
                    ShowResult($"The metal sulks.  Back to +{result.Value.LevelAfter}", Palette.Warn);
                    break;
                case ForgeOutcome.LevelKept:
                    ShowResult("The ward holds.  Level kept.", Palette.Warn);
                    break;
                case ForgeOutcome.Oathbreak:
                    ShowResult($"OATHBREAK.  Your +{result.Value.LevelBefore} {pieceName} is gone.", Palette.Bad);
                    break;
            }

            Busy = false;
        }

        private int _stopEntry = -1;
        private int _stopTier = 3;
        private Text _stopEntryLabel;
        private Text _stopTierLabel;

        private void Turn(int count)
        {
            if (Busy) return;
            string blocker = _root.Session.TurnBlocker();
            if (blocker != null)
            {
                ShowResult(blocker, Palette.Muted);
                return;
            }

            int stopEntry = count > 1 ? _stopEntry : -1;
            if (_root.Server.Online)
            {
                Busy = true;
                StartCoroutine(_root.Server.Turn(count, stopEntry, _stopTier, _root.Session.AnvilSlot, (turns, stopped, error) =>
                {
                    Busy = false;
                    if (error != null) ShowResult(error, Palette.Muted);
                    else if (count > 1) ShowResult(stopped ? $"Stopped after {turns} turn{(turns == 1 ? "" : "s")}: the rule hit." : $"{turns} turns, no match.", stopped ? Palette.Good : Palette.Warn);
                }));
            }
            else if (count == 1)
            {
                _root.Session.Turn();
            }
            else
            {
                int turns = _root.Session.TurnBulk(count, stopEntry >= 0 ? stopEntry : (int?)null, _stopTier, out bool stopped);
                ShowResult(stopped ? $"Stopped after {turns} turn{(turns == 1 ? "" : "s")}: the rule hit." : $"{turns} turns, no match.", stopped ? Palette.Good : Palette.Warn);
            }
        }

        private void ShowResult(string message, Color color)
        {
            _result.text = message;
            _result.color = color;
        }

        private void SetAnvil(float progress)
        {
            _anvilFill.anchorMax = new Vector2(0.06f + 0.88f * Mathf.Clamp01(progress), _anvilFill.anchorMax.y);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            ItemState weapon = session.OnAnvil;
            Inventory inv = session.Inventory;

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                ItemState piece = session.Equipped((EquipSlot)i);
                _slotLabels[i].text = SlotNames[i] + "\n" + (piece == null ? "-" : "+" + piece.UpgradeLevel);
                _slotLabels[i].color = piece == null ? Palette.Muted : LevelColor(piece.UpgradeLevel);
                _slotButtons[i].interactable = piece != null && !Busy;
                _slotButtons[i].GetComponent<Image>().color = (EquipSlot)i == session.AnvilSlot ? Palette.Warn * 0.55f : Palette.PanelDark;
            }

            _weapon.text = $"{weapon.DisplayName} +{weapon.UpgradeLevel}";
            _weapon.color = LevelColor(weapon.UpgradeLevel);

            HeroStats hero = session.Hero;
            _stats.text = $"Attack {hero.Attack}  ·  Crit {hero.CritChanceBp / 100}%  ·  Base stats {ForgeRules.StatPercent(weapon.UpgradeLevel)}%  ·  Blades lost {session.WeaponsBroken}";

            var sb = new StringBuilder();
            foreach (Etching e in weapon.Etchings)
                sb.Append("T").Append(e.Tier).Append("   ").Append(session.Pool.Entries[e.EntryId].Name).Append("  +").Append(e.Value).Append('\n');
            _etchings.text = sb.ToString().TrimEnd();
            _turnLabel.text = $"TURN x1\n(have {inv.Turnstones})";
            _turnButton.interactable = !Busy;
            _stopEntryLabel.text = _stopEntry < 0 ? "Stop rule: none (bulk turns run to the end)" : "Stop when: " + session.Pool.Entries[_stopEntry].Name;
            _stopTierLabel.text = _stopEntry < 0 ? "" : "at T" + _stopTier + "+";

            bool maxed = weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel;
            if (maxed)
            {
                _attemptInfo.text = "This piece has sworn all nine oaths.";
            }
            else
            {
                int target = weapon.UpgradeLevel + 1;
                string patience = weapon.PatienceBp > 0 ? $"  (includes +{weapon.PatienceBp / 100}% Forgemaster's Patience)" : "";
                string materials = session.ForgeMaterials > 0 ? $"  ·  {session.ForgeMaterials} {session.Lane.Stage.MaterialName}" : "";
                _attemptInfo.text = $"Attempt +{target}:  {session.ForgeChanceBp(ForgeMethod.ForgeAlone) / 100}% success{patience}\nCost {session.ForgeCost:N0} sorn{materials}";
            }

            bool breaks = weapon.UpgradeLevel + 1 >= ForgeRules.FirstOathbreakTarget;
            _methodLabels[0].text = "FORGE ALONE\n" + (breaks ? "fail: OATHBREAK" : "fail: -1 level");
            _methodLabels[1].text = $"SCROLL OF MERCY ({inv.ScrollsOfMercy})\nfail: -1 level";
            _methodLabels[2].text = $"KHAN'S ALLOY ({inv.KhansAlloys})\n+10%, fail: -1 level";
            for (int i = 0; i < _methodButtons.Length; i++) _methodButtons[i].interactable = !Busy && !maxed;
            _closeButton.interactable = !Busy;
        }
    }
}
