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
            Ui.Label("Title", canvas, 0.05f, 0.905f, 0.95f, 0.965f, "THE FORGE  ·  Forgemaster Dorun", 40, TextAnchor.MiddleCenter, Palette.Warn);
            _weapon = Ui.Label("Weapon", canvas, 0.05f, 0.815f, 0.95f, 0.90f, "", 72, TextAnchor.MiddleCenter, Color.white);
            _stats = Ui.Label("Stats", canvas, 0.05f, 0.765f, 0.95f, 0.81f, "", 30, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Panel("EtchingsBack", canvas, 0.06f, 0.545f, 0.94f, 0.755f, Palette.PanelDark);
            _etchings = Ui.Label("Etchings", canvas, 0.09f, 0.55f, 0.91f, 0.75f, "", 30, TextAnchor.MiddleLeft, Color.white);
            _turnButton = Ui.Button("Turn", canvas, 0.06f, 0.475f, 0.94f, 0.535f, "", 30, Palette.ButtonIdle, Turn, out _turnLabel);

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
            int target = _root.Session.Weapon.UpgradeLevel + 1;
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
                yield return _root.Server.Forge(method, (dto, error) =>
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
                    ShowResult($"OATHBREAK.  Your +{result.Value.LevelBefore} blade is gone.", Palette.Bad);
                    break;
            }

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
            if (_root.Server.Online) StartCoroutine(_root.Server.Turn(error => { if (error != null) ShowResult(error, Palette.Muted); }));
            else _root.Session.Turn();
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
            ItemState weapon = session.Weapon;
            Inventory inv = session.Inventory;

            _weapon.text = $"{weapon.DisplayName} +{weapon.UpgradeLevel}";
            _weapon.color = LevelColor(weapon.UpgradeLevel);

            HeroStats hero = session.Hero;
            _stats.text = $"Attack {hero.Attack}  ·  Crit {hero.CritChanceBp / 100}%  ·  Base stats {ForgeRules.StatPercent(weapon.UpgradeLevel)}%  ·  Blades lost {session.WeaponsBroken}";

            var sb = new StringBuilder();
            foreach (Etching e in weapon.Etchings)
                sb.Append("T").Append(e.Tier).Append("   ").Append(session.Pool.Entries[e.EntryId].Name).Append("  +").Append(e.Value).Append('\n');
            _etchings.text = sb.ToString().TrimEnd();
            _turnLabel.text = $"TURN ALL ETCHINGS  ·  1 Turnstone  (have {inv.Turnstones})";
            _turnButton.interactable = !Busy;

            bool maxed = weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel;
            if (maxed)
            {
                _attemptInfo.text = "This blade has sworn all nine oaths.";
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
