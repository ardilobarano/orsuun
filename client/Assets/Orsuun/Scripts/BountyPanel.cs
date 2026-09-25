using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// BOUNTIES: the day's and the week's bounties with the server's counts, CLAIM for finished ones (Hunt Marks), when
    /// each board resets (20:00 server time; the week on Mondays), and the Hunt Marks shop.
    /// </summary>
    public sealed class BountyPanel : MonoBehaviour
    {
        private const int Rows = 9;

        private sealed class Row
        {
            public Image Back;
            public Text Label;
            public RectTransform Fill;
            public Button Claim;
            public Text ClaimLabel;
            public int Id;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _marks;
        private Text _dailyTitle;
        private Text _weeklyTitle;
        private Text _message;
        private readonly Row[] _rows = new Row[Rows];
        private readonly Button[] _buy = new Button[HuntShop.Items.Length];
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        /// <summary>Any bounty finished and not claimed (the HUD marks the button).</summary>
        public bool AnyClaimable
        {
            get
            {
                var board = _root.Server.Bounties;
                if (board?.items == null) return false;
                foreach (var b in board.items) if (!b.claimed && b.count >= b.target) return true;
                return false;
            }
        }

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("BountyCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Bounties");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "BOUNTIES", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _marks = Ui.Title("Marks", canvas, 0.05f, 0.895f, 0.95f, 0.935f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);

            _dailyTitle = Ui.Title("DailyTitle", canvas, 0.04f, 0.855f, 0.96f, 0.89f, "", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            _weeklyTitle = Ui.Title("WeeklyTitle", canvas, 0.04f, 0.56f, 0.96f, 0.592f, "", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                // Five daily rows under the first title, four weekly rows under the second.
                float y1 = i < 5 ? 0.85f - i * 0.052f : 0.555f - (i - 5) * 0.052f;
                float y0 = y1 - 0.047f;
                var r = new Row();
                r.Back = Ui.Framed("Back" + i, canvas, 0.04f, y0, 0.74f, y1, Palette.PanelDark);
                Ui.Panel("Bar" + i, canvas, 0.045f, y0 + 0.004f, 0.735f, y0 + 0.012f, new Color(0f, 0f, 0f, 0.4f)).raycastTarget = false;
                r.Fill = Ui.Panel("Fill" + i, canvas, 0.045f, y0 + 0.004f, 0.735f, y0 + 0.012f, Palette.Sorn).rectTransform;
                r.Label = Ui.Label("Label" + i, canvas, 0.06f, y0 + 0.012f, 0.73f, y1, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                r.Claim = Ui.Button("Claim" + i, canvas, 0.76f, y0, 0.96f, y1, "", 22, Palette.Safe, () => Claim(index), out r.ClaimLabel);
                _rows[i] = r;
            }

            Ui.Section("ShopTitle", canvas, 0.08f, 0.31f, 0.92f, 0.345f, "HUNT MARKS SHOP", 24);
            for (int i = 0; i < HuntShop.Items.Length; i++)
            {
                ShopItem item = HuntShop.Items[i];
                float x0 = 0.04f + (i % 3) * 0.31f;
                float y1 = i < 3 ? 0.305f : 0.215f;
                _buy[i] = Ui.Button("Buy" + i, canvas, x0, y1 - 0.085f, x0 + 0.3f, y1, $"{item.Name}\n<size=18>{item.Detail}</size>\n{item.Marks} MARKS", 22,
                    Palette.ButtonIdle, () => Buy(item.Id), out _);
            }

            _message = Ui.Label("Message", canvas, 0.05f, 0.08f, 0.95f, 0.125f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            // Bounties pay the Campaign Trail's XP (GDD section 3): its screen is a tap away.
            Ui.Button("Trail", canvas, 0.03f, 0.015f, 0.47f, 0.075f, "CAMPAIGN TRAIL", 26, Palette.Alloy, () => { _canvas.SetActive(false); _root.Trail.Open(); }, out _);
            Ui.Button("Close", canvas, 0.5f, 0.015f, 0.97f, 0.075f, "BACK TO THE HUNT", 26, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "" : "Offline: bounties are counted by the server.";
            _canvas.SetActive(true);
        }

        private void Claim(int index)
        {
            if (_busy) return;
            _busy = true;
            int id = _rows[index].Id;
            StartCoroutine(_root.Server.ClaimBounty(id, error =>
            {
                _busy = false;
                if (error != null) _message.text = ConfirmDialog.Tint(error, Palette.Bad);
                else
                {
                    _message.text = "Claimed.";
                    GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
                }
            }));
        }

        private void Buy(int shopItemId)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.Buy(shopItemId, 1, error =>
            {
                _busy = false;
                ShopItem item = HuntShop.Find(shopItemId);
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : "Bought " + item?.Name + ".";
            }));
        }

        private static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            int h = seconds / 3600;
            return h >= 24 ? $"{h / 24}d {h % 24}h" : $"{h}h {seconds % 3600 / 60:00}m";
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            Inventory inv = _root.Session.Inventory;
            _marks.text = $"{inv.HuntMarks} Hunt Marks  ·  {inv.EtchingNeedles} Needles  ·  {inv.PinningWax} Wax" + (inv.MastersNeedles > 0 ? $"  ·  {inv.MastersNeedles} Master's" : "");
            var board = _root.Server.Bounties;
            int age = (int)(Time.realtimeSinceStartup - _root.Server.BountiesReceivedAt);
            _dailyTitle.text = board == null ? "DAILY" : "DAILY  ·  new bounties in " + Clock(board.dailyResetSeconds - age);
            _weeklyTitle.text = board == null ? "WEEKLY" : "WEEKLY  ·  new bounties in " + Clock(board.weeklyResetSeconds - age);

            int daily = 0, weekly = 0;
            for (int i = 0; i < Rows; i++) { _rows[i].Back.gameObject.SetActive(false); _rows[i].Label.text = ""; _rows[i].Claim.gameObject.SetActive(false); _rows[i].Fill.gameObject.SetActive(false); }
            if (board?.items == null) return;
            foreach (var b in board.items)
            {
                int slot = b.period == "Daily" ? daily++ : 5 + weekly++;
                if (slot >= Rows || (b.period == "Daily" && slot >= 5)) continue;
                Row r = _rows[slot];
                r.Id = b.id;
                r.Back.gameObject.SetActive(true);
                r.Fill.gameObject.SetActive(true);
                r.Claim.gameObject.SetActive(true);
                bool hunt = b.title.StartsWith("Hunt for");
                string count = hunt ? $"{b.count / 60}/{b.target / 60} min" : $"{b.count}/{b.target}";
                int trailXp = b.period == "Daily" ? CampaignTrail.DailyBountyXp : CampaignTrail.WeeklyBountyXp;
                r.Label.text = $"{b.title}   ·   {count}   ·   {b.marks} marks, {trailXp} Trail XP";
                r.Label.color = b.claimed ? Palette.Muted : Palette.Parchment;
                float done = b.target > 0 ? Mathf.Clamp01(b.count / (float)b.target) : 0f;
                r.Fill.anchorMax = new Vector2(0.045f + 0.69f * done, r.Fill.anchorMax.y);
                bool ready = !b.claimed && b.count >= b.target;
                // Unfinished bounties show how far along they are on a quiet plate; only a finished one glows green.
                r.ClaimLabel.text = b.claimed ? "CLAIMED" : ready ? "CLAIM" : $"{Mathf.FloorToInt(done * 100f)}%";
                r.Claim.GetComponent<Image>().color = ready ? Palette.Safe : Palette.ButtonIdle;
                r.Claim.interactable = ready && !_busy;
            }
            for (int i = 0; i < _buy.Length; i++)
                _buy[i].interactable = !_busy && _root.Server.Online && inv.HuntMarks >= HuntShop.Items[i].Marks;
        }
    }
}
