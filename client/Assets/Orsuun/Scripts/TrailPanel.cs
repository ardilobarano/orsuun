using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// THE CAMPAIGN TRAIL (GDD section 9, the battle pass; Rules.CampaignTrail): this season's 50 tiers climbed with the XP
    /// bounties pay, a free track and a paid one bought with Amber (Trail Plus adds ten tiers). The hero stands at the top
    /// in the season's costume, the paid track's first reward. Tap a row to claim its rewards, or CLAIM ALL.
    /// </summary>
    public sealed class TrailPanel : MonoBehaviour
    {
        private sealed class Row
        {
            public Image Back;
            public Text Tier;
            public Image FreeBack, PaidBack;
            public RawImage FreeIcon, PaidIcon;
            public Text Free, Paid;
        }

        private static readonly Color Ready = new Color(0.42f, 0.28f, 0.06f, 0.97f);
        private static readonly Color Plain = new Color(0.07f, 0.07f, 0.13f, 0.95f);
        private static readonly Color Taken = new Color(0.05f, 0.05f, 0.08f, 0.8f);

        private GameRoot _root;
        private GameObject _canvas;
        private HeroStage _stage;
        private ConfirmDialog _confirm;
        private ScrollRect _scroll;
        private RectTransform _list;
        private readonly Row[] _rows = new Row[CampaignTrail.Tiers];
        private Text _season;
        private Text _balance;
        private Text _tier;
        private Text _xp;
        private RectTransform _xpFill;
        private Text _left;
        private Text _costume;
        private Text _mount;
        private Button _buyTrail, _buyPlus;
        private Text _buyTrailLabel, _buyPlusLabel;
        private Button _claimAll;
        private Text _claimAllLabel;
        private Text _paidHead;
        private Text _message;
        private string _shownKey = "";
        private int _scrollTo = -1;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TrailCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Trail");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "CAMPAIGN TRAIL", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _season = Ui.Title("Season", canvas, 0.03f, 0.886f, 0.6f, 0.926f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Sliced("BalancePill", canvas, 0.64f, 0.886f, 0.97f, 0.926f, "Pill", Color.white).raycastTarget = false;
            RectTransform amberBox = Ui.Rect("AmberIconBox", canvas, 0.65f, 0.888f, 0.7f, 0.924f);
            Ui.Icon("AmberIcon", amberBox, 0f, 0f, 1f, 1f, "Amber");
            _balance = Ui.Title("Balance", canvas, 0.705f, 0.886f, 0.88f, 0.926f, "", 28, TextAnchor.MiddleLeft, Palette.Sorn);
            Ui.Button("More", canvas, 0.885f, 0.888f, 0.965f, 0.924f, "+", 30, Palette.ButtonForge, () => { Close(); _root.Caravan.Open(3); }, out _);

            // The season's showcase: the hero in the costume, the tier, the XP, what the season gives.
            Ui.Framed("Showcase", canvas, 0.03f, 0.64f, 0.97f, 0.878f, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            RectTransform stageBox = Ui.Rect("StageBox", canvas, 0.035f, 0.645f, 0.4f, 0.873f);
            _stage = new GameObject("TrailStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, HeroStage.Below + new Vector3(60f, 0f, 0f));
            _stage.gameObject.SetActive(false);
            _tier = Ui.Title("Tier", canvas, 0.42f, 0.822f, 0.96f, 0.87f, "", 40, TextAnchor.MiddleLeft, Palette.Sorn);
            Ui.Bar("Xp", canvas, 0.42f, 0.79f, 0.96f, 0.818f, CaravanPanel.AmberColor, out Image fill);
            _xpFill = fill.rectTransform;
            _xp = Ui.Label("XpText", canvas, 0.42f, 0.762f, 0.96f, 0.79f, "", 21, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Label("Source", canvas, 0.42f, 0.73f, 0.96f, 0.762f,
                $"Bounties pay Trail XP: {CampaignTrail.DailyBountyXp} a daily, {CampaignTrail.WeeklyBountyXp} a weekly.", 19, TextAnchor.MiddleLeft, Palette.Muted);
            _costume = Ui.Label("Costume", canvas, 0.42f, 0.698f, 0.96f, 0.73f, "", 20, TextAnchor.MiddleLeft, CaravanPanel.AmberColor);
            _mount = Ui.Label("Mount", canvas, 0.42f, 0.668f, 0.96f, 0.698f, "", 20, TextAnchor.MiddleLeft, CaravanPanel.AmberColor);
            _left = Ui.Label("Left", canvas, 0.42f, 0.648f, 0.96f, 0.668f, "", 18, TextAnchor.MiddleLeft, Palette.Muted);

            _buyTrail = Ui.Button("BuyTrail", canvas, 0.03f, 0.578f, 0.49f, 0.632f, "", 24, Palette.Danger, () => AskBuy(false), out _buyTrailLabel);
            _buyPlus = Ui.Button("BuyPlus", canvas, 0.51f, 0.578f, 0.97f, 0.632f, "", 24, Palette.Danger, () => AskBuy(true), out _buyPlusLabel);
            _buyTrailLabel.supportRichText = true;
            _buyPlusLabel.supportRichText = true;

            Ui.Title("TierHead", canvas, 0.03f, 0.538f, 0.16f, 0.572f, "TIER", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Title("FreeHead", canvas, 0.17f, 0.538f, 0.57f, 0.572f, "FREE", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _paidHead = Ui.Title("PaidHead", canvas, 0.58f, 0.538f, 0.97f, 0.572f, "THE TRAIL", 22, TextAnchor.MiddleCenter, CaravanPanel.AmberColor);
            _scroll = Ui.Scroll("Tiers", canvas, 0.03f, 0.135f, 0.97f, 0.535f, out _list);
            for (int t = 1; t <= CampaignTrail.Tiers; t++) _rows[t - 1] = BuildRow(t);

            _message = Ui.Label("Message", canvas, 0.04f, 0.078f, 0.96f, 0.13f, "", 21, TextAnchor.MiddleCenter, Palette.Warn);
            _claimAll = Ui.Button("ClaimAll", canvas, 0.03f, 0.015f, 0.47f, 0.068f, "CLAIM ALL", 26, Palette.Safe, () => Claim(0), out _claimAllLabel);
            Ui.Button("Close", canvas, 0.5f, 0.015f, 0.97f, 0.068f, "BACK TO THE HUNT", 24, Palette.ButtonIdle, Close, out _);

            _confirm = new GameObject("TrailConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        private Row BuildRow(int tier)
        {
            var r = new Row();
            r.Back = Ui.Framed("Tier" + tier, _list, 0f, 0f, 1f, 1f, Plain);
            r.Back.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
            r.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => Claim(tier));
            r.Back.gameObject.AddComponent<Press>();
            r.Tier = Ui.Title("Number", r.Back.transform, 0f, 0f, 0.14f, 1f, tier.ToString(), 34, TextAnchor.MiddleCenter, Palette.Parchment);
            (r.FreeBack, r.FreeIcon, r.Free) = Cell(r.Back.transform, "Free", 0.15f, 0.565f);
            (r.PaidBack, r.PaidIcon, r.Paid) = Cell(r.Back.transform, "Paid", 0.575f, 0.99f);
            return r;
        }

        private static (Image, RawImage, Text) Cell(Transform row, string name, float x0, float x1)
        {
            Image back = Ui.Sliced(name, row, x0, 0.08f, x1, 0.92f, "CardFill", Plain);
            back.raycastTarget = false;
            RectTransform iconBox = Ui.Rect("IconBox", back.transform, 0.03f, 0.1f, 0.25f, 0.9f);
            RawImage icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Turnstone");
            Text text = Ui.Label("Text", back.transform, 0.27f, 0.06f, 0.98f, 0.94f, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
            text.supportRichText = true;
            return (back, icon, text);
        }

        private static string IconOf(TrailReward r)
        {
            if (r.Piece != null) return Wardrobe.Find(r.Piece)?.Kind == WardrobeKind.Mount ? "WardrobeMount" : "WardrobeSkin";
            if (r.AnvilWards > 0) return "AnvilWard";
            if (r.KhansAlloys > 0) return "KhansAlloy";
            if (r.ScrollsOfMercy > 0) return "ScrollOfMercy";
            return "Turnstone";
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "" : "The Campaign Trail needs the server.";
            _shownKey = "";
            _canvas.SetActive(true);
            _stage.gameObject.SetActive(true);
            _scrollTo = 2;   // two frames: the list lays itself out first
        }

        public void Close()
        {
            _canvas.SetActive(false);
            _stage.gameObject.SetActive(false);
        }

        private static bool Bit(long mask, int tier) => (mask & (1L << (tier - 1))) != 0;

        private static bool IsReady(Net.ServerLink.TrailDto t, int tier, bool paid) =>
            t != null && tier <= t.tier && !Bit(paid ? t.paidClaimed : t.freeClaimed, tier) && (!paid || t.pass > 0);

        /// <summary>Anything to claim (the HUD's Trail button says CLAIM).</summary>
        public static bool AnyReady(Net.ServerLink.TrailDto t) => ReadyCount(t) > 0;

        private static int ReadyCount(Net.ServerLink.TrailDto t)
        {
            int n = 0;
            for (int tier = 1; tier <= CampaignTrail.Tiers; tier++)
            {
                if (IsReady(t, tier, false)) n++;
                if (IsReady(t, tier, true)) n++;
            }
            return n + (t?.owed ?? 0);
        }

        private void Claim(int tier)
        {
            var t = _root.Server.Trail;
            if (_busy || t == null || !_root.Server.Online) return;
            if (tier > 0 && !IsReady(t, tier, false) && !IsReady(t, tier, true))
            {
                _message.text = tier > t.tier ? $"Tier {tier} is {(tier - t.tier) * CampaignTrail.XpPerTier - t.xpIntoTier:N0} Trail XP away."
                    : t.pass == 0 && !Bit(t.paidClaimed, tier) ? "The Trail's reward needs the Trail." : "Taken.";
                return;
            }
            _busy = true;
            StartCoroutine(_root.Server.TrailClaim(tier, error =>
            {
                _busy = false;
                _message.text = error ?? "Claimed.";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
            }));
        }

        private void AskBuy(bool plus)
        {
            var t = _root.Server.Trail;
            if (_busy || t == null) return;
            var from = (TrailPass)t.pass;
            TrailPass to = plus ? TrailPass.Plus : TrailPass.Trail;
            int price = CampaignTrail.Price(from, to);
            if (price < 0) return;
            if (_root.Server.Amber < price)
            {
                _message.text = $"Not enough Amber: {price:N0} needed.";
                return;
            }
            TrailSeason season = CampaignTrail.Season(t.season);
            string what = plus
                ? $"Trail Plus: the paid track and {CampaignTrail.PlusTiers} tiers at once" + (from == TrailPass.Trail ? " (you have the Trail: you pay the difference)" : "")
                : "The Trail: the paid track";
            string body = $"{what}, for this hero, for {price:N0} Amber.\nSeason {season.Number}, {season.Name}: {Wardrobe.Find(season.Costume)?.Name} at tier 1, "
                          + $"{Wardrobe.Find(season.Mount)?.Name} at tier {CampaignTrail.Tiers}.";
            _confirm.Show(plus ? "TRAIL PLUS" : "THE TRAIL", body, "BUY  " + price.ToString("N0"), Palette.Danger, () =>
            {
                _busy = true;
                StartCoroutine(_root.Server.TrailBuy(plus, error =>
                {
                    _busy = false;
                    _message.text = error ?? (plus ? "Trail Plus is yours: ten tiers climbed." : "The Trail is yours: claim its rewards.");
                    if (error == null) GameAudio.Instance?.Play("LaneKorstoneBreak", 0.9f, 0.3f, 0f);
                }));
            });
        }

        private static string Left(long seconds)
        {
            long days = seconds / 86400, hours = seconds % 86400 / 3600;
            return days > 0 ? $"{days}d {hours}h" : $"{hours}h {seconds % 3600 / 60}m";
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var t = _root.Server.Trail;
            _balance.text = _root.Server.Amber.ToString("N0");
            TrailSeason season = CampaignTrail.Season(t?.season ?? 1);
            WardrobeDef costume = Wardrobe.Find(season.Costume), mount = Wardrobe.Find(season.Mount);

            // The hero in the season's costume, as the paid track's tier 1 gives it.
            ItemState armor = _root.Session.Equipped(EquipSlot.Armor);
            _stage.Show(_root.Session.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, ItemLooks.Tier(_root.Session.Weapon.ItemLevel), costume?.Look,
                secondLook: _root.Session.SecondLook);

            _season.text = $"Season {season.Number} · {season.Name}";
            _left.text = t == null ? "" : $"Ends in {Left(_root.Server.TrailSecondsLeft)}.";
            _costume.text = $"Tier 1: {costume?.Name} ({costume?.PerkText})";
            _mount.text = $"Tier {CampaignTrail.Tiers}: {mount?.Name} ({mount?.PerkText})";
            int tier = t?.tier ?? 0;
            _tier.text = $"TIER {tier} / {CampaignTrail.Tiers}";
            bool top = tier >= CampaignTrail.Tiers;
            _xp.text = t == null ? "" : top ? $"{t.xp:N0} Trail XP: the Trail is climbed." : $"{t.xpIntoTier:N0} / {CampaignTrail.XpPerTier:N0} Trail XP to tier {tier + 1}";
            _xpFill.anchorMax = new Vector2(top ? 1f : (t?.xpIntoTier ?? 0) / (float)CampaignTrail.XpPerTier, 1f);

            var pass = (TrailPass)(t?.pass ?? 0);
            bool online = _root.Server.Online && t != null;
            string InAmber(int price) => ConfirmDialog.Tint(price.ToString("N0"), CaravanPanel.AmberColor);
            _buyTrail.interactable = online && !_busy && pass == TrailPass.None;
            _buyTrailLabel.text = pass == TrailPass.None ? "THE TRAIL  " + InAmber(CampaignTrail.TrailAmber) : "THE TRAIL: YOURS";
            _buyPlus.interactable = online && !_busy && pass != TrailPass.Plus;
            _buyPlusLabel.text = pass == TrailPass.Plus ? "TRAIL PLUS: YOURS"
                : $"PLUS +{CampaignTrail.PlusTiers} TIERS  " + InAmber(CampaignTrail.Price(pass, TrailPass.Plus));
            _buyTrail.targetGraphic.color = pass == TrailPass.None ? Palette.Danger : Palette.ButtonIdle;
            _buyPlus.targetGraphic.color = pass == TrailPass.Plus ? Palette.ButtonIdle : Palette.Danger;
            _paidHead.text = pass == TrailPass.None ? "THE TRAIL (LOCKED)" : "THE TRAIL";
            int ready = ReadyCount(t);
            _claimAll.interactable = online && !_busy && ready > 0;
            _claimAllLabel.text = ready > 0 ? $"CLAIM ALL ({ready})" : "CLAIM ALL";
            if (t != null && t.owed > 0 && _message.text.Length == 0)
                _message.text = $"Last season left {t.owed} rewards unclaimed: CLAIM ALL hands them over.";

            string key = t == null ? "none" : $"{t.season}/{t.tier}/{t.pass}/{t.freeClaimed}/{t.paidClaimed}";
            if (key != _shownKey)
            {
                _shownKey = key;
                for (int i = 1; i <= CampaignTrail.Tiers; i++) Paint(_rows[i - 1], i, t, season);
            }
            if (_scrollTo >= 0 && --_scrollTo < 0)
            {
                // Open on the first reward waiting (or the next tier to reach), with the row before it showing.
                Canvas.ForceUpdateCanvases();
                int first = Mathf.Min(tier + 1, CampaignTrail.Tiers);
                for (int i = tier; i >= 1; i--)
                    if (IsReady(t, i, false) || IsReady(t, i, true)) first = i;
                RectTransform view = _scroll.viewport;
                float rows = _list.rect.height > 0f ? view.rect.height / (_list.rect.height / CampaignTrail.Tiers) : 6f;
                float at = Mathf.Clamp01((first - 2) / Mathf.Max(1f, CampaignTrail.Tiers - rows));
                _scroll.verticalNormalizedPosition = 1f - at;
            }
        }

        private void Paint(Row r, int tier, Net.ServerLink.TrailDto t, TrailSeason season)
        {
            bool reached = t != null && tier <= t.tier;
            bool locked = t == null || t.pass == 0;
            r.Tier.color = reached ? Palette.Sorn : Palette.Muted;
            r.Back.color = reached ? new Color(0.1f, 0.09f, 0.14f, 0.96f) : Plain;
            PaintCell(r.FreeBack, r.FreeIcon, r.Free, CampaignTrail.Free(tier), reached, t != null && Bit(t.freeClaimed, tier), false);
            PaintCell(r.PaidBack, r.PaidIcon, r.Paid, CampaignTrail.Paid(tier, season), reached, t != null && Bit(t.paidClaimed, tier), locked);
        }

        private static void PaintCell(Image back, RawImage icon, Text text, TrailReward reward, bool reached, bool taken, bool locked)
        {
            Ui.SetIcon(icon, IconOf(reward));
            bool ready = reached && !taken && !locked;
            back.color = taken ? Taken : ready ? Ready : Plain;
            icon.color = taken || locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            string name = reward.Piece != null ? ConfirmDialog.Tint(reward.Text, CaravanPanel.AmberColor) : reward.Text;
            string state = taken ? "<color=#8A8578>TAKEN</color>" : ready ? "<color=#FFD76B>TAP TO CLAIM</color>"
                : locked ? "<color=#8A8578>THE TRAIL</color>" : "";
            text.text = state.Length > 0 ? name + "\n<size=16>" + state + "</size>" : name;
            text.color = taken ? Palette.Muted : Palette.Parchment;
        }
    }
}
