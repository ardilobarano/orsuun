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
        private readonly Button[] _etchRows = new Button[ItemState.MaxEtchings];
        private readonly Text[] _etchRowLabels = new Text[ItemState.MaxEtchings];
        private Button _etchButton;
        private Text _etchLabel;
        private Text _attemptInfo;
        private Text _result;
        private Text _turnLabel;
        private Text[] _methodLabels;
        private Button[] _methodButtons;
        private Button _turnButton;
        private Button _closeButton;
        private ForgeFx _fx;
        private ConfirmDialog _confirm;
        private Text _closeLabel;
        private Button[] _slotButtons;
        private Text[] _slotLabels;

        private static readonly string[] SlotNames = { "WEAPON", "ARMOR", "HELM", "SHIELD", "BRACER", "NECK", "EARS", "BOOTS" };

        private static readonly ForgeMethod[] Methods = { ForgeMethod.ForgeAlone, ForgeMethod.ScrollOfMercy, ForgeMethod.KhansAlloy };

        public bool Busy { get; private set; }
        public bool IsOpen => _canvas.activeSelf;
        public ForgeResult? LastResult { get; private set; }

        /// <summary>Item-name colour by upgrade level, the same steps as the glow: +7 pale gold, +8 gold, +9 ember-gold.</summary>
        public static Color LevelColor(int level) =>
            level >= 9 ? new Color(1f, 0.55f, 0.2f) : level >= 8 ? new Color(1f, 0.80f, 0.30f) : level >= 7 ? new Color(1f, 0.92f, 0.62f) : Palette.Parchment;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("ForgeCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Forge");
            Ui.Title("Title", canvas, 0.05f, 0.93f, 0.95f, 0.975f, "THE FORGE  ·  Forgemaster Dorun", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // What goes on the anvil: one button per equipment slot, empty slots disabled.
            _slotButtons = new Button[SlotNames.Length];
            _slotLabels = new Text[SlotNames.Length];
            for (int i = 0; i < SlotNames.Length; i++)
            {
                EquipSlot slot = (EquipSlot)i;
                float x0 = 0.02f + i * 0.12f;
                _slotButtons[i] = Ui.Button("Slot" + i, canvas, x0, 0.855f, x0 + 0.115f, 0.925f, "", 18, Palette.PanelDark, () => PutOnAnvil(slot), out _slotLabels[i]);
                Ui.Icon("Icon", _slotButtons[i].transform, 0.12f, 0.30f, 0.88f, 0.98f, slot.ToString());
                RectTransform label = _slotLabels[i].rectTransform;
                label.anchorMin = new Vector2(0f, 0f); label.anchorMax = new Vector2(1f, 0.32f);
            }

            _weapon = Ui.Label("Weapon", canvas, 0.05f, 0.795f, 0.95f, 0.85f, "", 56, TextAnchor.MiddleCenter, Palette.Parchment);
            _stats = Ui.Label("Stats", canvas, 0.05f, 0.765f, 0.95f, 0.81f, "", 30, TextAnchor.MiddleCenter, Palette.Muted);

            // Etchings: each line is tappable to pin or unpin it (Pinning Wax); ETCH adds the next one (Etching Needle).
            Ui.Framed("EtchingsBack", canvas, 0.06f, 0.575f, 0.74f, 0.755f, Palette.PanelDark);
            _etchings = Ui.Label("Etchings", canvas, 0.09f, 0.58f, 0.73f, 0.75f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);
            for (int i = 0; i < _etchRows.Length; i++)
            {
                int index = i;
                float y1 = 0.752f - i * 0.0352f;
                _etchRows[i] = Ui.Button("Etch" + i, canvas, 0.065f, y1 - 0.034f, 0.735f, y1, "", 24, Palette.PanelDark, () => AskPin(index), out _etchRowLabels[i]);
                _etchRowLabels[i].alignment = TextAnchor.MiddleLeft;
                _etchRowLabels[i].font = Ui.Font;
                _etchRowLabels[i].fontStyle = FontStyle.Normal;
            }
            _etchButton = Ui.Button("EtchAdd", canvas, 0.75f, 0.575f, 0.94f, 0.755f, "", 22, Palette.Alloy, AskEtch, out _etchLabel);

            // Turnstones: one turn here; the turning helper keeps turning until up to five chosen etchings are there.
            _turnButton = Ui.Button("Turn", canvas, 0.06f, 0.48f, 0.34f, 0.57f, "", 26, Palette.ButtonIdle, Turn, out _turnLabel);
            Ui.Button("TurnHelper", canvas, 0.36f, 0.48f, 0.94f, 0.57f, "TURNING HELPER\npick up to 5 etchings", 28, Palette.Alloy,
                () => { if (!Busy) _root.TurnHelper.Open(); }, out _);

            _attemptInfo = Ui.Label("AttemptInfo", canvas, 0.05f, 0.385f, 0.95f, 0.465f, "", 32, TextAnchor.MiddleCenter, Palette.Parchment);

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

            _result = Ui.Label("Result", canvas, 0.04f, 0.09f, 0.96f, 0.235f, "", 52, TextAnchor.MiddleCenter, Palette.Parchment);
            _closeButton = Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _closeLabel);

            _canvas.SetActive(false);

            // The anvil moment plays on its own layer above this screen.
            _fx = new GameObject("ForgeFx").AddComponent<ForgeFx>();
            _fx.Init();
            _confirm = new GameObject("ConfirmDialog").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        /// <summary>Opens the Forge on whatever is on the anvil; from the Gear screen, closing goes back there.</summary>
        public void Open(bool fromGear = false)
        {
            _result.text = "";
            _closeLabel.text = fromGear ? "BACK TO GEAR" : "BACK TO THE HUNT";
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

            AskFirst(method);
        }

        /// <summary>Owner, 24 Sep 2026: every attempt asks first, with the chance, the full cost and what a failure costs.</summary>
        private void AskFirst(ForgeMethod method)
        {
            PlayerSession s = _root.Session;
            ItemState item = s.OnAnvil;
            int target = item.UpgradeLevel + 1;
            int chance = s.ForgeChanceBp(method) / 100;
            string cost = $"{s.ForgeCost:N0} sorn";
            if (s.ForgeMaterials > 0) cost += $"  ·  {s.ForgeMaterials} {s.Lane.Stage.MaterialName}";
            if (method == ForgeMethod.ScrollOfMercy) cost += "  ·  1 Scroll of Mercy";
            if (method == ForgeMethod.KhansAlloy) cost += "  ·  1 Khan's Alloy";

            bool breaks = method == ForgeMethod.ForgeAlone && target >= ForgeRules.FirstOathbreakTarget;
            string failure = breaks
                ? ConfirmDialog.Tint($"If it fails, your +{item.UpgradeLevel} {item.DisplayName} is destroyed.", Palette.Bad)
                  + (s.AnvilWorn ? "\nA plain starter piece takes its place." : "")
                : ConfirmDialog.Tint(item.UpgradeLevel == 0 ? "If it fails, it stays at +0." : $"If it fails, it drops to +{item.UpgradeLevel - 1}.", Palette.Warn);

            string where = s.AnvilWorn ? "" : "\n" + ConfirmDialog.Tint("from your bag", Palette.Muted);
            string body = ConfirmDialog.Tint($"{item.DisplayName} +{item.UpgradeLevel}", LevelColor(item.UpgradeLevel)) + where
                + $"\n\nSuccess chance {ConfirmDialog.Tint(chance + "%", Palette.Good)}\nCost {cost}\n\n{failure}";
            (string label, Color color) = method == ForgeMethod.ScrollOfMercy ? ("USE SCROLL", Palette.Safe)
                : method == ForgeMethod.KhansAlloy ? ("USE ALLOY", Palette.Alloy) : ("FORGE", Palette.Danger);
            _confirm.Show($"Forge to +{target}?", body, label, color, () => { if (!Busy) StartCoroutine(AttemptSequence(method)); });
        }

        private IEnumerator AttemptSequence(ForgeMethod method)
        {
            Busy = true;
            int target = _root.Session.OnAnvil.UpgradeLevel + 1;
            EquipSlot anvilSlot = _root.Session.AnvilSlot;
            string anvilId = _root.Server.IdOf(_root.Session.OnAnvil);
            string pieceName = _root.Session.OnAnvil.DisplayName;
            float duration = target >= ForgeRules.PatienceFromTarget ? LongSequence : ShortSequence;
            string line = DorunLines[Random.Range(0, DorunLines.Length)];
            ShowResult("", Palette.Muted);

            // The hammer beats run on unscaled time: the speed button must never shorten the wait.
            yield return _fx.Beats(anvilSlot, target - 1, duration, line);
            _fx.Hold();

            ForgeResult? result = null;
            if (_root.Server.Online)
            {
                // The server rolled; the anvil sequence above only hid the round trip.
                string failure = null;
                yield return _root.Server.Forge(method, anvilSlot, anvilId, (dto, error) =>
                {
                    failure = error;
                    if (dto != null)
                        result = new ForgeResult((ForgeOutcome)System.Enum.Parse(typeof(ForgeOutcome), dto.outcome), dto.chanceBp, dto.levelBefore, dto.levelAfter);
                });
                if (result == null)
                {
                    yield return _fx.Cancel();
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

            yield return _fx.Reveal(result.Value, pieceName);
            Busy = false;
        }

        /// <summary>
        /// Dev switch for screenshots (-fxdemo Success|Nine|LevelLost|LevelKept|Oathbreak): plays the anvil moment with
        /// a made-up result on the weapon slot. Touches no item and no currency.
        /// </summary>
        public IEnumerator Demo(string outcome)
        {
            Open();
            Busy = true;
            bool nine = outcome == "Nine";
            ForgeOutcome kind = nine ? ForgeOutcome.Success : (ForgeOutcome)System.Enum.Parse(typeof(ForgeOutcome), outcome);
            int before = nine ? 8 : 6;
            int after = kind == ForgeOutcome.Success ? before + 1 : kind == ForgeOutcome.LevelLost ? before - 1 : kind == ForgeOutcome.LevelKept ? before : 0;
            yield return _fx.Beats(EquipSlot.Weapon, before, LongSequence, DorunLines[0]);
            yield return _fx.Reveal(new ForgeResult(kind, 5000, before, after), "Rider's Glaive");
            Busy = false;
        }

        private void Turn()
        {
            if (Busy) return;
            string blocker = _root.Session.TurnBlocker();
            if (blocker != null)
            {
                ShowResult(blocker, Palette.Muted);
                return;
            }

            if (_root.Server.Online)
            {
                Busy = true;
                StartCoroutine(_root.Server.Turn(1, System.Array.Empty<TurnTarget>(), _root.Session.AnvilSlot, _root.Server.IdOf(_root.Session.OnAnvil), (turns, stopped, error) =>
                {
                    Busy = false;
                    if (error != null) ShowResult(error, Palette.Muted);
                }));
            }
            else
            {
                _root.Session.Turn();
            }
        }

        /// <summary>ETCH: an Etching Needle adds the next etching to the piece on the anvil, after asking.</summary>
        private void AskEtch()
        {
            if (Busy) return;
            PlayerSession s = _root.Session;
            ItemState item = s.OnAnvil;
            string blocker = EtchingActions.EtchBlocker(item, s.Inventory);
            if (blocker != null) { ShowResult(blocker, Palette.Muted); return; }
            int chance = EtchingActions.EtchChanceBp(item) / 100;
            _confirm.Show("Add an etching?", $"{item.DisplayName} +{item.UpgradeLevel}\n\nEtching {item.Etchings.Count + 1} takes {ConfirmDialog.Tint(chance + "%", Palette.Good)} of the time.\n"
                + ConfirmDialog.Tint("If it slips, only the needle is lost.", Palette.Muted) + $"\n\nYou have {s.Inventory.EtchingNeedles} Etching Needles.",
                "USE NEEDLE", Palette.Alloy, () =>
                {
                    if (_root.Server.Online)
                    {
                        Busy = true;
                        StartCoroutine(_root.Server.Etch(_root.Server.IdOf(item), (result, error) =>
                        {
                            Busy = false;
                            if (error != null) ShowResult(error, Palette.Muted);
                            else ShowResult(result.text, result.took ? Palette.Good : Palette.Warn);
                        }));
                    }
                    else
                    {
                        bool took = s.Etch(item);
                        ShowResult(took ? "The needle took." : "The needle slipped; the piece is unharmed.", took ? Palette.Good : Palette.Warn);
                    }
                });
        }

        /// <summary>PIN: Pinning Wax holds one etching through turns (turns then cost two); tapping it again unpins it.</summary>
        private void AskPin(int index)
        {
            if (Busy) return;
            PlayerSession s = _root.Session;
            ItemState item = s.OnAnvil;
            if (index >= item.Etchings.Count) return;
            Etching e = item.Etchings[index];
            string line = $"T{e.Tier} {s.Pool.Entries[e.EntryId].Name} +{e.Value}";
            bool pinned = item.LockedEtchingIndex == index;
            string blocker = EtchingActions.PinBlocker(item, index, s.Inventory);
            if (blocker != null) { ShowResult(blocker + " Hunt Marks buy it in BOUNTIES.", Palette.Muted); return; }
            string body = pinned
                ? $"{line}\n\nIt will change with the next turn again.\n" + ConfirmDialog.Tint("The wax is spent.", Palette.Warn)
                : $"{line}\n\nIt stays through every turn; each turn then costs {ConfirmDialog.Tint("2 Turnstones", Palette.Warn)}.\n"
                  + (item.LockedEtchingIndex >= 0 ? ConfirmDialog.Tint("The etching pinned now is released and its wax is spent.", Palette.Warn) + "\n" : "")
                  + $"You have {s.Inventory.PinningWax} Pinning Wax.";
            _confirm.Show(pinned ? "Unpin this etching?" : "Pin this etching?", body, pinned ? "UNPIN" : "USE WAX", Palette.Alloy, () =>
            {
                if (_root.Server.Online)
                {
                    Busy = true;
                    StartCoroutine(_root.Server.Pin(_root.Server.IdOf(item), index, error =>
                    {
                        Busy = false;
                        ShowResult(error ?? (pinned ? "Unpinned." : "Pinned."), error != null ? Palette.Muted : Palette.Good);
                    }));
                }
                else
                {
                    s.Pin(item, index);
                    ShowResult(pinned ? "Unpinned." : "Pinned.", Palette.Good);
                }
            });
        }

        private void ShowResult(string message, Color color)
        {
            _result.text = message;
            _result.color = color;
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
                _slotLabels[i].text = piece == null ? "-" : "+" + piece.UpgradeLevel;
                _slotLabels[i].color = piece == null ? Palette.Muted : LevelColor(piece.UpgradeLevel);
                _slotButtons[i].interactable = piece != null && !Busy;
                bool onAnvil = session.AnvilWorn && (EquipSlot)i == session.AnvilSlot;
                _slotButtons[i].GetComponent<Image>().color = onAnvil ? Palette.Warn * 0.55f : Palette.PanelDark;
            }

            _weapon.text = $"{weapon.DisplayName} +{weapon.UpgradeLevel}";
            _weapon.color = LevelColor(weapon.UpgradeLevel);

            HeroStats hero = session.Hero;
            _stats.text = session.AnvilWorn
                ? $"Attack {hero.Attack}  ·  Crit {hero.CritChanceBp / 100}%  ·  Base stats {ForgeRules.StatPercent(weapon.UpgradeLevel)}%  ·  Blades lost {session.WeaponsBroken}"
                : $"From your bag, not worn  ·  Item level {weapon.ItemLevel}  ·  Base stats {ForgeRules.StatPercent(weapon.UpgradeLevel)}%";

            _etchings.text = weapon.Etchings.Count == 0 ? "No etchings yet: ETCH adds one." : "";
            for (int i = 0; i < _etchRows.Length; i++)
            {
                bool has = i < weapon.Etchings.Count;
                _etchRows[i].gameObject.SetActive(has);
                if (!has) continue;
                Etching e = weapon.Etchings[i];
                bool pinned = weapon.LockedEtchingIndex == i;
                _etchRowLabels[i].text = $"T{e.Tier}   {session.Pool.Entries[e.EntryId].Name}  +{e.Value}" + (pinned ? "   ◆ PINNED" : "");
                _etchRowLabels[i].color = pinned ? Palette.Sorn : Palette.Parchment;
                _etchRows[i].interactable = !Busy;
            }
            int etchChance = EtchingActions.EtchChanceBp(weapon) / 100;
            _etchLabel.text = weapon.Etchings.Count >= ItemState.MaxEtchings - 1 ? $"ETCH\n<size=16>5th needs a\nMaster's Needle</size>"
                : $"ETCH\n<size=16>{etchChance}% chance\n{inv.EtchingNeedles} needles\n{inv.PinningWax} wax</size>";
            _etchButton.interactable = !Busy && EtchingActions.EtchBlocker(weapon, inv) == null;
            _turnLabel.text = $"TURN x1\n(have {inv.Turnstones})";
            _turnButton.interactable = !Busy;

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
