using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// Other players in the field (owner, 28 Sep 2026: picked "C + Other players" for the Metin2-style hunt): the heroes
    /// hunting the same map now (/v1/field, every 30 seconds, up to four) stand beside the road up ahead, each fighting a
    /// small group of the map's own monsters: their blows, the monsters' flinches and falls, new ones after. It is only a
    /// picture of their hunt (their real fights are their own lanes). On the road field they pass by with the road while
    /// the hero runs and come round again further up; on a big map (FieldMap) each hunts at one of its camps, and the hero
    /// meets them as his trail passes. A name over each. The big map's other camps have their monsters waiting, idling in
    /// threes (offline too). Tapping a hunter (29 Sep 2026: "Tap heroes on the map") opens the hero card (HeroActions).
    /// Partymates (29 Sep 2026: "Hunting parties"; the server lists them first) are no picture at a camp: they walk beside
    /// the hero, run when he runs and swing at his fight, with their names in the party's green.
    /// </summary>
    public sealed class FieldFolk : MonoBehaviour
    {
        private const float PollSeconds = 30f, RunSpeed = 6f, BehindX = -16f, AheadSpan = 44f;
        /// <summary>Where the hunters stand up the road (x) and to which side (z); the camera's side only well ahead.</summary>
        private static readonly Vector2[] Spots = { new Vector2(3.5f, 5.5f), new Vector2(8.5f, -5.5f), new Vector2(13f, 7f), new Vector2(19f, -6f) };
        /// <summary>Where partymates walk, from the hero (x along the road, z away from the camera): on the far side of him
        /// and a step behind, clear of the Bannerkin and out of the camera's line to the fight.</summary>
        private static readonly Vector2[] MateSpots = { new Vector2(0.9f, 3.3f), new Vector2(-1.9f, 1.3f), new Vector2(2.8f, 4.4f) };
        public static readonly Color MateGreen = new Color(0.55f, 0.95f, 0.55f);

        private sealed class Mob
        {
            public Transform Root;
            public Animation Anim;
            public int Hp;
            public float DeadAt = -1f;
        }

        private sealed class Hunter
        {
            public string Id, Key, Player;
            public Transform Root;
            public HeroFigure Figure;
            public TextMesh Name;
            public readonly List<Mob> Mobs = new List<Mob>();
            public float NextBlow, NextMob;
            /// <summary>Standing at a big map's camp (it moves with the map, never along the road), and which.</summary>
            public bool OnMap;
            public int Camp = -1;
            public int Picks;
            /// <summary>A partymate walking beside the hero (never at a camp).</summary>
            public bool Mate;
            public string Loop;
        }

        private GameRoot _root;
        private LaneView _lane;
        private readonly Hunter[] _hunters = new Hunter[Spots.Length];
        private MaterialPropertyBlock _block;
        private float _polledAt = -100f;
        private bool _fetching;
        private int _stage;
        private bool _pickForShot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-heropick") >= 0;
        private Transform[] _camps = new Transform[0];
        private FieldMap.Layout _campsOf;
        /// <summary>The camp flying the golden banner (Rules.EliteCamps) as shown, and its banner.</summary>
        private int _eliteShown = -2;
        private Transform _eliteBanner;

        /// <summary>A party dungeon's replay (GameRoot): the partymates to show beside the hero on its floors (null: none), in
        /// place of the field's hunters.</summary>
        public TownHeroDto[] DungeonMates;
        private TownHeroDto[] _matesShown;

        public void Init(GameRoot root, LaneView lane)
        {
            _root = root;
            _lane = lane;
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_root == null || _lane == null) return;
            Net.ServerLink server = _root.Server;
            // A new place: the old place's hunters leave with it.
            if (_lane.StageNow != _stage)
            {
                _stage = _lane.StageNow;
                for (int i = 0; i < _hunters.Length; i++) Clear(i);
                _matesHere.Clear();
                _polledAt = -100f;
                _campsOf = null;   // the new stage's own monsters
            }
            Camps();
            // A party dungeon's floors: its partymates beside the hero, and no field poll while it plays.
            if (DungeonMates != _matesShown || (DungeonMates != null && System.Array.TrueForAll(_hunters, h => h == null)))
            {
                _matesShown = DungeonMates;
                if (DungeonMates != null)
                {
                    foreach (TownHeroDto mate in DungeonMates) mate.party = true;
                    Receive(DungeonMates);
                }
                else
                {
                    for (int i = 0; i < _hunters.Length; i++) Clear(i);
                    _polledAt = -100f;
                }
            }
            // In town or at the river the lane is not on screen: no need to ask.
            bool away = _root.Town.IsOpen || server.AtRiver;
            if (DungeonMates == null && server.Online && !server.WaitingForHero && !away && !_fetching && Time.time - _polledAt > PollSeconds)
            {
                _fetching = true;
                _polledAt = Time.time;
                StartCoroutine(server.FetchField((field, error) =>
                {
                    _fetching = false;
                    if (field != null && DungeonMates == null) Receive(field.heroes ?? new TownHeroDto[0]);
                }));
            }
            if (!server.Online) { for (int i = 0; i < _hunters.Length; i++) Clear(i); return; }

            if (Input.GetMouseButtonDown(0)) TapAt(Input.mousePosition);
            // Screenshots: -heropick opens the card of the first hunter to arrive.
            if (_pickForShot && System.Array.Find(_hunters, h => h?.Root != null) is Hunter first)
            {
                _pickForShot = false;
                _root.HeroCard.Show(first.Id, first.Player);
            }
            float dt = Time.deltaTime;
            bool running = _lane.RunningNow;
            Quaternion facing = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            for (int i = 0; i < _hunters.Length; i++)
            {
                Hunter h = _hunters[i];
                if (h?.Root == null) continue;
                if (running && !h.OnMap && !h.Mate)
                {
                    Vector3 p = h.Root.localPosition;
                    p.x -= RunSpeed * dt;
                    // Passed: met again further up the road.
                    if (p.x < BehindX) p.x += AheadSpan;
                    h.Root.localPosition = p;
                }
                if (h.Name != null) h.Name.transform.rotation = facing;
                if (h.Mate) Beside(h);
                else Fight(h);
            }
        }

        /// <summary>The other players hunting at the big map's camps now, by id, name and camp (the full map shows them).</summary>
        public IEnumerable<(string id, string name, int camp)> AtCamps
        {
            get
            {
                foreach (Hunter h in _hunters)
                    if (h?.Root != null && h.OnMap && h.Camp >= 0) yield return (h.Id, h.Player, h.Camp);
            }
        }

        /// <summary>A tap on the lane: the nearest hunter under it (feet to name, a finger's width either side) gets the hero
        /// card. Taps on the HUD or a screen are theirs.</summary>
        private void TapAt(Vector3 screen)
        {
            if (EventSystem.current != null && (Input.touchCount > 0 ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                    : EventSystem.current.IsPointerOverGameObject())) return;
            Camera cam = Camera.main;
            if (cam == null || !cam.enabled) return;
            float reach = Mathf.Max(40f, Screen.height * 0.035f);
            Hunter best = null;
            float bestDistance = float.MaxValue;
            foreach (Hunter h in _hunters)
            {
                if (h?.Root == null || !h.Root.gameObject.activeInHierarchy) continue;
                Vector3 feet = cam.WorldToScreenPoint(h.Root.position), head = cam.WorldToScreenPoint(h.Root.position + Vector3.up * 2.6f);
                if (feet.z <= 0f) continue;
                if (screen.y < feet.y - reach * 0.5f || screen.y > head.y + reach * 0.5f) continue;
                float distance = Mathf.Abs(screen.x - (feet.x + head.x) / 2f);
                if (distance > reach || distance >= bestDistance) continue;
                best = h;
                bestDistance = distance;
            }
            if (best != null) _root.HeroCard.Show(best.Id, best.Player);
        }

        /// <summary>A hunter's camp on a big map (a slot each): every other camp round the loop, then the ones between
        /// (1, 3, 5, 2 of six); -1 without camps.</summary>
        private static int CampOf(FieldMap map, int slot)
        {
            var camps = map.Current.Camps;
            if (camps == null || camps.Length == 0) return -1;
            int n = camps.Length;
            return (slot * 2 + 1 + slot / ((n + 1) / 2)) % n;
        }

        /// <summary>Where a hunter stands at his camp, in the map's own metres.</summary>
        private static Vector3 CampSpot(FieldMap map, int camp)
        {
            if (camp < 0) return Vector3.zero;
            FieldMap.Spot c = map.Current.Camps[camp];
            return new Vector3(c.X - 1.5f, 0f, c.Z - 1.5f);
        }

        /// <summary>The big map's waiting monsters: three at each camp, hidden where another player hunts. Made again with
        /// a new map or stage (a rebuilt map took the old ones with it).</summary>
        private void Camps()
        {
            FieldMap map = _lane.Map != null && _lane.Map.Active ? _lane.Map : null;
            FieldMap.Layout layout = map?.Current;
            bool lost = _camps.Length > 0 && _camps[0] == null;
            if (layout != _campsOf || lost)
            {
                foreach (Transform t in _camps) if (t != null) Destroy(t.gameObject);
                _camps = new Transform[0];
                _campsOf = layout;
                if (layout?.Camps != null)
                {
                    _camps = new Transform[layout.Camps.Length];
                    for (int c = 0; c < _camps.Length; c++) _camps[c] = CampGroup(map, c, false);
                }
                _eliteShown = -2;
            }
            // The server's elite camp (its slot, one of six, onto this map's camps): its monsters in gold under a golden
            // banner, and the lane's packs met near it elite too (LaneView.EliteCamp).
            int eliteSlot = _root.Server.EliteCampNow;
            int elite = layout?.Camps != null && layout.Camps.Length > 0 && eliteSlot >= 0 ? eliteSlot % layout.Camps.Length : -1;
            _lane.EliteCamp = elite;
            if (elite != _eliteShown && map != null)
            {
                int was = _eliteShown;
                _eliteShown = elite;
                foreach (int c in new[] { was, elite })
                    if (c >= 0 && c < _camps.Length)
                    {
                        if (_camps[c] != null) Destroy(_camps[c].gameObject);
                        _camps[c] = CampGroup(map, c, c == elite);
                    }
                if (_eliteBanner != null) Destroy(_eliteBanner.gameObject);
                _eliteBanner = null;
                if (elite >= 0)
                {
                    FieldMap.Spot spot = layout.Camps[elite];
                    _eliteBanner = FieldMap.PlaceModel(map.Root, "WarBanner", new Vector3(spot.X + 2.6f, 0f, spot.Z + 1.4f), 25f, 5.5f, "#FFC23A");
                    _root.Hud.Log($"A golden banner flies over {spot.Name}: an elite pack waits there.");
                    if (!_root.Tutorial.Running && !_root.Story.Showing) _root.Tips.Offer(TipCard.Tip.Elite);
                }
            }
            for (int c = 0; c < _camps.Length; c++)
            {
                if (_camps[c] == null) continue;
                // A Commander standing at a camp (MapCommander) has sent its monsters off.
                bool hunted = System.Array.Exists(_hunters, h => h != null && h.OnMap && h.Camp == c)
                              || (_root.Commander != null && _root.Commander.Here is { } boss && boss.Camp == c);
                if (_camps[c].gameObject.activeSelf == hunted) _camps[c].gameObject.SetActive(!hunted);
            }
        }

        private Transform CampGroup(FieldMap map, int camp, bool elite)
        {
            var group = new GameObject("Camp" + camp).transform;
            group.SetParent(map.Root, false);
            FieldMap.Spot c = map.Current.Camps[camp];
            group.localPosition = new Vector3(c.X, 0f, c.Z);
            for (int m = 0; m < 3; m++)
            {
                Transform mob = _lane.CosmeticMob(camp * 7 + m, out _);
                if (mob == null) break;
                mob.SetParent(group, false);
                float a = (m * 120f + camp * 40f) * Mathf.Deg2Rad;
                mob.localPosition = new Vector3(Mathf.Cos(a) * 1.8f, 0f, Mathf.Sin(a) * 1.8f);
                mob.localRotation = Quaternion.Euler(0f, camp * 53f + m * 110f, 0f);
                mob.localScale /= Mathf.Max(0.01f, group.lossyScale.x);
                if (elite) LaneView.DressElite(mob);
            }
            return group;
        }

        /// <summary>Who hunts here now: those already standing keep their places; newcomers take the free ones.</summary>
        private void Receive(TownHeroDto[] heroes)
        {
            var here = new HashSet<string>();
            foreach (TownHeroDto dto in heroes) here.Add(dto.id);
            _matesHere.RemoveWhere(id => !here.Contains(id));
            for (int i = 0; i < _hunters.Length; i++)
                if (_hunters[i] != null && !here.Contains(_hunters[i].Id)) Clear(i);
            foreach (TownHeroDto dto in heroes)
            {
                int at = System.Array.FindIndex(_hunters, h => h != null && h.Id == dto.id);
                if (at < 0) at = System.Array.IndexOf(_hunters, null);
                if (at < 0) break;
                Stand(at, dto);
            }
        }

        private void Stand(int slot, TownHeroDto dto)
        {
            if (!System.Enum.TryParse(dto.@class, out HeroClass cls)) return;
            Figure figure = System.Enum.TryParse(dto.figure, out Figure f) ? f : ItemLooks.NativeFigure(cls);
            int armorBand = ItemLooks.Tier(dto.armorLevel), weaponBand = ItemLooks.Tier(dto.weaponLevel);
            string key = cls + "/" + armorBand + "/" + weaponBand + "/" + dto.skin + "/" + figure + "/" + dto.party;
            Hunter h = _hunters[slot];
            if (h != null && h.Key == key) return;
            FieldMap map = _lane.Map != null && _lane.Map.Active && !dto.party ? _lane.Map : null;
            int camp = map != null ? CampOf(map, slot) : -1;
            bool rebuilt = h?.Root != null && h.Mate == dto.party;
            Vector3 at = dto.party ? MateSpot(slot)
                : rebuilt ? h.Root.localPosition
                : map != null ? CampSpot(map, camp) : new Vector3(Spots[slot].x, 0f, Spots[slot].y);
            Clear(slot);
            h = _hunters[slot] = new Hunter { Id = dto.id, Key = key, Player = dto.name, OnMap = map != null, Camp = camp, Mate = dto.party };
            h.Root = new GameObject((dto.party ? "Partymate" : "Hunter") + slot).transform;
            h.Root.SetParent(map != null ? map.Root : _lane.transform, false);
            h.Root.localPosition = at;
            h.Figure = HeroFigure.Build(h.Root, cls, armorBand, weaponBand, string.IsNullOrEmpty(dto.skin) ? null : dto.skin,
                ItemLooks.SecondLook(cls, figure), name: "HunterFigure");
            h.Figure.Stand(h.Root.position);
            foreach (Renderer r in h.Figure.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            h.Figure.Idle(always: false);
            HeroFigure.Glow(h.Figure.Pieces, _block, UpgradeGlow.ForLevel(dto.armorPlus), UpgradeGlow.ForLevel(dto.weaponPlus));
            h.Name = NameTag(h.Root, dto.name);
            h.NextBlow = Time.time + Random.Range(0.3f, 1.2f);
            if (h.Mate)
            {
                // Facing up the road with the hero, at his fight.
                h.Root.localRotation = Quaternion.Euler(0f, LaneView.HeroLaneYaw, 0f);
                h.Name.color = MateGreen;
                // Said once each time a partymate comes to hunt here (party chat: "starts hunting beside you").
                if (_matesHere.Add(dto.id)) _root.Hud.Log($"{dto.name} hunts beside you.");
                return;
            }
            // Facing the monsters in front of him, a little toward the camera.
            h.Root.localRotation = Quaternion.Euler(0f, slot % 2 == 0 ? 60f : 110f, 0f);
            for (int m = 0; m < 3; m++) AddMob(h, m);
        }

        /// <summary>A partymate's place beside the hero: the slots' mates in the order they stand.</summary>
        private Vector3 MateSpot(int slot)
        {
            int k = 0;
            for (int i = 0; i < slot; i++) if (_hunters[i] != null && _hunters[i].Mate) k++;
            Vector2 spot = MateSpots[Mathf.Min(k, MateSpots.Length - 1)];
            return new Vector3(LaneView.HeroLaneX + spot.x, 0f, spot.y);
        }

        /// <summary>A partymate's step with the hero: running while he runs, a blow every second or so at his fight,
        /// standing otherwise.</summary>
        private void Beside(Hunter h)
        {
            Animation anim = h.Figure?.Anim;
            if (anim == null) return;
            bool fighting = _lane.FightingNow;
            string loop = _lane.RunningNow && anim.GetClip("Run") != null ? "Run" : "Idle";
            if (fighting && Time.time >= h.NextBlow && anim.GetClip("Attack") != null)
            {
                h.NextBlow = Time.time + Random.Range(0.9f, 1.5f);
                anim.CrossFade("Attack", 0.08f);
                anim["Attack"].time = 0f;
                anim.CrossFadeQueued(loop, 0.2f);
                h.Loop = null;
                return;
            }
            if (anim.IsPlaying("Attack") || (h.Loop == loop && anim.IsPlaying(loop))) return;
            h.Loop = loop;
            if (anim.GetClip(loop) != null) anim.CrossFade(loop, 0.2f);
        }

        /// <summary>A monster of the map before a hunter, on a small arc in front of him.</summary>
        private void AddMob(Hunter h, int place)
        {
            Transform mob = _lane.CosmeticMob(h.Picks++ + h.Id.GetHashCode(), out Animation anim);
            if (mob == null) return;
            mob.SetParent(h.Root, false);
            float angle = (-35f + place * 35f) * Mathf.Deg2Rad;
            mob.localPosition = new Vector3(Mathf.Sin(angle) * 1.7f, 0f, Mathf.Cos(angle) * 1.7f);
            mob.localRotation = Quaternion.Euler(0f, 180f + (place - 1) * 20f, 0f);   // facing the hunter
            mob.localScale /= Mathf.Max(0.01f, h.Root.lossyScale.x);
            h.Mobs.Add(new Mob { Root = mob, Anim = anim, Hp = Random.Range(3, 6) });
        }

        /// <summary>A hunter's fight: a blow every second or so at the first monster; three to five fell it, and after a
        /// moment another takes its place.</summary>
        private void Fight(Hunter h)
        {
            float now = Time.time;
            for (int i = h.Mobs.Count - 1; i >= 0; i--)
            {
                Mob m = h.Mobs[i];
                if (m.DeadAt < 0f) continue;
                if (now - m.DeadAt > 1.4f)
                {
                    if (m.Root != null) Destroy(m.Root.gameObject);
                    h.Mobs.RemoveAt(i);
                    h.NextMob = now + 1.5f;
                }
                else if (m.Root != null && m.Anim == null)
                    m.Root.localPosition += Vector3.down * Time.deltaTime * 0.8f;   // a still model sinks away
            }
            if (h.Mobs.Count < 3 && h.NextMob > 0f && now >= h.NextMob)
            {
                h.NextMob = 0f;
                AddMob(h, h.Mobs.Count);
            }
            if (now < h.NextBlow) return;
            h.NextBlow = now + Random.Range(0.9f, 1.6f);
            Mob target = h.Mobs.Find(m => m.DeadAt < 0f);
            if (target == null) return;
            Play(h.Figure.Anim, "Attack");
            target.Hp--;
            if (target.Hp > 0) Play(target.Anim, "Hit");
            else
            {
                target.DeadAt = now;
                if (target.Anim != null && target.Anim.GetClip("Death") != null)
                {
                    target.Anim.Stop();
                    target.Anim.Play("Death");
                }
            }
        }

        private static void Play(Animation anim, string clip)
        {
            if (anim == null || anim.GetClip(clip) == null) return;
            anim.Stop();
            anim.Play(clip);
            if (anim.GetClip("Idle") != null) anim.CrossFadeQueued("Idle", 0.25f);
        }

        /// <summary>A player's name over the hunter's head, as written (never translated).</summary>
        private static TextMesh NameTag(Transform parent, string name)
        {
            var go = new GameObject("Name");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Ui.Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            mesh.text = name;
            mesh.fontSize = 48;
            mesh.characterSize = 0.045f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = new Color(0.95f, 0.9f, 0.75f);
            return mesh;
        }

        /// <summary>Partymates already hunting beside the hero on this map (a new one is told in the lane's log).</summary>
        private readonly HashSet<string> _matesHere = new HashSet<string>();

        private void Clear(int slot)
        {
            Hunter h = _hunters[slot];
            if (h?.Root != null) Destroy(h.Root.gameObject);
            _hunters[slot] = null;
        }
    }
}
