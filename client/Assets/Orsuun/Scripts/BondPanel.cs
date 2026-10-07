using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The sworn bond's card (owner, 7 Oct 2026; Rules.Bonds), from FRIENDS (BOND) and the call when someone asks: the
    /// partner and whether they hunt beside the hero now, the Bond Ring with its level, its XP bonus and the time to the next
    /// level, and BOND CHAT, PARTY (asks the partner into the hero's party) and BREAK; an ask waiting (ACCEPT, DECLINE); or how
    /// a bond is sworn (and the wait after one ended). -bondshow opens it once the hero is in (screenshots).
    /// </summary>
    public sealed class BondPanel : MonoBehaviour
    {
        public const string RingIcon = "BondRing";
        private GameRoot _root;
        private GameObject _canvas, _bonded, _asked;
        private Text _title, _partner, _status, _ring, _progress, _body, _message;
        private RectTransform _fill;
        private ConfirmDialog _confirm;
        private Net.ServerLink.BondDto _bond;
        private bool _busy, _showOnce;
        private float _fetchedAt = -100f;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-bondshow") >= 0;
            _canvas = Ui.Canvas("BondCanvas", 35).gameObject;
            Transform canvas = _canvas.transform;
            Image dim = Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Transform box = Ui.Framed("Box", canvas, 0.07f, 0.28f, 0.93f, 0.72f, Palette.PanelDark).transform;
            _title = Ui.Title("Title", box, 0.05f, 0.88f, 0.95f, 0.98f, "SWORN BOND", 34, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Icon("Ring", Ui.Rect("RingBox", box, 0.05f, 0.55f, 0.3f, 0.86f), 0f, 0f, 1f, 1f, RingIcon);
            _partner = Ui.Raw(Ui.Title("Partner", box, 0.33f, 0.76f, 0.96f, 0.86f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment));
            _status = Ui.Raw(Ui.Label("Status", box, 0.33f, 0.68f, 0.96f, 0.76f, "", 20, TextAnchor.MiddleLeft, Palette.Muted));
            _status.supportRichText = true;
            _ring = Ui.Raw(Ui.Label("RingLine", box, 0.33f, 0.6f, 0.96f, 0.68f, "", 22, TextAnchor.MiddleLeft, KorstoneRainView.Violet));
            _ring.supportRichText = true;
            _body = Ui.Raw(Ui.Label("Body", box, 0.06f, 0.36f, 0.94f, 0.58f, "", 21, TextAnchor.MiddleCenter, Palette.Parchment));
            _body.supportRichText = true;

            // Sworn: the time to the next ring, BOND CHAT, PARTY, BREAK.
            _bonded = Ui.Rect("Bonded", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Bar("Bar", _bonded.transform, 0.06f, 0.5f, 0.94f, 0.545f, KorstoneRainView.Violet, out Image fill);
            _fill = fill.rectTransform;
            _progress = Ui.Raw(Ui.Label("Progress", _bonded.transform, 0.06f, 0.455f, 0.94f, 0.5f, "", 19, TextAnchor.MiddleCenter, Palette.Parchment));
            Ui.Button("Chat", _bonded.transform, 0.05f, 0.25f, 0.35f, 0.37f, "BOND CHAT", 20, Palette.Safe, () => { Close(); _root.Chat.Open(bond: true); }, out _);
            Ui.Button("Party", _bonded.transform, 0.37f, 0.25f, 0.63f, 0.37f, "PARTY", 20, Palette.ButtonForge, InviteToParty, out _);
            Ui.Button("Break", _bonded.transform, 0.65f, 0.25f, 0.95f, 0.37f, "BREAK", 20, Palette.Danger, AskBreak, out _);

            // Asked: ACCEPT or DECLINE.
            _asked = Ui.Rect("Asked", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("Accept", _asked.transform, 0.06f, 0.25f, 0.48f, 0.37f, "ACCEPT", 24, Palette.Safe, () => Answer(true), out _);
            Ui.Button("Decline", _asked.transform, 0.52f, 0.25f, 0.94f, 0.37f, "DECLINE", 24, Palette.Danger, () => Answer(false), out _);

            _message = Ui.Raw(Ui.Label("Message", box, 0.05f, 0.15f, 0.95f, 0.23f, "", 20, TextAnchor.MiddleCenter, Palette.Muted));
            _message.supportRichText = true;
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.13f, "CLOSE", 24, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
            _confirm = new GameObject("BondConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        public void Open(string message = null)
        {
            if (!_root.Server.Online) { _root.Hud.Log("Bonds need the server."); return; }
            _message.text = message ?? "";
            _canvas.SetActive(true);
            Fetch();
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Asks a friend to swear a bond (FRIENDS); the card opens with the answer.</summary>
        public void Ask(string accountId, string name)
        {
            if (_busy || !_root.Server.Online) return;
            if (_root.Session.Inventory.Level < Bonds.MinLevel) { _root.Hud.Log($"Bonds are sworn from level {Bonds.MinLevel}."); return; }
            _busy = true;
            StartCoroutine(_root.Server.BondAsk(accountId, (bond, error) =>
            {
                _busy = false;
                if (bond != null) _bond = bond;
                Open(error != null ? ConfirmDialog.Tint(Loc.T(error), Palette.Bad) : Loc.T(bond?.message ?? ""));
            }));
        }

        private void Fetch()
        {
            _fetchedAt = Time.realtimeSinceStartup;
            StartCoroutine(_root.Server.FetchBond((bond, error) =>
            {
                if (bond != null) _bond = bond;
                else if (IsOpen) _message.text = ConfirmDialog.Tint(Loc.T(error), Palette.Bad);
            }));
        }

        private void Answer(bool accept)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.BondAnswer(accept, (bond, error) =>
            {
                _busy = false;
                if (bond != null) _bond = bond;
                _message.text = error != null ? ConfirmDialog.Tint(Loc.T(error), Palette.Bad) : Loc.T(bond?.message ?? "");
                if (bond != null && accept && !Net.ServerLink.IsNoGuid(bond.partnerId)) GameAudio.Instance?.Play("LaneLevelUp", 0.8f, 0.2f, 0f);
            }));
        }

        private void InviteToParty()
        {
            if (_bond == null || Net.ServerLink.IsNoGuid(_bond.partnerId)) return;
            _root.Party.Invite(_bond.partnerId, message => _message.text = Loc.T(message));
        }

        private void AskBreak()
        {
            if (_bond == null || Net.ServerLink.IsNoGuid(_bond.partnerId)) return;
            _confirm.Show(Loc.T("Break the bond?"),
                Loc.T("Your Bond Ring is lost, and both of you wait three days before another bond."),
                Loc.T("BREAK"), Palette.Danger, () =>
                {
                    StartCoroutine(_root.Server.BondBreak((bond, error) =>
                    {
                        if (bond != null) _bond = bond;
                        _message.text = error != null ? ConfirmDialog.Tint(Loc.T(error), Palette.Bad) : Loc.T(bond?.message ?? "");
                    }));
                });
        }

        private static string Hours(long seconds)
        {
            seconds = System.Math.Max(0, seconds);
            long hours = seconds / 3600;
            return hours >= 48 ? $"{hours / 24}d {hours % 24}h" : $"{hours}h {seconds % 3600 / 60:00}m";
        }

        private void Update()
        {
            if (_root == null) return;
            if (_showOnce && _root.Server.Online && !_root.Server.WaitingForHero)
            {
                _showOnce = false;
                Open();
            }
            if (!_canvas.activeSelf) return;
            if (!_root.Server.Online) { Close(); return; }
            if (Time.realtimeSinceStartup - _fetchedAt > 20f) Fetch();
            Net.ServerLink.BondDto b = _bond;
            bool sworn = b != null && !Net.ServerLink.IsNoGuid(b.partnerId);
            bool asked = b != null && !sworn && !string.IsNullOrEmpty(b.askName);
            if (_bonded.activeSelf != sworn) _bonded.SetActive(sworn);
            if (_asked.activeSelf != asked) _asked.SetActive(asked);
            if (b == null) { _partner.text = ""; _status.text = ""; _ring.text = ""; _body.text = ""; return; }
            // Sworn, the line under the bar is a short hint; otherwise the body has the card's middle to itself.
            _body.rectTransform.anchorMin = new Vector2(0.06f, sworn ? 0.385f : 0.36f);
            _body.rectTransform.anchorMax = new Vector2(0.94f, sworn ? 0.45f : 0.58f);
            if (sworn)
            {
                _partner.text = b.partnerName;
                string where = b.together ? ConfirmDialog.Tint(Loc.T("hunting beside you"), Palette.Good)
                    : b.partnerOnline ? Loc.T("online, not beside you") : Loc.T("away");
                _status.text = $"{Loc.T("Lv " + b.partnerLevel)} {b.partnerClass}  ·  {where}";
                _ring.text = $"<b>{Loc.T(b.ringName)}</b>  ·  " + Loc.T($"XP together +{b.xpBonusBp / 100f:0.#}%");
                _body.text = b.together ? "" : Loc.T("Hunt the same map in one party to earn the bonus and grow the ring.");
                long next = b.nextRingSeconds;
                float share = next > 0 ? Mathf.Clamp01((float)b.secondsTogether / next) : 1f;
                _fill.anchorMax = new Vector2(share, 1f);
                _progress.text = next > 0 ? Loc.T($"{Hours(b.secondsTogether)} together") + "  ·  " + Loc.T($"next ring at {next / 3600}h")
                    : Loc.T($"{Hours(b.secondsTogether)} together") + "  ·  " + Loc.T("the ring is at its height");
                return;
            }
            _partner.text = Loc.T(asked ? "An ask waits" : "No bond yet");
            _status.text = "";
            _ring.text = "";
            _body.text = asked ? Loc.T($"{b.askName} asks you to swear a bond. Hunt together in one party for more XP, and a Bond Ring that grows with every hour.")
                : b.waitSeconds > 0 ? Loc.T($"After a bond ends, wait {Hours(b.waitSeconds)} before another.")
                : Loc.T("Swear a bond with a friend of level 15 or more: while you hunt the same map in one party, both earn more XP, and your Bond Ring grows with every hour together. Ask from FRIENDS: tap a friend, SWEAR A BOND.");
        }
    }
}
