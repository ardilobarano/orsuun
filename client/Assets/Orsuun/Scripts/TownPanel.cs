using System.Collections.Generic;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// The town square (owner, 28 Sep 2026: picked "Town square in 3D"; TownScene is the place). The hero stands at the
    /// square's near end seen from behind; the Banner's starting town names it (Emberhearth, Wardenstep, Scalehouse). Tap
    /// one of the townsfolk (or the plate over his head) and the hero walks over, then his screen opens: Forgemaster Dorun
    /// the Forge, Ilke of the Scales the Caravan, Elder Tamir the skills, Pitmaster Bora the Pits (from their level). The
    /// newest rugs lie along the aisle, the hero's own first; a rug's tag opens it on RUG STALLS. The hunt goes on while
    /// the hero is in town (only the river stops it); BACK TO THE HUNT leaves. ZONES has the way in.
    /// </summary>
    public sealed class TownPanel : MonoBehaviour
    {
        private static readonly string[] FolkNames = { "Forgemaster Dorun", "Ilke of the Scales", "Elder Tamir", "Pitmaster Bora" };
        private static readonly string[] FolkRoles = { "The Forge", "The Caravan", "Skills", "The Pits" };
        private const float WalkSpeed = 4.2f, StopShort = 1.35f, RugsEvery = 60f;

        private GameRoot _root;
        private GameObject _canvas;
        private RectTransform _canvasRect;
        private HeroStage _stage;
        private TownScene _place;
        private Text _title, _message;
        private readonly RectTransform[] _hits = new RectTransform[4];
        private readonly RectTransform[] _plates = new RectTransform[4];
        private readonly RectTransform[] _tags = new RectTransform[6];
        private readonly Text[] _tagLabels = new Text[6];
        private readonly string[] _tagSellers = new string[6];
        private RugsDto _rugs;
        private float _rugsAt = -100f;
        private bool _fetching;
        private int _walkingTo = -1;
        private Vector3 _walkFrom, _walkTarget;
        private float _walkStart, _walkTime;
        private Banner _banner = (Banner)(-1);
        private int _shotWalk = -1, _shotVisit = -1;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TownCanvas", 6).gameObject;
            Transform canvas = _canvas.transform;
            _canvasRect = (RectTransform)canvas;
            RectTransform stageBox = Ui.Rect("Stage", canvas, 0f, 0f, 1f, 1f);
            Vector3 spot = HeroStage.Below + new Vector3(-240f, 0f, 0f);
            _stage = new GameObject("TownStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, spot, fromBehind: true, rod: false);
            _stage.ViewOverride = (spot + TownScene.CameraFrom, spot + TownScene.CameraTo);
            _stage.gameObject.SetActive(false);
            _place = new GameObject("TownScene").AddComponent<TownScene>();
            _place.Init(spot);

            Ui.Framed("TitleBack", canvas, 0.14f, 0.925f, 0.86f, 0.985f, new Color(0.05f, 0.04f, 0.04f, 0.72f)).raycastTarget = false;
            _title = Ui.Title("Title", canvas, 0.15f, 0.93f, 0.85f, 0.98f, "", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Raw(_title);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                // An invisible button over the townsman's figure, and his plate above him.
                Image hit = Ui.Panel("Folk" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0f, 0f, 0f, 0f));
                hit.gameObject.AddComponent<Button>().onClick.AddListener(() => Visit(index));
                _hits[i] = hit.rectTransform;
                Image plate = Ui.Framed("Plate" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0.07f, 0.05f, 0.04f, 0.86f));
                plate.gameObject.AddComponent<Button>().onClick.AddListener(() => Visit(index));
                _plates[i] = plate.rectTransform;
                Ui.Title("Name", plate.transform, 0.04f, 0.46f, 0.96f, 0.96f, FolkNames[i], 20, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Label("Role", plate.transform, 0.04f, 0.04f, 0.96f, 0.5f, FolkRoles[i], 17, TextAnchor.MiddleCenter, Palette.Sorn);
            }
            for (int i = 0; i < _tags.Length; i++)
            {
                int index = i;
                Image tag = Ui.Framed("Rug" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0.06f, 0.05f, 0.05f, 0.78f));
                tag.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenRug(index));
                _tags[i] = tag.rectTransform;
                _tagLabels[i] = Ui.Label("Label", tag.transform, 0.04f, 0.04f, 0.96f, 0.96f, "", 16, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Raw(_tagLabels[i]);
                _tagLabels[i].supportRichText = true;
                tag.gameObject.SetActive(false);
            }

            Ui.Framed("MessageBack", canvas, 0.06f, 0.09f, 0.94f, 0.135f, new Color(0.05f, 0.04f, 0.04f, 0.78f)).raycastTarget = false;
            _message = Ui.Label("Message", canvas, 0.08f, 0.092f, 0.92f, 0.133f, "", 21, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Rugs", canvas, 0.04f, 0.015f, 0.36f, 0.075f, "RUG STALLS", 22, Palette.ButtonForge, () => _root.Rugs.Open(), out _);
            Ui.Button("Leave", canvas, 0.38f, 0.015f, 0.96f, 0.075f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _stage.gameObject.SetActive(true);
            _place.Show();
            StopWalking();
            _message.text = "Tap someone in the square to go and see them.";
            _rugsAt = -100f;
            string[] args = System.Environment.GetCommandLineArgs();
            int walk = System.Array.IndexOf(args, "-townwalk");
            if (walk >= 0 && walk + 1 < args.Length) int.TryParse(args[walk + 1], out _shotWalk);
            int visit = System.Array.IndexOf(args, "-townvisit");
            if (visit >= 0 && visit + 1 < args.Length && int.TryParse(args[visit + 1], out int who)) _shotVisit = who;
        }

        public void Close()
        {
            _canvas.SetActive(false);
            _stage.Show(null, 0, 0, null);
            _stage.gameObject.SetActive(false);
            _place.Hide();
            StopWalking();
        }

        private void StopWalking()
        {
            _walkingTo = -1;
            _stage.Walk = Vector3.zero;
            _place.SetHeroAt(Vector3.zero);
            _stage.Facing = 0f;
            _stage.Loop("Idle");
        }

        /// <summary>The hero walks over to a townsman; his screen opens when he gets there.</summary>
        private void Visit(int index)
        {
            if (_walkingTo >= 0) return;
            if (index == 3 && !_root.Unlocked(Feature.Pits)) { _message.text = Unlocks.Locked(Feature.Pits); return; }
            Vector3 to = TownScene.Folk[index];
            Vector3 from = _stage.Walk;
            Vector3 way = to - from;
            way.y = 0f;
            _walkFrom = from;
            _walkTarget = to - way.normalized * StopShort;
            _walkTime = Mathf.Max(0.2f, (_walkTarget - from).magnitude / WalkSpeed);
            _walkStart = Time.time;
            _walkingTo = index;
            _stage.Facing = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            _stage.Loop("Run");
            _message.text = FolkNames[index];
        }

        private void Arrive(int index)
        {
            StopWalking();
            _message.text = "Tap someone in the square to go and see them.";
            switch (index)
            {
                case 0: _root.Forge.Open(); break;
                case 1: _root.Caravan.Open(); break;
                case 2: _root.Skills.Open(); break;
                default: _root.Pits.Open(); break;
            }
        }

        private void OpenRug(int index)
        {
            string seller = _tagSellers[index];
            if (!string.IsNullOrEmpty(seller)) _root.Rugs.Open(seller);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            Net.ServerLink server = _root.Server;
            // The river takes the hero out of town (another device may send him there); so does a change of hero.
            if (server.AtRiver || server.WaitingForHero) { Close(); return; }
            // A screen opened from the square covers it: the square's camera rests meanwhile.
            bool covered = _root.Forge.IsOpen || _root.Caravan.IsOpen || _root.Skills.IsOpen || _root.Pits.IsOpen || _root.Rugs.IsOpen
                           || _root.Gear.IsOpen || _root.Menu.IsOpen;
            if (_stage.gameObject.activeSelf == covered) _stage.gameObject.SetActive(!covered);
            if (covered) return;

            Banner banner = server.Banner;
            if (banner != _banner)
            {
                _banner = banner;
                _title.text = Loc.ToUpper(banner == Banner.None ? "The Town Square" : Banners.Def(banner).StartingTown);
                _place.SetBanner(banner);
            }

            PlayerSession s = _root.Session;
            ItemState armor = s.Equipped(EquipSlot.Armor), weapon = s.Equipped(EquipSlot.Weapon);
            string skin = null;
            foreach (WardrobeDef piece in s.Worn) if (piece.Kind == WardrobeKind.Skin) skin = piece.Look;
            _stage.Show(s.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, weapon != null ? ItemLooks.Tier(weapon.ItemLevel) : 0, skin,
                armor != null ? UpgradeGlow.ForLevel(armor.UpgradeLevel) : 0f, weapon != null ? UpgradeGlow.ForLevel(weapon.UpgradeLevel) : 0f, s.SecondLook);

            // Screenshots (-townvisit n): a whole visit, the walk and then the screen.
            if (_shotVisit >= 0 && Time.time > 4f)
            {
                Visit(_shotVisit);
                _shotVisit = -1;
            }
            // Screenshots (-townwalk n): the hero caught halfway to a townsman.
            if (_shotWalk >= 0 && Time.time > 4f)
            {
                Visit(_shotWalk);
                _walkTime = 600f;
                _walkStart = Time.time - 300f;
                _shotWalk = -1;
            }
            if (_walkingTo >= 0)
            {
                float k = Mathf.Clamp01((Time.time - _walkStart) / _walkTime);
                _stage.Walk = Vector3.Lerp(_walkFrom, _walkTarget, k);
                _place.SetHeroAt(_stage.Walk);
                if (k >= 1f) Arrive(_walkingTo);
            }

            if (server.Online && !_fetching && Time.time - _rugsAt > RugsEvery)
            {
                _fetching = true;
                _rugsAt = Time.time;
                StartCoroutine(server.FetchRugs((rugs, error) =>
                {
                    _fetching = false;
                    if (rugs != null) { _rugs = rugs; LayRugs(); }
                }));
            }
            PlaceLabels();
        }

        /// <summary>The newest rugs on the square's spots, the hero's own first.</summary>
        private void LayRugs()
        {
            var shown = new List<RugStallDto>();
            RugStallDto[] stalls = _rugs?.stalls ?? new RugStallDto[0];
            foreach (RugStallDto st in stalls) if (st.mine) shown.Add(st);
            foreach (RugStallDto st in stalls) if (!st.mine && shown.Count < TownScene.RugSpots.Length) shown.Add(st);
            var banners = new List<Banner?>();
            for (int i = 0; i < _tags.Length; i++)
            {
                bool has = i < shown.Count;
                _tagSellers[i] = has ? shown[i].sellerId : null;
                banners.Add(has ? BannerLook.Parse(shown[i].banner) : (Banner?)null);
                if (has)
                    {
                    int n = shown[i].wares;
                    // The seller's name as written; the rest in the player's language (the label is raw).
                    _tagLabels[i].text = (shown[i].mine ? Loc.T("Your rug") : shown[i].name) + "\n"
                                         + ConfirmDialog.Tint(Loc.T(n == 1 ? $"{n} ware" : $"{n} wares"), Palette.Sorn);
                }
            }
            _place.SetRugs(banners);
        }

        /// <summary>Plates over the townsfolk and tags over the rugs, where the square's camera shows them.</summary>
        private void PlaceLabels()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 feet = _place.World(TownScene.Folk[i]);
                Vector2 size = _place.FolkSize(i);
                Vector3 bottom = _stage.ScreenOf(feet), top = _stage.ScreenOf(feet + Vector3.up * size.y);
                Vector3 side = _stage.ScreenOf(feet + Vector3.right * (size.x * 0.5f));
                float halfWidth = Mathf.Max(Mathf.Abs(side.x - bottom.x), Screen.width * 0.05f);
                SetScreenRect(_hits[i], bottom.x - halfWidth, bottom.y, bottom.x + halfWidth, top.y);
                float plateW = Screen.width * 0.2f, plateH = Screen.height * 0.04f;
                SetScreenRect(_plates[i], top.x - plateW / 2f, top.y + Screen.height * 0.004f, top.x + plateW / 2f, top.y + Screen.height * 0.004f + plateH);
            }
            for (int i = 0; i < _tags.Length; i++)
            {
                bool has = _tagSellers[i] != null;
                if (_tags[i].gameObject.activeSelf != has) _tags[i].gameObject.SetActive(has);
                if (!has) continue;
                // Standing on the rug's far edge, so the rug shows under it.
                Vector3 at = _stage.ScreenOf(_place.World(TownScene.RugSpots[i] + new Vector3(0f, 0f, TownScene.RugWidth * 0.45f)));
                float w = Screen.width * 0.2f, h = Screen.height * 0.034f;
                SetScreenRect(_tags[i], at.x - w / 2f, at.y, at.x + w / 2f, at.y + h);
            }
        }

        /// <summary>Places a canvas child over a screen rectangle (pixels).</summary>
        private void SetScreenRect(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            rect.anchorMin = new Vector2(x0 / w, y0 / h);
            rect.anchorMax = new Vector2(x1 / w, y1 / h);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
