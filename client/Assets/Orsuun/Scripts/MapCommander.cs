using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// Map world bosses (owner, 29 Sep 2026: "Map world bosses"). While a Commander is up (its spawn window on the server's
    /// clock, BossDef.WindowSeconds every RespawnSeconds), it stands at its landmark's camp on the big maps it belongs to:
    /// Old Greyjaw at the Oathfields' Wolf Den and the Ember Steppe's Wolf Hill, Warlord Tul-Gorak at Gorak Pass's Drum
    /// Ground and the war camp's War Drums, the Mirage Queen at the Salt Sea's Oasis Shrine and the Salt Flats' Mirage Flats.
    /// Every hero on such a map is called: a card and a horn when it rises, then a call on the HUD with its time left, and a
    /// gold crown on the minimap and the full map. The call (or the crown's name on the full map) fights it through the
    /// Commanders' own fight (GameRoot.FightBoss: one fight a spawn, the shared pool). Presentation only: the clock, the
    /// pool and the fight are the server's.
    /// </summary>
    public sealed class MapCommander : MonoBehaviour
    {
        /// <summary>Which Commander (BossDef id) shows on which big map (its layout's file name), at which camp.</summary>
        public static readonly (int Boss, string Layout, string Camp)[] Sightings =
        {
            (3, "Oathfields", "Wolf Den"), (3, "EmberSteppe", "Wolf Hill"),
            (1, "GorakPass", "The Drum Ground"), (1, "GorakWarCamp", "War Drums"),
            (2, "SaltSea", "Oasis Shrine"), (2, "SaltFlats", "Mirage Flats"),
        };

        /// <summary>A Commander towers over the monsters round it (three times the lane's mob size).</summary>
        private const float Size = 3f;
        public static readonly Color Gold = new Color(1f, 0.78f, 0.25f);

        private GameRoot _root;
        private LaneView _lane;
        private Transform _model;
        private Animation _anim;
        private TextMesh _tag;
        private Renderer[] _body;
        private int _modelBoss = -1;
        private FieldMap.Layout _modelOn;
        private Button _call;
        private Text _callLabel;
        private string _announced = "";
        private float _nextRoar;

        /// <summary>The Commander up on this big map now: its id, name, camp and seconds left (null: none).</summary>
        public (int Boss, string Name, int Camp, long Left, bool Fought)? Here { get; private set; }

        public void Init(GameRoot root, LaneView lane)
        {
            _root = root;
            _lane = lane;
            _call = Ui.Button("CommanderCall", root.Hud.Canvas, 0.24f, 0.742f, 0.89f, 0.776f, "", 18, Palette.Danger, Fight, out _callLabel);
            _callLabel = Ui.Raw(_callLabel);
            _callLabel.supportRichText = true;
            _call.gameObject.SetActive(false);
        }

        /// <summary>Fights the Commander up here (the call, the full map's crown): refused with the reason when it cannot be.</summary>
        public void Fight()
        {
            if (Here is not { } here) return;
            BossDef boss = Content.Boss(here.Boss);
            if (boss == null) return;
            if (!_root.Unlocked(Feature.Commanders)) { _root.Hud.Log(Unlocks.Locked(Feature.Commanders)); return; }
            if (!Content.IsUnlocked(boss.ZoneId, _root.Session.HighestStageCleared)) { _root.Hud.Log("Clear campaign stage 5 to fight the Commanders."); return; }
            if (here.Fought) { _root.Hud.Log("You already fought " + boss.Name + " this spawn."); return; }
            _root.MapScreen.Close();
            _root.FightBoss(here.Boss);
        }

        private void Update()
        {
            if (_root == null || _lane == null) return;
            FieldMap map = _lane.Map != null && _lane.Map.Active ? _lane.Map : null;
            Here = map != null && _root.Server.Online && !_root.Server.WaitingForHero ? Find(map.Current) : null;
            bool show = Here != null && !_root.Replaying && !_root.Town.IsOpen && !_root.Server.AtRiver;

            if (!show || _modelBoss != Here.Value.Boss || _modelOn != map.Current || _model == null) Clear();
            if (show && _model == null) Stand(map, Here.Value.Boss, Here.Value.Camp);
            if (_model != null) Animate();

            if (_call.gameObject.activeSelf != show) _call.gameObject.SetActive(show);
            if (!show) return;
            (int id, string name, int camp, long left, bool fought) = Here.Value;
            string where = map.Current.Camps[camp].Name;
            string state = fought ? ConfirmDialog.Tint(Loc.T("FOUGHT"), Palette.Muted)
                : !_root.Unlocked(Feature.Commanders) ? Loc.T("LV " + Unlocks.Level(Feature.Commanders)) : "<b>" + Loc.T("FIGHT") + "</b>";
            _callLabel.text = $"{Loc.ToUpper(Loc.T(name))}  ·  {Loc.ToUpper(Loc.T(where))}  ·  {left / 60}:{left % 60:00}  ·  {state}";

            // Once a spawn: a card and a horn for every hero on the map.
            string key = id + "@" + Mathf.RoundToInt((Time.realtimeSinceStartup + left) / 60f);
            if (key == _announced) return;
            _announced = key;
            if (fought) return;
            _root.Hud.Announce($"{name.ToUpperInvariant()} HAS RISEN", $"At {where}: every hero on {map.Current.Name} is called. Tap the call to fight.");
            GameAudio.Instance?.Play("KorstoneAwaken", 1f, 1f, 0f);
            GameAudio.Instance?.Play("BossSlam", 0.7f, 0.2f, 0f);
        }

        /// <summary>The Commander up on this layout now, from the server's last word on the Commanders.</summary>
        private (int, string, int, long, bool)? Find(FieldMap.Layout layout)
        {
            if (layout?.Camps == null) return null;
            BossStatusDto[] bosses = _root.Server.Bosses;
            if (bosses == null) return null;
            long since = (long)(Time.realtimeSinceStartup - _root.Server.BossesReceivedAt);
            foreach ((int boss, string key, string campName) in Sightings)
            {
                if (key != layout.Key) continue;
                int camp = System.Array.FindIndex(layout.Camps, c => c.Name == campName);
                if (camp < 0) continue;
                foreach (BossStatusDto s in bosses)
                {
                    if (s.bossId != boss || !s.up || s.slain) continue;
                    long left = s.secondsLeft - since;
                    if (left <= 0) continue;
                    return (boss, Content.Boss(boss)?.Name ?? s.name, camp, left, s.foughtThisSpawn);
                }
            }
            return null;
        }

        /// <summary>The Commander's model at its camp, a head taller than the lane's, its name over it in gold.</summary>
        private void Stand(FieldMap map, int bossId, int camp)
        {
            BossDef boss = Content.Boss(bossId);
            string model = boss == null ? null : boss.Name.Contains("Greyjaw") ? "Greyjaw" : boss.Name.Contains("Gorak") ? "Gorak" : "Queen";
            _model = model == null ? null : _lane.CommanderModel(model, out _anim);
            if (_model == null) return;
            _modelBoss = bossId;
            _modelOn = map.Current;
            FieldMap.Spot c = map.Current.Camps[camp];
            _model.SetParent(map.Root, false);
            _model.localPosition = new Vector3(c.X, 0f, c.Z);
            _model.localScale *= Size / Mathf.Max(0.01f, map.Root.lossyScale.x);
            _body = _model.GetComponentsInChildren<Renderer>();
            // Measured at the bind pose (a skinned mesh's own bounds are not updated until it is drawn).
            float top = Mathf.Max(2f, HeroFigure.Measure(new System.Collections.Generic.List<Renderer>(_body), _model.position).max.y - _model.position.y);
            var go = new GameObject("CommanderName");
            go.transform.SetParent(_model, false);
            go.transform.position = _model.position + Vector3.up * (top + 0.5f);
            _tag = go.AddComponent<TextMesh>();
            _tag.font = Ui.Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            _tag.text = Loc.ToUpper(Loc.T(boss.Name));
            _tag.fontSize = 64;
            _tag.characterSize = 0.09f / _model.lossyScale.x;
            _tag.anchor = TextAnchor.MiddleCenter;
            _tag.color = Gold;
            _nextRoar = Time.time + 1.5f;
        }

        /// <summary>It watches the hero, roars now and then, and its colours smoulder.</summary>
        private void Animate()
        {
            Vector3 hero = _lane.transform.TransformPoint(new Vector3(LaneView.HeroLaneX, 0f, 0f));
            Vector3 look = hero - _model.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                _model.rotation = Quaternion.Slerp(_model.rotation, Quaternion.LookRotation(look, Vector3.up), 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (_tag != null && Camera.main != null) _tag.transform.rotation = Camera.main.transform.rotation;
            if (_anim != null && Time.time >= _nextRoar)
            {
                _nextRoar = Time.time + Random.Range(5f, 8f);
                if (_anim.GetClip("Attack") != null)
                {
                    _anim.CrossFade("Attack", 0.1f);
                    _anim.CrossFadeQueued("Idle", 0.3f);
                }
            }
            float glow = 1f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f));
            foreach (Renderer r in _body)
                if (r != null && r.GetComponent<TextMesh>() == null) r.material.color = new Color(glow, 0.8f + 0.2f * glow * 0.8f, 0.75f);
        }

        private void Clear()
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            _anim = null;
            _tag = null;
            _body = null;
            _modelBoss = -1;
            _modelOn = null;
        }
    }
}
