using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Korstone Rain on the field (owner, 7 Oct 2026; Rules.KorstoneRain): while a Giant Korstone stands on the hero's big map
    /// it stands at its camp (falling out of the sky with a streak, a horn and a quake the first time it is seen, if it fell
    /// lately), with its name over it, and a call on the HUD's call line (after a trade and a partymate's dungeon, before
    /// the Commander) strikes it. When it breaks it shatters where it stood and says who struck the last blow; the shower
    /// comes by letter. A NEW card explains it the first time.
    /// </summary>
    public sealed class KorstoneRainView : MonoBehaviour
    {
        public static readonly Color Violet = new Color(0.8f, 0.5f, 1f);
        private const float FallSeconds = 1.6f, FallHeight = 70f;

        private GameRoot _root;
        private LaneView _lane;
        private Button _call;
        private Text _callLabel;
        private Image _callImage;
        private Transform _stone;
        private KorstoneFx _fx;
        private TextMesh _tag;
        private long _stoneId = -1, _announced = -1, _shattered = -1, _labelKey = -1;
        private FieldMap.Layout _stoneOn;
        private float _fallAt = -1f, _height;
        private Vector3 _rest;
        private bool _tipOffered;
        /// <summary>-rainstrike strikes once the stone stands here (screenshots of the strike).</summary>
        private bool _strikeOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-rainstrike") >= 0;

        /// <summary>Its call is on the HUD's call line now.</summary>
        public bool Calling => _call != null && _call.gameObject.activeSelf;

        /// <summary>The stone on this big map now (its camp on the layout), or null.</summary>
        public (Net.ServerLink.RainDto Rain, int Camp, long Left)? Here { get; private set; }

        public void Init(GameRoot root, LaneView lane)
        {
            _root = root;
            _lane = lane;
            Rect line = Hud.CallLine;
            _call = Ui.Button("RainCall", root.Hud.Canvas, line.xMin, line.yMin, line.xMax, line.yMax, "", 18, new Color(0.42f, 0.22f, 0.6f), Strike, out _callLabel);
            _callLabel = Ui.Raw(_callLabel);
            _callLabel.supportRichText = true;
            _callImage = _call.GetComponent<Image>();
            _call.gameObject.SetActive(false);
        }

        /// <summary>Strikes the stone (the call, the full map's mark): refused with the reason when it cannot be.</summary>
        public void Strike()
        {
            if (Here is not { } here) return;
            if (here.Rain.broken) return;
            if (here.Rain.strikesLeft <= 0) { _root.Hud.Log("You have struck this stone three times: watch the others break it."); return; }
            _root.MapScreen.Close();
            _root.StrikeRain();
        }

        private void Update()
        {
            if (_root == null || _lane == null) return;
            FieldMap map = _lane.Map != null && _lane.Map.Active ? _lane.Map : null;
            Net.ServerLink.RainDto rain = _root.Server.Online && !_root.Server.WaitingForHero ? _root.Server.Rain : null;
            Here = null;
            if (map != null && rain != null && rain.map == map.Current.Map && map.Current.Camps != null && map.Current.Camps.Length > 0)
                Here = (rain, rain.camp % map.Current.Camps.Length, rain.secondsLeft - (long)(Time.realtimeSinceStartup - _root.Server.RainAt));
            bool show = Here != null && !_root.Town.IsOpen && !_root.Server.AtRiver;

            // The stone itself: made for this fall on this map, gone when it shatters, sinks or the hero leaves.
            if (!show || rain.id != _stoneId || map.Current != _stoneOn || rain.broken)
            {
                if (show && rain.broken && rain.id == _stoneId && _shattered != rain.id) Shatter(rain);
                Clear();
            }
            if (show && !rain.broken && _stone == null) Stand(map, rain, Here.Value.Camp);
            if (_stone != null) Animate();

            bool call = show && !rain.broken && !_root.Replaying && !_root.Hud.TradeCalling && !(_root.Party != null && _root.Party.Calling);
            if (_call.gameObject.activeSelf != call) _call.gameObject.SetActive(call);
            if (!show) return;
            (Net.ServerLink.RainDto r, int camp, long left) = Here.Value;
            if (call)
            {
                int percent = (int)(r.hpLeft * 100 / System.Math.Max(1, r.hpMax));
                long key = ((((r.id * 101 + percent) * 4 + r.strikesLeft) * 100_000 + System.Math.Max(0, left)) * 8) + (int)Loc.Current;
                if (key != _labelKey)
                {
                    _labelKey = key;
                    string where = map.Current.Camps[camp].Name;
                    string action = r.strikesLeft > 0 ? "<b>" + Loc.T($"STRIKE ×{r.strikesLeft}") + "</b>" : ConfirmDialog.Tint(Loc.T("NO STRIKES LEFT"), Palette.Muted);
                    _callLabel.text = $"{Loc.ToUpper(Loc.T(KorstoneRain.StoneName))}  ·  {Loc.ToUpper(Loc.T(where))}  ·  {percent}%  ·  {System.Math.Max(0, left) / 60}:{System.Math.Max(0, left) % 60:00}  ·  {action}";
                }
                _callImage.color = Color.Lerp(new Color(0.42f, 0.22f, 0.6f), new Color(0.6f, 0.32f, 0.85f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            }
            if (_strikeOnce && call && r.strikesLeft > 0 && !_root.PushBusy && Time.realtimeSinceStartup > 8f)
            {
                _strikeOnce = false;
                Strike();
            }
            // Once a fall: a card and a horn for every hero on the map, and the first time ever, what it is.
            if (r.id != _announced && !r.broken)
            {
                _announced = r.id;
                string where = map.Current.Camps[camp].Name;
                _root.Hud.Announce("A GIANT KORSTONE FALLS",
                    $"At {where}: every hero on {map.Current.Name} may strike it three times. Break it together for a shower of loot and rare Korshards.");
                GameAudio.Instance?.Play("CommanderHorn", 0.95f, 5f, 0f);
                if (!_tipOffered && !_root.Tutorial.Running && !_root.Story.Showing)
                {
                    _tipOffered = true;
                    _root.Tips.Offer(TipCard.Tip.Rain);
                }
            }
        }

        private void Stand(FieldMap map, Net.ServerLink.RainDto rain, int camp)
        {
            FieldMap.Spot c = map.Current.Camps[camp];
            // Beside the camp's monsters, a few metres off the camp's own spot.
            _rest = new Vector3(c.X + 3.5f, 0f, c.Z + 3f);
            _stone = _lane.GiantKorstone(map.Root, _rest, KorstoneRain.Stage(rain.map).GearItemLevel, out _fx, out _height);
            if (_stone == null) return;
            _stone.name = "GiantKorstone";
            _stoneId = rain.id;
            _stoneOn = map.Current;
            // It falls out of the sky if it fell lately and this is the first time the hero sees it.
            _fallAt = rain.secondsSinceFall < 40 && _announced != rain.id ? Time.time : -1f;
            if (_fallAt > 0f) _stone.localPosition = _rest + Vector3.up * FallHeight;
            var go = new GameObject("GiantKorstoneName");
            go.transform.SetParent(_stone, false);
            go.transform.position = _stone.position + Vector3.up * (_height + 0.8f);
            _tag = go.AddComponent<TextMesh>();
            _tag.font = Ui.Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            _tag.text = Loc.ToUpper(Loc.T(KorstoneRain.StoneName));
            _tag.fontSize = 64;
            _tag.characterSize = 0.09f / Mathf.Max(0.01f, _stone.lossyScale.x);
            _tag.anchor = TextAnchor.MiddleCenter;
            _tag.color = Violet;
        }

        /// <summary>The fall (eased in, a streak of sparks), the quake where it lands, then the stone's own slow glow.</summary>
        private void Animate()
        {
            if (_tag != null && Camera.main != null)
            {
                _tag.transform.position = _stone.position + Vector3.up * (_height + 0.8f);
                _tag.transform.rotation = Camera.main.transform.rotation;
            }
            if (_fallAt < 0f) return;
            float t = Mathf.Clamp01((Time.time - _fallAt) / FallSeconds);
            _stone.localPosition = _rest + Vector3.up * (FallHeight * (1f - t * t));
            if (_fx != null && Random.value < 0.6f) _fx.Wave(6, 2f);
            if (t < 1f) return;
            _fallAt = -1f;
            _stone.localPosition = _rest;
            ActionCamera.Shake(0.35f);
            if (_fx != null) _fx.Wave(140, 10f);
            GameAudio.Instance?.Play("BossSlam", 1f, 0.5f, 0f);
            GameAudio.Instance?.Play("LaneKorstoneBreak", 0.7f, 0.5f, 0f);
        }

        /// <summary>The stone breaks where it stood: a burst, a quake, the sound, and who struck the last blow.</summary>
        private void Shatter(Net.ServerLink.RainDto rain)
        {
            _shattered = rain.id;
            if (_fx != null) _fx.Wave(220, 12f);
            // The stone goes at once, its burst of sparks a moment later.
            if (_stone != null)
            {
                foreach (MeshRenderer r in _stone.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                _stone.SetParent(_stone.parent, true);
                Destroy(_stone.gameObject, 3f);
                _stone = null;
            }
            ActionCamera.Shake(0.25f);
            GameAudio.Instance?.Play("LaneKorstoneBreak", 1f, 0.5f, 0f);
            _root.Hud.Announce("THE GIANT KORSTONE BREAKS",
                (string.IsNullOrEmpty(rain.brokenBy) ? "It broke" : $"{rain.brokenBy} struck the last blow") + ": its shower comes to every striker and hunter here by letter.");
        }

        private void Clear()
        {
            if (_stone != null) Destroy(_stone.gameObject);
            _stone = null;
            _fx = null;
            _tag = null;
            _stoneId = -1;
            _stoneOn = null;
            _fallAt = -1f;
        }
    }
}
