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
        private Image[] _slotTiles;
        private RawImage[] _slotIcons;
        private ItemPreview _preview;

        private static readonly string[] SlotNames = { "WEAPON", "ARMOR", "HELM", "SHIELD", "BRACER", "NECK", "EARS", "BOOTS" };

        private static readonly ForgeMethod[] Methods = { ForgeMethod.ForgeAlone, ForgeMethod.ScrollOfMercy, ForgeMethod.KhansAlloy };

        public bool Busy { get; private set; }
        public bool IsOpen => _canvas.activeSelf;
        public ForgeResult? LastResult { get; private set; }

        /// <summary>Etching tier colours, T1 to T5: grey, green, blue, violet, gold.</summary>
        public static Color TierColor(int tier) => tier switch
        {
            1 => new Color(0.78f, 0.76f, 0.72f),
            2 => new Color(0.5f, 0.86f, 0.48f),
            3 => new Color(0.45f, 0.7f, 1f),
            4 => new Color(0.78f, 0.55f, 1f),
            _ => new Color(1f, 0.8f, 0.32f),
        };

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
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.978f, "THE FORGE  ·  Forgemaster Dorun", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // What goes on the anvil: one framed slot per equipment slot (forge mockup), empty slots disabled.
            _slotButtons = new Button[SlotNames.Length];
            _slotLabels = new Text[SlotNames.Length];
            _slotTiles = new Image[SlotNames.Length];
            _slotIcons = new RawImage[SlotNames.Length];
            for (int i = 0; i < SlotNames.Length; i++)
            {
                EquipSlot slot = (EquipSlot)i;
                float x0 = 0.025f + i * 0.119f;
                _slotTiles[i] = Ui.SlotTile("SlotTile" + i, canvas, x0, 0.862f, x0 + 0.112f, 0.93f, Palette.PanelDark);
                Image hit = Ui.Panel("Slot" + i, canvas, x0, 0.862f, x0 + 0.112f, 0.93f, Color.clear);
                _slotButtons[i] = hit.gameObject.AddComponent<Button>();
                _slotButtons[i].targetGraphic = _slotTiles[i];
                _slotButtons[i].onClick.AddListener(() => PutOnAnvil(slot));
                hit.gameObject.AddComponent<Press>();
                _slotIcons[i] = Ui.Icon("Icon", hit.transform, 0.14f, 0.26f, 0.86f, 0.9f, slot.ToString());
                _slotLabels[i] = Ui.Title("Level", hit.transform, 0.1f, 0.02f, 0.92f, 0.34f, "", 20, TextAnchor.MiddleRight, Palette.Parchment);
            }

            // The piece itself, big: its real look turning in the forge light (ItemPreview), or its painted icon.
            Image stage = Ui.Framed("PreviewCard", canvas, 0.04f, 0.668f, 0.96f, 0.855f, new Color(0.05f, 0.045f, 0.09f, 0.95f));
            RectTransform previewBox = Ui.Rect("PreviewBox", stage.transform, 0.018f, 0.05f, 0.982f, 0.95f);
            _preview = new GameObject("ItemPreview").AddComponent<ItemPreview>();
            _preview.Init(previewBox);
            _preview.gameObject.SetActive(false);

            Ui.Framed("NamePlate", canvas, 0.1f, 0.606f, 0.9f, 0.664f, new Color(0.07f, 0.08f, 0.16f, 0.96f));
            _weapon = Ui.Title("Weapon", canvas, 0.13f, 0.629f, 0.87f, 0.66f, "", 40, TextAnchor.MiddleCenter, Palette.Parchment);
            _stats = Ui.Label("Stats", canvas, 0.13f, 0.609f, 0.87f, 0.631f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);

            // Etchings in their own card: each line is tappable to pin or unpin it (Pinning Wax); ETCH adds the next one
            // (Etching Needle).
            Ui.Framed("EtchingsBack", canvas, 0.04f, 0.404f, 0.96f, 0.6f, new Color(0.06f, 0.06f, 0.12f, 0.93f));
            Ui.Sliced("EtchRuleL", canvas, 0.07f, 0.573f, 0.33f, 0.585f, "Rule", Color.white).raycastTarget = false;
            Ui.Title("EtchingsHead", canvas, 0.33f, 0.562f, 0.67f, 0.596f, "ETCHINGS", 28, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Sliced("EtchRuleR", canvas, 0.67f, 0.573f, 0.93f, 0.585f, "Rule", Color.white).raycastTarget = false;
            _etchings = Ui.Label("Etchings", canvas, 0.08f, 0.42f, 0.76f, 0.555f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);
            for (int i = 0; i < _etchRows.Length; i++)
            {
                int index = i;
                float y1 = 0.557f - i * 0.0298f;
                Image row = Ui.Sliced("Etch" + i, canvas, 0.065f, y1 - 0.027f, 0.765f, y1, "CardFill", new Color(0.11f, 0.11f, 0.19f, 0.95f));
                _etchRows[i] = row.gameObject.AddComponent<Button>();
                _etchRows[i].targetGraphic = row;
                _etchRows[i].onClick.AddListener(() => AskPin(index));
                row.gameObject.AddComponent<Press>();
                _etchRowLabels[i] = Ui.Label("Label", row.transform, 0.04f, 0.05f, 0.97f, 0.95f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                _etchRowLabels[i].supportRichText = true;
            }
            _etchButton = Ui.Button("EtchAdd", canvas, 0.78f, 0.414f, 0.945f, 0.557f, "", 22, Palette.Alloy, AskEtch, out _etchLabel);

            // The attempt in a framed pill, under the anvil sign.
            Ui.Framed("AttemptBack", canvas, 0.04f, 0.352f, 0.96f, 0.398f, new Color(0.07f, 0.07f, 0.14f, 0.95f));
            RectTransform anvil = Ui.Rect("AttemptIconBox", canvas, 0.06f, 0.356f, 0.13f, 0.394f);
            Ui.Icon("AttemptIcon", anvil, 0f, 0f, 1f, 1f, "NavForge");
            _attemptInfo = Ui.Label("AttemptInfo", canvas, 0.14f, 0.354f, 0.94f, 0.396f, "", 28, TextAnchor.MiddleCenter, Palette.Parchment);
            _attemptInfo.supportRichText = true;

            // The three ways to strike, as tiles with their own icons.
            Color[] colors = { Palette.Danger, Palette.Safe, Palette.Alloy };
            string[] icons = { "NavForge", "ScrollOfMercy", "KhansAlloy" };
            _methodButtons = new Button[Methods.Length];
            _methodLabels = new Text[Methods.Length];
            for (int i = 0; i < Methods.Length; i++)
            {
                ForgeMethod method = Methods[i];
                float x0 = 0.04f + i * 0.31f;
                _methodButtons[i] = Ui.Tile("Method" + i, canvas, x0, 0.243f, x0 + 0.30f, 0.345f, "", 24, colors[i], icons[i],
                    () => StartAttempt(method), out _methodLabels[i]);
                RectTransform label = _methodLabels[i].rectTransform;
                label.anchorMin = new Vector2(0.06f, 0.08f);
                label.anchorMax = new Vector2(0.94f, 0.46f);
                RectTransform icon = (RectTransform)_methodButtons[i].transform.Find("IconBox");
                icon.anchorMin = new Vector2(0.3f, 0.46f);
                icon.anchorMax = new Vector2(0.7f, 0.92f);
            }

            // Turnstones: one turn here; the turning helper keeps turning until up to five chosen etchings are there.
            _turnButton = Ui.IconButton("Turn", canvas, 0.06f, 0.183f, 0.37f, 0.236f, "", 24, Palette.ButtonIdle, "Turnstone", Turn, out _turnLabel);
            Ui.IconButton("TurnHelper", canvas, 0.39f, 0.183f, 0.94f, 0.236f, "TURNING HELPER", 26, Palette.Alloy, "EtchingNeedle",
                () => { if (!Busy) _root.TurnHelper.Open(); }, out _);

            _result = Ui.Label("Result", canvas, 0.04f, 0.083f, 0.96f, 0.178f, "", 48, TextAnchor.MiddleCenter, Palette.Parchment);
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
            _preview.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (Busy) return;
            _canvas.SetActive(false);
            _preview.gameObject.SetActive(false);
        }

        /// <summary>The screen area (canvas anchors) covering the named Forge elements, for the tutorial's highlight.</summary>
        public Rect Area(params string[] names)
        {
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (string n in names)
            {
                var rect = (RectTransform)_canvas.transform.Find(n);
                if (rect == null) continue;
                min = Vector2.Min(min, rect.anchorMin);
                max = Vector2.Max(max, rect.anchorMax);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
            yield return _fx.Beats(anvilSlot, target - 1, duration, line, Ui.ItemIcon(_root.Session.OnAnvil));
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
                + ConfirmDialog.Tint("If it slips, only the needle is lost.", Palette.Muted)
                + (item.Etchings.Count == ItemState.MaxEtchings - 1 ? $"\n\nThe fifth takes a Master's Needle: you have {s.Inventory.MastersNeedles}."
                    : $"\n\nYou have {s.Inventory.EtchingNeedles} Etching Needles."),
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
                _slotLabels[i].text = piece == null ? "" : "+" + piece.UpgradeLevel;
                _slotLabels[i].color = piece == null ? Palette.Muted : LevelColor(piece.UpgradeLevel);
                _slotButtons[i].interactable = piece != null && !Busy;
                bool onAnvil = session.AnvilWorn && (EquipSlot)i == session.AnvilSlot;
                _slotTiles[i].color = onAnvil ? new Color(0.85f, 0.62f, 0.2f) : piece == null ? new Color(0.06f, 0.06f, 0.09f) : Palette.PanelDark;
                _slotIcons[i].color = piece == null ? new Color(1f, 1f, 1f, 0.22f) : Color.white;
                Ui.SetIcon(_slotIcons[i], piece != null ? Ui.ItemIcon(piece) : ((EquipSlot)i).ToString());
            }
            _preview.Show(weapon, session.Class);

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
                _etchRowLabels[i].text = ConfirmDialog.Tint("T" + e.Tier, TierColor(e.Tier)) + $"   {session.Pool.Entries[e.EntryId].Name}  +{e.Value}" + (pinned ? "   ◆ PINNED" : "");
                _etchRowLabels[i].color = pinned ? Palette.Sorn : Palette.Parchment;
                _etchRows[i].interactable = !Busy;
            }
            int etchChance = EtchingActions.EtchChanceBp(weapon) / 100;
            // The fifth etching takes a Master's Needle (the Carvers' Archive, the Pit shop).
            _etchLabel.text = weapon.Etchings.Count >= ItemState.MaxEtchings ? "ETCH\n<size=16>all five\netchings</size>"
                : weapon.Etchings.Count == ItemState.MaxEtchings - 1 ? $"ETCH\n<size=16>{etchChance}% chance\n{inv.MastersNeedles} Master's\nNeedles</size>"
                : $"ETCH\n<size=16>{etchChance}% chance\n{inv.EtchingNeedles} needles\n{inv.PinningWax} wax</size>";
            _etchButton.interactable = !Busy && EtchingActions.EtchBlocker(weapon, inv) == null;
            _turnLabel.text = $"TURN  <size=18>({inv.Turnstones})</size>";
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
                if (session.ForgeLuckBp > 0) patience += $"  (includes +{session.ForgeLuckBp / 100}% Lucky Forge Hour)";
                string materials = session.ForgeMaterials > 0 ? $"  ·  {session.ForgeMaterials} {session.Lane.Stage.MaterialName}" : "";
                _attemptInfo.text = $"Attempt +{target}:  {ConfirmDialog.Tint(session.ForgeChanceBp(ForgeMethod.ForgeAlone) / 100 + "%", Palette.Good)} success{patience}  ·  Cost {session.ForgeCost:N0} sorn{materials}";
            }

            bool breaks = weapon.UpgradeLevel + 1 >= ForgeRules.FirstOathbreakTarget;
            _methodLabels[0].text = "FORGE ALONE\n<size=17>" + (breaks ? "fail: OATHBREAK" : "fail: -1 level") + "</size>";
            _methodLabels[1].text = $"SCROLL OF MERCY ({inv.ScrollsOfMercy})\n<size=17>fail: -1 level</size>";
            _methodLabels[2].text = $"KHAN'S ALLOY ({inv.KhansAlloys})\n<size=17>+10%, fail: -1 level</size>";
            for (int i = 0; i < _methodButtons.Length; i++) _methodButtons[i].interactable = !Busy && !maxed;
            _closeButton.interactable = !Busy;
        }
    }
}
