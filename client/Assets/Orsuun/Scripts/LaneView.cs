using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Lane presentation: the hero rig with its armour and weapon looks, 3D enemies from Resources/Models/Mobs (the
    /// Oathfields' Hollowed Wolf, Hollowed Boar and Deserter; Old Greyjaw is a great wolf) and the Korstone model with
    /// lit cracks. Anything without a model falls back to a grey box.
    /// </summary>
    public sealed class LaneView : MonoBehaviour
    {
        private const float HeroX = -1.6f;
        private const float SpawnX = 9f;
        private const float MobSpacing = 0.75f;
        private const int MobsPerRow = 8;
        private const int MaxFloatingTexts = 40;

        private sealed class EnemyView
        {
            public Transform Root;
            public Transform HpFill;
            public float Punch;
            public Vector3 Scale;
            public bool IsModel;
            /// <summary>Resting height of the root: models stand on the ground, boxes are centred.</summary>
            public float Y;
            /// <summary>A 3D wolf, boar or deserter (not the Korstone, not a grey box).</summary>
            public bool IsMob;
            public bool IsKorstone;
            /// <summary>Height above the root where hits land (models stand on the ground, boxes are centred).</summary>
            public float HitHeight;
            /// <summary>Which model it wears (Wolf, Boar, Deserter, Gorak, Queen, Greyjaw) for attack sounds.</summary>
            public string ArtName;
            /// <summary>Where it stands; the lunge is drawn on top of this.</summary>
            public Vector3 Base;
            /// <summary>Seconds into its attack lunge, or below zero when not attacking.</summary>
            public float LungeT = -1f;
            public bool IsBoss;
            public KorstoneFx Fx;
            /// <summary>The mob's own clips (Idle, Run, Attack, Hit, Death; art/blender/mobrig.py), or null for a still model.</summary>
            public Animation Anim;
        }

        /// <summary>A 3D enemy: model and material under Resources (art/blender/looks.py mob_model), and its lane scale.</summary>
        private sealed class MobArt
        {
            public GameObject Model;
            public Material Material;
            public float Height;
        }

        private static readonly Dictionary<string, MobArt> MobArts = new Dictionary<string, MobArt>();
        /// <summary>Mob models in the order of the Oathfields' mob names (Content map 1): enemy id picks one.</summary>
        private static readonly string[] MobModels = { "Wolf", "Boar", "Deserter" };
        /// <summary>The Salt Flats (and Korstone Field III): salt scorpions, glass snakes, caravan ghouls.</summary>
        private static readonly string[] SaltMobs = { "Scorpion", "GlassSnake", "Ghoul" };
        /// <summary>The Frost Pasture (and Korstone Fields IV-V): frost bears, ice wights, snow hags.</summary>
        private static readonly string[] FrostMobs = { "FrostBear", "IceWight", "SnowHag" };
        /// <summary>The Cinder Marches (campaign map 5): ash fiends, magma hounds, flame cultists.</summary>
        private static readonly string[] CinderMobs = { "AshFiend", "MagmaHound", "FlameCultist" };
        /// <summary>Whisperwood (campaign map 6): the hollowed dead, hanging spirits, lantern wisps.</summary>
        private static readonly string[] WhisperMobs = { "HollowedDead", "HangingSpirit", "LanternWisp" };
        /// <summary>The Bloodbirch (campaign map 7): red treants, birch stalkers, sap horrors.</summary>
        private static readonly string[] BloodbirchMobs = { "RedTreant", "BirchStalker", "SapHorror" };
        /// <summary>The Drowned Steppe (campaign map 8): marsh serpents, bog riders, leech swarms.</summary>
        private static readonly string[] DrownedMobs = { "MarshSerpent", "BogRider", "LeechSwarm" };
        /// <summary>Colossus Graves (campaign map 9): stone giants, bone pickers, siege beasts.</summary>
        private static readonly string[] GravesMobs = { "StoneGiant", "BonePicker", "SiegeBeast" };
        /// <summary>The Sunken Bazaar (campaign map 10): Khan cultists, gilded constructs, debt wraiths.</summary>
        private static readonly string[] BazaarMobs = { "KhanCultist", "GildedConstruct", "DebtWraith" };
        /// <summary>The Thousand Markers (campaign map 11): the Hollow Khan's buried army, risen troopers, riders and captains.</summary>
        private static readonly string[] MarkersMobs = { "RisenTrooper", "RisenRider", "RisenCaptain" };
        /// <summary>The Hollow Throne (campaign map 12): the Guard of the Khan, throne guards, the Khan's hounds, oath chanters.</summary>
        private static readonly string[] ThroneMobs = { "ThroneGuard", "KhanHound", "OathChanter" };

        /// <summary>
        /// Gorak Pass (campaign map 2): war hounds and Gorak marauders, the Oathfields' wolf and deserter in the warlord's
        /// colours ("Model#RRGGBB" tints a model).
        /// </summary>
        private static readonly string[] GorakMobs = { "Wolf#7A5A48", "Deserter#E0876E", "Deserter#B9C2D6" };

        /// <summary>The Hollow Spire's floors: the hollowed dead of the grave plain, greyed and washed violet.</summary>
        private static readonly string[] SpireMobs = { "Ghoul#9C94B0", "Deserter#8E8AA0", "Wolf#8A8298" };
        /// <summary>Silkmother's Warren: silk spiders, with salt scorpions and cocooned ghouls bleached pale in the caves.</summary>
        private static readonly string[] WarrenMobs = { "SilkSpider", "Scorpion#D8D0E0", "Ghoul#D9D2C4" };
        /// <summary>The Carvers' Archive: stone sentinels, the vault's cold wights and the thieves who died in it.</summary>
        private static readonly string[] ArchiveMobs = { "StoneSentinel", "IceWight#9FB8D8", "Deserter#7F92B8" };

        /// <summary>The dungeon a floor belongs to (Rules.Dungeons: stage 300 + id * 10 + floor), or 0.</summary>
        private static int DungeonOf(int stageNumber) => Dungeons.IsFloor(stageNumber) ? (stageNumber - Dungeons.FloorStageBase) / 10 : 0;

        /// <summary>The campaign map of a stage (1 the Oathfields .. 12 the Hollow Throne), or 0 for a zone or a dungeon floor.</summary>
        private static int CampaignMap(int stageNumber) => Content.IsZone(stageNumber) ? 0 : Content.MapOfStage(stageNumber).Id;

        /// <summary>
        /// Which mob set a stage or zone fields (zone ids from Content: Salt Flats, Frost Pasture, Korstone Fields
        /// III-V; campaign maps 2-6 are Gorak Pass, the Salt Sea, Whitefang Range, the Cinder Marches and Whisperwood).
        /// </summary>
        private static string[] MobSetFor(int stageNumber)
        {
            int map = CampaignMap(stageNumber);
            int dungeon = DungeonOf(stageNumber);
            string[] set = dungeon == 2 ? WarrenMobs : dungeon == 3 ? ArchiveMobs : dungeon != 0 ? SpireMobs
                : map == 2 ? GorakMobs
                : map == 5 ? CinderMobs
                : map == 6 ? WhisperMobs
                : map == 7 ? BloodbirchMobs
                : map == 8 ? DrownedMobs
                : map == 9 ? GravesMobs
                : map == 10 ? BazaarMobs
                : map == 11 ? MarkersMobs
                : map == 12 ? ThroneMobs
                : stageNumber == Content.SaltFlats || stageNumber == Content.KorstoneFieldIII || map == 3 ? SaltMobs
                : stageNumber == Content.FrostPasture || stageNumber == Content.KorstoneFieldIV || stageNumber == Content.KorstoneFieldV || map == 4 ? FrostMobs : MobModels;
            // Older builds without the new models keep the Oathfields set.
            return LoadMob(ModelOf(set[0])) != null ? set : MobModels;
        }

        /// <summary>The model name in a mob set entry ("Wolf#7A5A48" is the wolf).</summary>
        private static string ModelOf(string entry)
        {
            int hash = entry.IndexOf('#');
            return hash < 0 ? entry : entry.Substring(0, hash);
        }

        /// <summary>
        /// Backdrop for a stage: the Hunting Grounds past the Ember Steppe have their own environment keys, and so do the
        /// campaign maps past the Oathfields (Gorak Pass under the war camp, the Salt Sea and Whitefang Range under the
        /// Salt Flats and the Frost Pasture; the Cinder Marches and Whisperwood under their own).
        /// </summary>
        private static string BackdropKey(ZoneType zone, int stageNumber)
        {
            if (DungeonOf(stageNumber) == 2 && Art.Load<Material>("Backdrops/BackdropSilkWarren") != null) return "SilkWarren";
            if (DungeonOf(stageNumber) == 3 && Art.Load<Material>("Backdrops/BackdropCarversArchive") != null) return "CarversArchive";
            if (Dungeons.IsFloor(stageNumber) && Art.Load<Material>("Backdrops/BackdropHollowSpire") != null) return "HollowSpire";
            int map = CampaignMap(stageNumber);
            if ((stageNumber == Content.SaltFlats || map == 3) && Art.Load<Material>("Backdrops/BackdropSaltFlats") != null) return "SaltFlats";
            if ((stageNumber == Content.FrostPasture || map == 4) && Art.Load<Material>("Backdrops/BackdropFrostPasture") != null) return "FrostPasture";
            if (map == 5 && Art.Load<Material>("Backdrops/BackdropCinderMarches") != null) return "CinderMarches";
            if (map == 6 && Art.Load<Material>("Backdrops/BackdropWhisperwood") != null) return "Whisperwood";
            if (map == 7 && Art.Load<Material>("Backdrops/BackdropBloodbirch") != null) return "Bloodbirch";
            if (map == 8 && Art.Load<Material>("Backdrops/BackdropDrownedSteppe") != null) return "DrownedSteppe";
            if (map == 9 && Art.Load<Material>("Backdrops/BackdropColossusGraves") != null) return "ColossusGraves";
            if (map == 10 && Art.Load<Material>("Backdrops/BackdropSunkenBazaar") != null) return "SunkenBazaar";
            if (map == 11 && Art.Load<Material>("Backdrops/BackdropThousandMarkers") != null) return "ThousandMarkers";
            if (map == 12 && Art.Load<Material>("Backdrops/BackdropHollowThrone") != null) return "HollowThrone";
            if (map == 2) return ZoneType.CommanderGround.ToString();
            return (zone == ZoneType.Campaign ? ZoneType.HuntingGround : zone).ToString();
        }
        /// <summary>Models face +Z; this turns them toward the hero, three-quarter to the camera (the hero uses 125).</summary>
        private const float EnemyYaw = -125f;
        private const float MobScale = 0.85f;

        private sealed class FloatingText
        {
            public TextMesh Mesh;
            public float Age;
        }

        private readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        private readonly List<FloatingText> _texts = new List<FloatingText>();
        /// <summary>Slain enemies tipping over and sinking before they are removed.</summary>
        private readonly List<(Transform root, float age, float side)> _falling = new List<(Transform, float, float)>();
        private const float FallSeconds = 0.5f;
        private readonly List<Transform> _stripes = new List<Transform>();

        private LaneSim _sim;
        private Transform _hero;
        /// <summary>Hero root height: the capsule is centred at 1, the Vanguard model stands on 0.</summary>
        private float _heroY = 1f;
        private Color _heroTint = HeroColor;
        private float _heroPunch;
        /// <summary>The rigged armour look's legacy clips (Idle, Run, Attack, Hit, Death); null on the grey-box hero.</summary>
        private Animation _anim;
        private bool _heroDown;
        private float _heroHurt;

        private Renderer _ground;
        /// <summary>
        /// The painted floor under the lane (owner, 25 Sep 2026: "no floor on the maps right?"): Resources/Floors/&lt;backdrop
        /// key&gt;, a tileable top-down texture that repeats every FloorTile metres and scrolls with the run. Without one the
        /// plain ground and its stripes show.
        /// </summary>
        private const float FloorTile = 6f;
        /// <summary>
        /// A tile's depth: twice its length, which offsets the camera's low angle and leaves one road (a floor's road runs
        /// across the middle of its tile) under the hero; FloorRoadOffset puts that middle at z = 0 on the ground's top face.
        /// </summary>
        private const float FloorTileDepth = 12f;
        /// <summary>From the ground mesh's top face: which way its u runs along x (+1 or -1), and the v offset that puts a
        /// tile's middle (where a floor's road runs) at z = 0, under the hero.</summary>
        private float _floorScrollSign = 1f;
        private float _floorRoadOffset;
        private Material _groundMaterial;
        private bool _hasFloor;
        private float _floorScroll;
        /// <summary>Editor previews only: a suffix picking a candidate floor ("_A", "_B").</summary>
        public static string FloorVariant = "";
        private Renderer _backdrop;
        private ZoneType? _zone;

        private static Material _greyBox;
        private static Material _korstoneMaterial;
        private static GameObject _korstoneModel;
        private static bool _korstoneLoaded;

        private static readonly Color HeroColor = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color MobColor = new Color(0.75f, 0.25f, 0.22f);
        private static readonly Color BossColor = new Color(0.55f, 0.12f, 0.35f);
        private static readonly Color KorstoneColor = new Color(0.12f, 0.10f, 0.12f);

        public LaneSim Sim => _sim;

        /// <summary>Switches to another lane (park or push replay), clearing every enemy view.</summary>
        public void Bind(LaneSim sim)
        {
            foreach (EnemyView view in _views.Values) Kill(view.Root.gameObject);   // the editor preview rebinds too
            _views.Clear();
            _sim = sim;
            SetZone(sim.Stage.Zone, sim.Stage.StageNumber);
            _hero.rotation = Quaternion.identity;
            _hero.position = new Vector3(HeroX, _heroY, 0f);
            if (_heroDown) { _heroDown = false; Play("Idle", 0f); }
            foreach (Enemy enemy in sim.Enemies) SpawnView(enemy.Id);
        }

        public void Init(LaneSim sim)
        {
            _sim = sim;
            BuildScenery();
            BuildHero();
            SetZone(sim.Stage.Zone, sim.Stage.StageNumber);
            _skillFx = new GameObject("SkillFx").AddComponent<SkillFx>();
            _skillFx.transform.SetParent(transform, false);
            _skillFx.Init(this);
        }

        /// <summary>Ground, scrolling stripes and the zone backdrop. Public so the editor preview can frame it.</summary>
        public void BuildScenery()
        {
            Transform ground = Primitive(PrimitiveType.Cube, "Ground", new Color(0.30f, 0.34f, 0.26f));
            ground.SetParent(transform, false);
            // Runs from z=-12 (below the bottom of the view) to z=5, so the lane band has no empty strip under it.
            ground.position = new Vector3(3f, -0.25f, -3.5f);
            ground.localScale = new Vector3(40f, 0.5f, 17f);
            _ground = ground.GetComponent<Renderer>();
            _groundMaterial = _ground.material;
            MeasureFloorMapping(ground);

            for (int i = 0; i < 10; i++)
            {
                Transform stripe = Primitive(PrimitiveType.Cube, "Stripe", new Color(0.36f, 0.40f, 0.30f));
                stripe.SetParent(transform, false);
                stripe.localScale = new Vector3(0.25f, 0.02f, 17f);
                stripe.position = new Vector3(-8f + i * 2.4f, 0.01f, -3.5f);
                _stripes.Add(stripe);
            }

            // Environment key far behind the lane. The ground's far edge meets the view centre, so only the painting's
            // upper three quarters (grass band, hills, sky) show above it.
            Transform backdrop = Primitive(PrimitiveType.Quad, "Backdrop", Color.white);
            backdrop.SetParent(transform, false);
            backdrop.position = new Vector3(1.5f, -1.2f, 40f);   // its top at the view's top (the camera sits 0.8 m higher since 26 Sep 2026)
            backdrop.localScale = new Vector3(32.7f, 18.4f, 1f);
            _backdrop = backdrop.GetComponent<Renderer>();
            _backdrop.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _backdrop.receiveShadows = false;
        }

        private string _backdropKey;

        /// <summary>
        /// Backdrop and ground tint for a stage: by zone type, with the Salt Flats and the Frost Pasture on their own
        /// environment keys. Campaign stages use the Hunting Grounds key for now.
        /// </summary>
        public void SetZone(ZoneType zone, int stageNumber = 0)
        {
            string key = BackdropKey(zone, stageNumber);
            if (_backdropKey == key) return;
            _backdropKey = key;
            _zone = zone;
            var mat = Art.Load<Material>("Backdrops/Backdrop" + key);
            if (mat != null) _backdrop.sharedMaterial = mat;
            _backdrop.enabled = mat != null;

            (Color ground, Color stripe) = key switch
            {
                "KorstoneField" => (new Color(0.50f, 0.38f, 0.20f), new Color(0.56f, 0.43f, 0.24f)),
                "CommanderGround" => (new Color(0.30f, 0.21f, 0.15f), new Color(0.35f, 0.25f, 0.18f)),
                "SaltFlats" => (new Color(0.66f, 0.61f, 0.53f), new Color(0.72f, 0.67f, 0.58f)),
                "FrostPasture" => (new Color(0.60f, 0.67f, 0.76f), new Color(0.68f, 0.75f, 0.83f)),
                "HollowSpire" => (new Color(0.24f, 0.23f, 0.26f), new Color(0.29f, 0.27f, 0.31f)),
                "CinderMarches" => (new Color(0.22f, 0.19f, 0.18f), new Color(0.30f, 0.21f, 0.16f)),
                "SilkWarren" => (new Color(0.34f, 0.31f, 0.38f), new Color(0.40f, 0.36f, 0.45f)),
                "Bloodbirch" => (new Color(0.38f, 0.18f, 0.14f), new Color(0.46f, 0.22f, 0.16f)),
                "DrownedSteppe" => (new Color(0.24f, 0.28f, 0.22f), new Color(0.29f, 0.33f, 0.26f)),
                "ColossusGraves" => (new Color(0.36f, 0.35f, 0.33f), new Color(0.42f, 0.41f, 0.38f)),
                "SunkenBazaar" => (new Color(0.55f, 0.45f, 0.30f), new Color(0.62f, 0.51f, 0.34f)),
                "ThousandMarkers" => (new Color(0.33f, 0.30f, 0.29f), new Color(0.39f, 0.35f, 0.33f)),
                "HollowThrone" => (new Color(0.16f, 0.14f, 0.13f), new Color(0.22f, 0.19f, 0.15f)),
                "CarversArchive" => (new Color(0.27f, 0.30f, 0.36f), new Color(0.32f, 0.36f, 0.43f)),
                "Whisperwood" => (new Color(0.20f, 0.25f, 0.22f), new Color(0.24f, 0.30f, 0.27f)),
                _ => (new Color(0.52f, 0.48f, 0.22f), new Color(0.58f, 0.54f, 0.27f)),
            };
            _ground.material.color = ground;
            foreach (Transform s in _stripes) s.GetComponent<Renderer>().material.color = stripe;
            RefreshFloor();
        }

        /// <summary>
        /// Reads the cube's top face (its UVs run 0..1 across it, in whichever direction the mesh has them): the sign that
        /// makes the floor run past like the stripes, and the v offset that centres a tile's middle on z = 0.
        /// </summary>
        private void MeasureFloorMapping(Transform ground)
        {
            Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vs = mesh.vertices;
            Vector3[] ns = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int a = -1, bx = -1, bz = -1;
            for (int i = 0; i < vs.Length; i++)
            {
                if (ns[i].y < 0.9f) continue;
                if (a < 0) { a = i; continue; }
                if (bx < 0 && Mathf.Abs(vs[i].x - vs[a].x) > 0.5f && Mathf.Abs(vs[i].z - vs[a].z) < 0.01f) bx = i;
                if (bz < 0 && Mathf.Abs(vs[i].z - vs[a].z) > 0.5f && Mathf.Abs(vs[i].x - vs[a].x) < 0.01f) bz = i;
            }
            if (a < 0 || bx < 0 || bz < 0) return;
            float duDx = (uvs[bx].x - uvs[a].x) / (vs[bx].x - vs[a].x);
            // The top face may map x to v and z to u; the floors assume u along the lane.
            if (Mathf.Abs(duDx) < 0.5f) duDx = (uvs[bx].y - uvs[a].y) / (vs[bx].x - vs[a].x);
            _floorScrollSign = duDx >= 0f ? 1f : -1f;
            float dvDz = (uvs[bz].y - uvs[a].y) / (vs[bz].z - vs[a].z);
            float zLocal = (0f - ground.position.z) / ground.localScale.z;           // world z = 0 on the unit cube
            float v = uvs[a].y + dvDz * (zLocal - vs[a].z);
            float tiles = ground.localScale.z / FloorTileDepth;
            _floorRoadOffset = Mathf.Repeat(0.5f - v * tiles, 1f);
        }

        /// <summary>Puts the current backdrop's floor on the ground (or the plain colour and stripes when it has none).</summary>
        public void RefreshFloor()
        {
            if (_groundMaterial == null) return;
            var floor = _backdropKey == null ? null : Art.Load<Texture2D>("Floors/" + _backdropKey + FloorVariant);
            _hasFloor = floor != null;
            _groundMaterial.SetTexture("_BaseMap", floor);
            if (_hasFloor)
            {
                Vector3 size = _ground.transform.localScale;
                _groundMaterial.SetTextureScale("_BaseMap", new Vector2(size.x / FloorTile, size.z / FloorTileDepth));
                _groundMaterial.SetTextureOffset("_BaseMap", new Vector2(_floorScroll, _floorRoadOffset));
                _groundMaterial.color = new Color(0.92f, 0.92f, 0.92f);   // the light does the rest
            }
            foreach (Transform s in _stripes) s.gameObject.SetActive(!_hasFloor);
        }

        /// <summary>A piece of the hero and what drives its glow: an EquipSlot index (weapon or armour) or NoSlot.</summary>
        private readonly List<(Renderer renderer, int slot)> _heroParts = new List<(Renderer, int)>();
        private readonly float[] _partGlow = new float[16];
        private const int NoSlot = -1;

        private Transform _rig;
        private GameObject _armorLook;
        private GameObject _weaponLook;
        private WeaponKind _weaponKind;
        // The skills' casts (owner, 26 Sep 2026): effects, the clip playing, and what the Peerless spirit is built from.
        private SkillFx _skillFx;
        private float _castUntil;
        private GameObject _lookPrefab, _weaponPrefab;
        private Material _lookMaterial, _weaponMaterial;
        private float _blinkUntil, _blinkX;
        private bool _blinking;
        private static readonly Color EmpowerTint = new Color(1.7f, 1.3f, 0.6f);
        private string _armorLookId;
        private string _weaponLookId;

        /// <summary>Look shown when no body armour is equipped: the plain quilted coat of the first band.</summary>
        public const string BareArmorLook = "Armor_T0";

        public void BuildHero()
        {
            // Looks (owner, 23 Sep 2026): the weapon and the body armour are the visible items, and their models change
            // with the item's level band (ItemLooks). The hero is a rig holding one armour look and one weapon look;
            // the weapon is placed between the armour's WeaponBase and WeaponTip, so any glaive fits any armour.
            _hero = new GameObject("Hero").transform;
            _hero.SetParent(transform, false);
            _rig = new GameObject("Rig").transform;
            _rig.SetParent(_hero, false);
            _rig.localRotation = Quaternion.Euler(0f, 125f, 0f); // models face +Z: this faces the enemies, three-quarter to the camera
            _rig.localScale = VanguardBuild;
            _heroY = 0f;
            _heroTint = Color.white;
            _hero.position = new Vector3(HeroX, _heroY, 0f);
            if (SetLooks(BareArmorLook, "Weapon_T1")) return;

            // No looks in the build: the grey-box capsule and glaive.
            Kill(_rig.gameObject);
            _rig = null;
            _heroY = 1f;
            _heroTint = HeroColor;
            Transform capsule = Primitive(PrimitiveType.Capsule, "Body", HeroColor);
            capsule.SetParent(_hero, false);
            _hero.position = new Vector3(HeroX, 1f, 0f);
            Renderer body = capsule.GetComponent<Renderer>();
            UseMaterial(body, "EmberGear", HeroColor);
            _heroParts.Add((body, (int)EquipSlot.Armor));

            Transform glaive = Primitive(PrimitiveType.Cube, "Glaive", new Color(0.62f, 0.62f, 0.66f));
            glaive.SetParent(_hero, false);
            glaive.localPosition = new Vector3(0.62f, 0.35f, -0.25f);
            glaive.localScale = new Vector3(0.07f, 2.1f, 0.07f);
            glaive.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Renderer blade = glaive.GetComponent<Renderer>();
            UseMaterial(blade, "EmberWeapon", new Color(0.62f, 0.62f, 0.66f));
            _heroParts.Add((blade, (int)EquipSlot.Weapon));
        }

        /// <summary>
        /// Shows the armour and weapon looks (ItemState.LookId; null armour = the bare coat). Missing art falls back to the
        /// nearest band below, then above. Returns false when no look models exist at all.
        /// </summary>
        public bool SetLooks(string armorLookId, string weaponLookId)
        {
            if (_rig == null) return false;
            if (_class != HeroClass.Vanguard) return true;   // other classes wear their own model (SetHeroClass)
            armorLookId ??= BareArmorLook;
            if (armorLookId == _armorLookId && weaponLookId == _weaponLookId && _second == _armorSecond) return true;
            if (_heroDown) return true; // lying down: swap once back on his feet

            // The Vanguard's second look (owner, 26 Sep 2026) wears the same armour cut for her: "ArmorAlt_T3" for "Armor_T3".
            string armorUsed = null;
            GameObject armorPrefab = _second && armorLookId.StartsWith("Armor_T", System.StringComparison.Ordinal)
                ? LoadLook(AltName(armorLookId), out armorUsed) : null;
            if (armorPrefab == null) armorPrefab = LoadLook(armorLookId, out armorUsed);
            if (armorPrefab == null) return false;
            string weaponUsed = null;
            GameObject weaponPrefab = weaponLookId != null ? LoadLook(weaponLookId, out weaponUsed) : null;

            if (_armorLook != null) Kill(_armorLook);
            if (_weaponLook != null) Kill(_weaponLook);
            _heroParts.Clear();
            for (int i = 0; i < _partGlow.Length; i++) _partGlow[i] = -1f;

            _armorLook = Instantiate(armorPrefab, _rig);
            // Measured below at the bind pose: the clips only start sampling on the next frame.
            _anim = _armorLook.GetComponent<Animation>();
            if (_anim != null && _anim.GetClip("Idle") == null) _anim = null;
            CastClips.Ensure(_anim, HeroClass.Vanguard);
            Material armorMat = Art.Load<Material>("Looks/" + armorUsed);
            _lookPrefab = armorPrefab;
            _lookMaterial = armorMat;
            _weaponPrefab = weaponPrefab;
            _weaponMaterial = weaponUsed != null ? Art.Load<Material>("Looks/" + weaponUsed) : null;
            _weaponKind = KindOfLook(weaponUsed);
            Bounds b = default;
            bool first = true;
            foreach (Renderer r in _armorLook.GetComponentsInChildren<Renderer>())
            {
                if (armorMat != null) r.sharedMaterial = armorMat;
                _heroParts.Add((r, SlotOf(r.name)));
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            // Feet on the ground, centred on the hero.
            Vector3 origin = _rig.position;
            _armorLook.transform.position -= new Vector3(b.center.x - origin.x, b.min.y - origin.y, b.center.z - origin.z);

            if (weaponPrefab != null)
            {
                _weaponLook = Instantiate(weaponPrefab, _rig);
                foreach (Renderer r in _weaponLook.GetComponentsInChildren<Renderer>())
                {
                    if (_weaponMaterial != null) r.sharedMaterial = _weaponMaterial;
                    _heroParts.Add((r, (int)EquipSlot.Weapon));
                }
                LayWeapon(_weaponLook, _armorLook.transform, _rig, _anim != null, _weaponKind);
            }
            Play("Idle", 0f);

            _armorLookId = armorLookId;
            _weaponLookId = weaponLookId;
            _armorSecond = _second;
            return true;
        }

        /// <summary>The hero wears the class's second look (the other figure: ItemLooks.SecondLook).</summary>
        private bool _second;
        private bool _armorSecond;

        /// <summary>
        /// A look's second-look model: "Armor_T3" -> "ArmorAlt_T3", "Kestrel_T3" -> "KestrelAlt_T3"; a costume
        /// ("Skin_TulGorak", "Kestrel_SkinAmberRoad") takes "Alt" at the end.
        /// </summary>
        internal static string AltName(string look)
        {
            int split = look.LastIndexOf("_T", System.StringComparison.Ordinal);
            bool band = split >= 0 && split + 2 < look.Length && int.TryParse(look.Substring(split + 2), out _);
            return band ? look.Substring(0, split) + "Alt" + look.Substring(split) : look + "Alt";
        }

        /// <summary>
        /// Lays a glaive look from an armour's grip to the tip of its pole: measured standing upright, then stretched
        /// along the pole only (Rodin makes thin objects chunky, so girth stays as modelled). On rigged looks the grip
        /// rides the hand bone, so the glaive swings with the arm.
        /// </summary>
        /// <summary>
        /// Puts a weapon look in the armour's hand. The armour marks its pole (WeaponBase at the foot, WeaponTip past the head)
        /// and, since the Vanguard's redesign, the fist on it (WeaponGrip). A glaive is stretched from foot to tip; a sword
        /// rises from the fist at its own length, a share of the pole's (owner, 26 Sep 2026: weapons mixed by level).
        /// </summary>
        internal static void LayWeapon(GameObject weapon, Transform body, Transform rig, bool rigged, WeaponKind kind)
        {
            Transform grip = FindDeep(body, "WeaponBase");
            Transform tip = FindDeep(body, "WeaponTip");
            Transform fist = FindDeep(body, "WeaponGrip");
            Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>();
            if (grip == null || tip == null || renderers.Length == 0) return;
            Transform w = weapon.transform;
            w.rotation = Quaternion.identity;
            w.localScale = Vector3.one;
            w.position = Vector3.zero;
            float length = renderers[0].bounds.size.y;
            Vector3 axis = tip.position - grip.position;
            w.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized) * rig.rotation;
            if (kind == WeaponKind.Glaive || length <= 0.01f)
            {
                w.localScale = new Vector3(1f, length > 0.01f ? axis.magnitude / length : 1f, 1f);
                w.position = grip.position;
            }
            else
            {
                (float share, float held) = SwordSpan(kind);
                float reach = axis.magnitude * share;
                Vector3 hand = fist != null ? fist.position : grip.position + axis * 0.56f;
                w.localScale = Vector3.one * (reach / length);
                w.position = hand - axis.normalized * reach * held;
            }
            if (rigged) w.SetParent(grip, true);
        }

        /// <summary>
        /// A sword's length as a share of the armour's pole, and where the fist holds it (a share of its length from the
        /// pommel): a one-handed sword about 1.15 m against the 2.5 m pole, a greatsword 1.7 m.
        /// </summary>
        private static (float Share, float Held) SwordSpan(WeaponKind kind) => kind == WeaponKind.Sword ? (0.46f, 0.1f) : (0.68f, 0.14f);

        /// <summary>The kind of a weapon look ("Weapon_T3" is band 3's), the glaive for anything else.</summary>
        internal static WeaponKind KindOfLook(string lookId)
        {
            int split = lookId == null ? -1 : lookId.LastIndexOf("_T", System.StringComparison.Ordinal);
            return split >= 0 && int.TryParse(lookId.Substring(split + 2), out int tier) && tier >= 0 && tier < ItemLooks.WeaponKinds.Length
                ? ItemLooks.WeaponKinds[tier] : WeaponKind.Glaive;
        }

        internal static GameObject LoadLook(string lookId, out string used)
        {
            used = lookId;
            var model = Art.Load<GameObject>("Models/Looks/" + lookId);
            if (model != null) return model;
            int split = lookId.LastIndexOf("_T", System.StringComparison.Ordinal);
            if (split < 0 || !int.TryParse(lookId.Substring(split + 2), out int tier)) return null;
            string kind = lookId.Substring(0, split);
            for (int step = 1; step <= ItemLooks.MaxTier; step++)
                foreach (int t in new[] { tier - step, tier + step })
                {
                    if (t < 0 || t > ItemLooks.MaxTier) continue;
                    model = Art.Load<GameObject>("Models/Looks/" + kind + "_T" + t);
                    if (model != null) { used = kind + "_T" + t; return model; }
                }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name || parent.name.StartsWith(name + ".")) return parent;   // Blender suffixes duplicates
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>Vanguard_Weapon -> EquipSlot.Weapon, Vanguard_Armor -> EquipSlot.Armor; anything else has no slot.</summary>
        internal static int SlotOf(string partName)
        {
            int dot = partName.IndexOf('.');
            if (dot >= 0) partName = partName.Substring(0, dot);   // Blender's "Vanguard_Armor.001"
            string suffix = partName.Substring(partName.LastIndexOf('_') + 1);
            return System.Enum.TryParse(suffix, out EquipSlot slot) ? (int)slot : NoSlot;
        }

        /// <summary>Upgrade glow per equipment slot (UpgradeGlow.PerSlot); each hero piece shows its own item's level.</summary>
        public void SetGear(float[] glowBySlot)
        {
            for (int p = 0; p < _heroParts.Count; p++)
            {
                (Renderer r, int slot) = _heroParts[p];
                float glow = slot >= 0 && UpgradeGlow.IsVisible((EquipSlot)slot) ? glowBySlot[slot] : 0f;
                if (Mathf.Approximately(glow, _partGlow[p]) && _partGlow[p] >= 0f) continue;
                _partGlow[p] = glow;
                r.material.SetFloat(UpgradeGlow.GlowId, glow);
                // Star sparkles off the piece from +7 (made the first time a piece glows).
                if (glow > 0f || r.GetComponentInChildren<GearSparkle>() != null) GearSparkle.On(r, slot == (int)EquipSlot.Weapon).Set(glow);
            }
        }

        public void Handle(LaneEvent e)
        {
            switch (e.Kind)
            {
                case LaneEventKind.EnemySpawned:
                    SpawnView(e.EnemyId);
                    break;

                case LaneEventKind.EnemyDamaged:
                    _heroPunch = 1f;
                    Strike();
                    if (_views.TryGetValue(e.EnemyId, out EnemyView hit))
                    {
                        hit.Punch = 1f;
                        Float(e.Amount.ToString(), hit.Root.position + Vector3.up * 1.2f, e.Crit ? Palette.Warn : Color.white, e.Crit ? 1.5f : 1f);
                        Vector3 at = hit.Root.position + Vector3.up * hit.HitHeight + new Vector3(-0.2f, 0f, -0.3f);
                        Color korstoneGlow = hit.IsKorstone && hit.Fx != null ? KorstoneLook.Tiers[_korstoneTier].Glow : new Color(1f, 0.45f, 0.12f);
                        if (e.Crit) Sparks(at, 18, new Color(1f, 0.95f, 0.7f), 1.4f);
                        else Sparks(at, hit.IsKorstone ? 10 : 7, hit.IsKorstone ? korstoneGlow : new Color(1f, 0.7f, 0.3f), 1f);
                        hit.Fx?.Flare();
                        if (hit.Anim != null && !hit.Anim.IsPlaying("Attack")) hit.Anim.CrossFade("Hit", 0.05f);
                        SpellFx(at);
                    }
                    GameAudio.Instance?.Play(e.Crit ? "LaneHitCrit" : "LaneHitSlash", e.Crit ? 0.9f : 0.55f, e.Crit ? 0.08f : 0.07f);
                    break;

                case LaneEventKind.EnemyDied:
                    if (_views.TryGetValue(e.EnemyId, out EnemyView dead))
                    {
                        _views.Remove(e.EnemyId);
                        Vector3 at = dead.Root.position + Vector3.up * dead.HitHeight;
                        if (dead.IsKorstone)
                        {
                            Sparks(at + Vector3.up * 0.4f, 90, new Color(1f, 0.5f, 0.12f), 2.2f);
                            GameAudio.Instance?.Play("LaneKorstoneBreak", 1f, 0.3f);
                        }
                        else
                        {
                            Sparks(at, 14, new Color(0.75f, 0.15f, 0.1f), 1.1f);
                            GameAudio.Instance?.Play("LaneMobDeath", 0.45f, 0.12f);
                        }
                        if (dead.IsMob && _falling.Count < 24)
                        {
                            // Models fall (their own Death clip, or a keel-over) and sink into the grass; the HP bar goes at once.
                            Transform bar = dead.Root.Find("HpBar");
                            if (bar != null) Destroy(bar.gameObject);
                            if (dead.Anim != null) dead.Anim.CrossFade("Death", 0.05f);
                            _falling.Add((dead.Root, 0f, dead.Anim != null ? 0f : (e.EnemyId & 1) == 0 ? 1f : -1f));
                        }
                        else Destroy(dead.Root.gameObject);
                    }
                    break;

                case LaneEventKind.KorstoneWave:
                    Float("WAVE " + e.Amount, new Vector3(3.4f, 3.4f, 0f), Palette.Bad, 1.8f);
                    foreach (EnemyView v in _views.Values) v.Fx?.Wave();
                    GameAudio.Instance?.Play("LaneKorstoneWave", 0.8f, 0.5f);
                    break;

                case LaneEventKind.Shielded:
                    if (_views.TryGetValue(e.EnemyId, out EnemyView shielded))
                        Float("SHIELDED", shielded.Root.position + Vector3.up * 1.2f, Palette.Muted, 1f);
                    break;

                case LaneEventKind.BossMechanic:
                    Float(e.Text, new Vector3(1.5f, 3.6f, 0f), Palette.Warn, 1.6f);
                    break;

                case LaneEventKind.HeroDamaged:
                    _heroHurt = 1f;
                    Flinch();
                    if (e.Text == "veiled") _skillFx?.Veiled();
                    if (_sim.WardActive && _views.TryGetValue(e.EnemyId, out EnemyView warded) && !warded.IsKorstone)
                        _skillFx?.Warded(warded.Root.position + Vector3.up * warded.HitHeight);
                    GameAudio.Instance?.Play("LaneHeroHurt", 0.4f, 0.3f);
                    // The attacker lunges; the blow lands on the hero with a spray of red, or whiffs on an evade.
                    if (_views.TryGetValue(e.EnemyId, out EnemyView attacker) && !attacker.IsKorstone)
                    {
                        attacker.LungeT = 0f;
                        if (attacker.Anim != null)
                        {
                            attacker.Anim.CrossFade("Attack", 0.05f);
                            attacker.Anim["Attack"].time = 0f;
                        }
                        bool evaded = e.Text == "evaded";
                        if (!evaded)
                        {
                            Vector3 chest = _hero.position + new Vector3(0.25f, 1.25f, -0.25f);
                            Sparks(chest, attacker.IsBoss ? 16 : 8, new Color(0.9f, 0.08f, 0.06f), attacker.IsBoss ? 1.4f : 0.9f);
                            if (attacker.IsBoss) Sparks(chest, 8, new Color(1f, 0.85f, 0.6f), 1.6f);
                        }
                        GameAudio.Instance?.Play(AttackSound(attacker), attacker.IsBoss ? 0.9f : 0.45f, attacker.IsBoss ? 0.25f : 0.1f);
                    }
                    break;

                case LaneEventKind.HeroHealed:
                    Float("+" + e.Amount, _hero.position + Vector3.up * 1.4f, Palette.Good, 1.2f);
                    GameAudio.Instance?.Play("LanePotion", 0.7f, 0.3f);
                    break;

                case LaneEventKind.Loot:
                    GameAudio.Instance?.Play("LaneLoot", 0.6f, 0.4f);
                    break;

                case LaneEventKind.HeroDied:
                    foreach (EnemyView view in _views.Values) Destroy(view.Root.gameObject);
                    _views.Clear();
                    _heroDown = true;
                    if (_anim != null) Play("Death", 0.1f);
                    else
                    {
                        _hero.rotation = Quaternion.Euler(0f, 0f, 90f);
                        _hero.position = new Vector3(HeroX, _heroY < 0.5f ? 0.3f : 0.5f, 0f);
                    }
                    Float("DEFEATED", _hero.position + Vector3.up * 2f, Palette.Bad, 2f);
                    break;

                case LaneEventKind.HeroRespawned:
                    _heroDown = false;
                    Play("Idle", 0f);
                    _hero.rotation = Quaternion.identity;
                    _hero.position = new Vector3(HeroX, _heroY, 0f);
                    break;

                case LaneEventKind.SkillCast:
                    Float(e.Text, _hero.position + Vector3.up * 1.9f, new Color(0.6f, 0.85f, 1f), 1.3f);
                    if (e.Amount >= 0 && e.Amount < _sim.Skills.Length)
                    {
                        SkillKind kind = _sim.Skills[(int)e.Amount].Kind;
                        GameAudio.Instance?.Play(kind == SkillKind.Burst || kind == SkillKind.Execute ? "LaneSkillBurst"
                            : kind == SkillKind.Area || kind == SkillKind.Charge || kind == SkillKind.Poison ? "LaneSkillArea" : "LaneSkillHaste", 0.85f, 0.2f);
                        int slot = (int)e.Amount;
                        int[] bonus = _sim.Hero.SkillGradeBonusPercent;
                        _skillFx?.Cast(slot, _sim.Skills[slot], _sim.Hero.Class, slot < bonus.Length ? bonus[slot] : 0);
                        PlayCast(slot);
                    }
                    break;
            }
        }

        private void Update()
        {
            if (_sim == null) return;
            float dt = Time.deltaTime;

            if (_sim.Phase == LanePhase.Running)
            {
                if (_hasFloor)
                {
                    // The floor runs past at the stripes' speed.
                    _floorScroll = Mathf.Repeat(_floorScroll + _floorScrollSign * 6f * dt / FloorTile, 1f);
                    _groundMaterial.SetTextureOffset("_BaseMap", new Vector2(_floorScroll, _floorRoadOffset));
                }
                foreach (Transform stripe in _stripes)
                {
                    Vector3 p = stripe.position;
                    p.x -= 6f * dt;
                    if (p.x < -9f) p.x += 24f;
                    stripe.position = p;
                }
            }

            PlaceEnemies(1f - Mathf.Exp(-9f * dt), dt);

            for (int i = _falling.Count - 1; i >= 0; i--)
            {
                (Transform root, float age, float side) = _falling[i];
                if (root == null) { _falling.RemoveAt(i); continue; }
                age += dt;
                float t = Mathf.Clamp01(age / FallSeconds);
                // Roll onto the side over the first half (still models; animated ones play their Death), then sink.
                if (side != 0f) root.rotation = Quaternion.Euler(0f, EnemyYaw, side * 85f * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f)));
                Vector3 p = root.position;
                p.y = -Mathf.Max(0f, t - (side != 0f ? 0.55f : 0.65f)) * 1.4f;
                root.position = p;
                if (t >= 1f) { Destroy(root.gameObject); _falling.RemoveAt(i); }
                else _falling[i] = (root, age, side);
            }

            _heroPunch = Mathf.MoveTowards(_heroPunch, 0f, dt * 8f);
            _heroHurt = Mathf.MoveTowards(_heroHurt, 0f, dt * 5f);
            if (_anim != null)
            {
                // Legs carry him on the run; otherwise he stands and breathes between blows.
                if (!_heroDown)
                {
                    // In the saddle the mount does the running.
                    string loop = _sim.Phase == LanePhase.Running && _mount == null ? "Run" : "Idle";
                    if (Time.time >= _castUntil && !_anim.IsPlaying("Attack") && !_anim.IsPlaying("Hit") && !_anim.IsPlaying(loop)) Play(loop, 0.15f);
                    // Shadow Stoop: for a moment the hero stands behind her mark, facing back at it.
                    bool blink = Time.time < _blinkUntil;
                    if (blink != _blinking && _rig != null)
                    {
                        _blinking = blink;
                        _rig.localRotation = Quaternion.Euler(0f, blink ? -125f : 125f, 0f);
                    }
                    _hero.position = new Vector3(blink ? _blinkX : HeroX + RideShift + _heroPunch * 0.12f, _heroY + RideY, 0f);
                }
            }
            else if (_sim.Phase != LanePhase.Dead)
                _hero.position = new Vector3(HeroX + _heroPunch * 0.35f, _heroY + (_sim.Phase == LanePhase.Running ? Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.12f : 0f), 0f);
            UpdateWardrobe(dt);
            Color heroBase = _sim.HasteActive ? new Color(1f, 0.55f, 0.2f) : _anim != null ? _skinTint : _heroTint;
            // Textured looks only flush a little when struck; the grey-box capsule flashes hard.
            Color tint = Color.Lerp(heroBase, Color.red, _heroHurt * (_anim != null ? 0.3f : 0.7f));
            // Honed Edge: the blade burns white-gold while it lasts.
            bool empowered = _sim.EmpowerActive;
            foreach ((Renderer r, int slot) in _heroParts)
                r.material.color = empowered && slot == (int)EquipSlot.Weapon ? EmpowerTint : tint;

            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = _texts[i];
                text.Age += dt;
                text.Mesh.transform.position += Vector3.up * (1.6f * dt);
                Color c = text.Mesh.color;
                c.a = Mathf.Clamp01(1.4f - text.Age * 1.6f);
                text.Mesh.color = c;
                if (text.Age > 0.9f)
                {
                    Destroy(text.Mesh.gameObject);
                    _texts.RemoveAt(i);
                }
            }
        }

        /// <summary>Moves every enemy toward its lane spot; follow = 1 snaps (the editor preview has no frames).</summary>
        public void PlaceEnemies(float follow, float dt = 0f)
        {
            if (_sim == null) return;
            int mobIndex = 0;
            foreach (Enemy enemy in _sim.Enemies)
            {
                if (!_views.TryGetValue(enemy.Id, out EnemyView view)) continue;

                Vector3 target;
                if (enemy.IsKorstone)
                {
                    // The model stands on its base; the box fallback is centred.
                    target = new Vector3(3.4f, view.IsModel ? 0f : 1.3f, 2.2f);
                }
                else if (enemy.IsBoss)
                {
                    target = new Vector3((view.IsModel ? 1.6f : 1.2f) + (_mount != null ? MountMobPush : 0f), view.Y, 0.3f);
                }
                else
                {
                    // Every other mob a step back, so long bodies (wolves, boars) do not sit inside each other.
                    int row = mobIndex / MobsPerRow;
                    target = new Vector3(-0.4f + (_mount != null ? MountMobPush : 0f) + (mobIndex % MobsPerRow) * MobSpacing, view.Y, row * 1.1f + (mobIndex % 2) * 0.5f);
                    mobIndex++;
                }

                if (view.IsMob && view.Anim == null)
                    target.y += Mathf.Abs(Mathf.Sin(Time.time * 5f + enemy.Id * 1.7f)) * 0.04f;   // restless on their feet
                if (view.Anim != null && !view.Anim.IsPlaying("Attack") && !view.Anim.IsPlaying("Hit"))
                {
                    // Animated mobs run while they close in and stand breathing in their place.
                    string loop = (view.Base - target).sqrMagnitude > 0.04f ? "Run" : "Idle";
                    if (!view.Anim.IsPlaying(loop)) view.Anim.CrossFade(loop, 0.2f);
                }
                view.Base = Vector3.Lerp(view.Base, target, follow);
                Vector3 lunge = Vector3.zero;
                if (view.LungeT >= 0f)
                {
                    // A quick leap at the hero and back: out in the first half, home in the second.
                    view.LungeT += dt;
                    float t = view.LungeT / (view.IsBoss ? 0.45f : 0.32f);
                    if (t >= 1f) view.LungeT = -1f;
                    else lunge = new Vector3(-Mathf.Sin(t * Mathf.PI) * (view.IsBoss ? 0.9f : 0.55f), Mathf.Sin(t * Mathf.PI) * 0.12f, 0f)
                                 * (view.Anim != null ? 0.45f : 1f);   // an Attack clip carries its own lunge
                }
                view.Root.position = view.Base + lunge;
                view.Punch = Mathf.MoveTowards(view.Punch, 0f, dt * 6f);
                view.Root.localScale = view.Scale * (1f + view.Punch * 0.18f);

                float ratio = Mathf.Clamp01(enemy.Hp / (float)enemy.MaxHp);
                view.HpFill.localScale = new Vector3(ratio, 1f, 1f);
                view.HpFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, 0f, -0.01f);
            }
        }

        /// <summary>
        /// "A bit muscled up" (owner, 24 Sep 2026): the Vanguard rig is drawn broader and a touch taller, which widens
        /// shoulders, chest and arms on every armour look without new art.
        /// </summary>
        private static readonly Vector3 VanguardBuild = new Vector3(1.1f, 1.03f, 1.1f);

        private HeroClass _class = HeroClass.Vanguard;
        private string _classSkin;

        /// <summary>
        /// A wardrobe skin's own model for a class, when it has been made (Vanguard: an armour look id); for a second look
        /// its own cut of the costume ("...Alt"), or null (the lane then tints the band armour).
        /// </summary>
        public static string SkinModel(HeroClass cls, string look, bool secondLook = false)
        {
            if (look == null) return null;
            if (secondLook)
            {
                string first = SkinModelName(cls, look);
                string alt = AltName(first);
                string altKey = cls + "/" + look + "/alt";
                if (!SkinModels.TryGetValue(altKey, out string altFound))
                {
                    string altFolder = cls == HeroClass.Vanguard ? "Models/Looks/" : "Models/Classes/";
                    altFound = Art.Load<GameObject>(altFolder + alt) != null ? alt : null;
                    SkinModels[altKey] = altFound;
                }
                return altFound;
            }
            string key = cls + "/" + look;
            if (SkinModels.TryGetValue(key, out string found)) return found;
            string name = SkinModelName(cls, look);
            string folder = cls == HeroClass.Vanguard ? "Models/Looks/" : "Models/Classes/";
            found = Art.Load<GameObject>(folder + name) != null ? name : null;
            SkinModels[key] = found;
            return found;
        }

        private static readonly Dictionary<string, string> SkinModels = new Dictionary<string, string>();

        /// <summary>A costume's first-look model name: "Skin_AmberRoad" for the Vanguard, "Kestrel_SkinAmberRoad" for the others.</summary>
        internal static string SkinModelName(HeroClass cls, string look) => cls == HeroClass.Vanguard ? "Skin_" + look : cls + "_Skin" + look;
        private GameObject _classLook;
        private int _classBand = -1;

        /// <summary>
        /// Shows the class being played: the Vanguard's armour and glaive looks, or another class's own model for the
        /// armour's level band (Models/Classes/&lt;Class&gt;_T&lt;band&gt;, nearest band if that one is not drawn yet).
        /// </summary>
        // ---- The wardrobe on the lane (owner, 25 Sep 2026): a skin tints the hero (GameRoot picks its band), a mount
        // carries him with his legs held in a riding pose, a companion trots behind or glides above. ----

        /// <summary>Skin looks (WardrobeDef.Look): the armour band shown and the tint over it.</summary>
        public static readonly Dictionary<string, (int Band, Color Tint)> SkinLooks = new Dictionary<string, (int, Color)>
        {
            ["SaltNomad"] = (1, new Color(1f, 0.93f, 0.78f)),
            ["FrostHunter"] = (3, new Color(0.82f, 0.92f, 1f)),
            ["EmberKhan"] = (5, new Color(1f, 0.9f, 0.84f)),
            ["GraveWarden"] = (4, new Color(0.62f, 0.52f, 0.8f)),
            ["TulGorak"] = (2, new Color(0.92f, 0.76f, 0.7f)),
            ["MirageVeil"] = (3, new Color(1f, 0.92f, 0.66f)),
            ["GreyjawPelt"] = (2, new Color(0.78f, 0.78f, 0.8f)),
            ["AmberRoad"] = (4, new Color(0.8f, 0.95f, 0.95f)),
            ["WhiteSteppe"] = (4, new Color(0.88f, 0.94f, 1f)),
        };

        /// <summary>A mount's model, its scale on the lane, and how far behind its middle the saddle sits (the camel's is
        /// between its humps).</summary>
        private static readonly Dictionary<string, (string Model, float Scale, float Saddle)> MountLooks = new Dictionary<string, (string, float, float)>
        {
            ["HorsePony"] = ("MountPony", 1f, SaddleBack),
            ["HorseEmber"] = ("MountWarhorse", 0.88f, SaddleBack),
            ["HorseGold"] = ("MountWarhorseGold", 0.9f, SaddleBack),
            ["HorseHollow"] = ("MountWarhorseHollow", 0.92f, SaddleBack),
            ["HorseAmber"] = ("MountWarhorseAmber", 0.91f, SaddleBack),
            ["HorseWhite"] = ("MountWarhorseWhite", 0.91f, SaddleBack),
            ["Camel"] = ("MountCamel", 0.88f, 0.27f),
            ["Yak"] = ("MountYak", 0.95f, SaddleBack),
            ["Stag"] = ("MountStag", 0.9f, SaddleBack),
        };

        private static readonly Dictionary<string, (string Model, float Scale, bool Flies)> CompanionLooks = new Dictionary<string, (string, float, bool)>
        {
            ["Fox"] = ("PetFox", 1f, false),
            ["WolfPup"] = ("Wolf", 0.5f, false),
            ["Falcon"] = ("PetFalcon", 1.4f, true),
            ["Eagle"] = ("PetEagle", 1.8f, true),
            ["Lynx"] = ("PetLynx", 1f, false),
            ["Owl"] = ("PetOwl", 1.6f, true),
            ["Raven"] = ("PetRaven", 1.5f, true),
        };

        /// <summary>
        /// Seating (owner, 27 Sep 2026: "make classes mounted well to all mounts"): the rider's hip joints sit SeatClear above
        /// the saddle's top, and his thighs open until his knees clear the mount's barrel by KneeClear, both measured from
        /// the meshes (Seat, RiderFit), so every class and figure fits every mount. The rest is the old guess for a mount
        /// or a rig the measure cannot read.
        /// </summary>
        private const float SeatClear = 0.07f, KneeClear = 0.04f, MinThighOut = 8f, MaxThighOut = 48f, MaxShinOut = 30f;
        /// <summary>
        /// Mounted, the rider is drawn this far right of HeroX (so the horse's hindquarters stay in frame), the saddle sits
        /// this far behind the mount's centre, and the mobs line up this much further off (clear of the horse's head).
        /// </summary>
        private const float MountShift = 0.6f, SaddleBack = 0.6f, MountMobPush = 1.3f;
        private float RideShift => _mount != null && !_heroDown ? MountShift : 0f;
        /// <summary>
        /// The riding legs, aimed in the rider's own frame (models face +Z, up +Y) so the bones' rolls do not matter: the
        /// rigs' left and right thighs have mirrored axes, and one Euler swing for both sent the right leg back into the
        /// mount. Thighs pitch RideThighPitch forward of straight down and open to clear the barrel (FitRider); shins hang
        /// RideShinPitch from straight down (negative: heels back) and a little out; feet keep their rest angle.
        /// </summary>
        private static readonly float RideThighPitch = ArgFloat("-ridethigh", 55f), RideShinPitch = ArgFloat("-rideshin", -8f), RideShinOut = 4f;

        private sealed class RideLeg
        {
            public Transform Thigh, Shin, Foot;
            /// <summary>Rest rotations and directions (hip to knee, knee to ankle) in the look's own space.</summary>
            public Quaternion ThighRest, ShinRest, FootRest;
            public Vector3 ThighDir, ShinDir;
            /// <summary>Which way along the look's X is outward for this leg (the hip's side).</summary>
            public float Side;
        }

        private readonly List<RideLeg> _rideLegs = new List<RideLeg>();

        private static readonly float RideSeatClear = ArgFloat("-rideclear", SeatClear);

        private static float ArgFloat(string name, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        /// <summary>
        /// A mount model's saddle top and its barrel's half-width at each height (SeatBin steps from the ground up) along the
        /// rider's legs, in its own units (times its scale on the lane).
        /// </summary>
        private sealed class MountSeat
        {
            public float Top;
            public float[] Half;
        }

        private const float SeatBin = 0.05f;
        private static readonly Dictionary<string, MountSeat> Seats = new Dictionary<string, MountSeat>();
        private MountSeat _seat;
        private float _mountScale = 1f, _saddleBack = SaddleBack, _thighOut = 16f, _shinOut = 4f;

        private Color _skinTint = Color.white;
        private string _mountKey, _companionKey;
        private Transform _mount, _companion;
        private Animation _mountAnim, _companionAnim;
        private float _rideY;
        private bool _companionFlies;
        private GameObject _ridePoseFor;

        private float RideY => _mount != null && !_heroDown ? _rideY + (_sim != null && _sim.Phase == LanePhase.Running ? Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.05f : 0f) : 0f;

        /// <summary>Shows the worn mount and companion (WardrobeDef.Look keys; null for none) and the skin's tint.</summary>
        public void SetWardrobe(string mountLook, string companionLook, Color skinTint)
        {
            if (_rig == null) return;
            _skinTint = skinTint;
            if (mountLook != _mountKey)
            {
                _mountKey = mountLook;
                if (_mount != null) Kill(_mount.gameObject);
                _mount = null;
                _mountAnim = null;
                _ridePoseFor = null;
                if (mountLook != null && MountLooks.TryGetValue(mountLook, out var m))
                {
                    _mount = Companion(m.Model, m.Scale, out _mountAnim, out _mountHeight);
                    _mountScale = m.Scale;
                    _saddleBack = m.Saddle;
                    _seat = _mount != null ? Seat(m.Model, _mount, m.Scale, m.Saddle) : null;
                }
                // The seat is fitted to the rider in LateUpdate (his hips and thighs); until then the old guess.
                if (_mount != null) _rideY = Mathf.Max(0f, _mountHeight * 0.56f - _rig.localScale.y);
            }
            if (companionLook != _companionKey)
            {
                _companionKey = companionLook;
                if (_companion != null) Kill(_companion.gameObject);
                _companion = null;
                _companionAnim = null;
                if (companionLook != null && CompanionLooks.TryGetValue(companionLook, out var c))
                {
                    _companion = Companion(c.Model, c.Scale, out _companionAnim, out _);
                    _companionFlies = c.Flies;
                }
            }
        }

        private float _mountHeight;

        /// <summary>
        /// Measures a mount once: the top of its back over the saddle (<paramref name="back"/> behind its origin, which faces +Z) and, at
        /// each height below it, how wide its barrel gets between the saddle and a stride ahead, from the mesh's rest shape.
        /// Null when the mesh cannot be read (then the rider keeps the old guess).
        /// </summary>
        private static MountSeat Seat(string model, Transform root, float scale, float back)
        {
            if (Seats.TryGetValue(model, out MountSeat known)) return known;
            float saddle = -back / scale, stride = 0.7f / scale, slab = 0.15f / scale;
            var points = new List<Vector3>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 p = root.InverseTransformPoint(r.transform.TransformPoint(v));
                    if (p.z > saddle - slab && p.z < saddle + stride) points.Add(p);
                }
            }
            float top = 0f;
            foreach (Vector3 p in points)
                if (Mathf.Abs(p.z - saddle) < slab) top = Mathf.Max(top, p.y);
            MountSeat seat = null;
            if (top > 0f)
            {
                seat = new MountSeat { Top = top, Half = new float[Mathf.CeilToInt(top / SeatBin) + 1] };
                foreach (Vector3 p in points)
                {
                    int bin = Mathf.FloorToInt(p.y / SeatBin);
                    if (bin >= 0 && bin < seat.Half.Length) seat.Half[bin] = Mathf.Max(seat.Half[bin], Mathf.Abs(p.x));
                }
            }
            Seats[model] = seat;
            return seat;
        }

        /// <summary>The mount's barrel half-width at a height above the ground, in lane units.</summary>
        private float BarrelHalf(float y)
        {
            int bin = Mathf.FloorToInt(y / _mountScale / SeatBin);
            return _seat == null || bin < 0 || bin >= _seat.Half.Length ? 0f : _seat.Half[bin] * _mountScale;
        }

        private Transform Companion(string model, float scale, out Animation anim, out float height)
        {
            anim = null;
            height = 0f;
            MobArt art = LoadMob(model);
            if (art == null) return null;
            Transform root = Instantiate(art.Model).transform;
            root.name = model;
            root.SetParent(transform, false);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = art.Material;
                height = Mathf.Max(height, r.bounds.max.y);
            }
            root.localScale = Vector3.one * scale;
            root.rotation = Quaternion.Euler(0f, -EnemyYaw, 0f);   // faces the enemies like the hero
            height *= scale;
            anim = root.GetComponent<Animation>();
            if (anim != null && anim.GetClip("Idle") == null) anim = null;
            if (anim != null)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.Play("Idle");
            }
            return root;
        }

        private void UpdateWardrobe(float dt)
        {
            bool running = _sim.Phase == LanePhase.Running;
            if (_mount != null)
            {
                // The saddle under the rider: the mount's centre sits its saddle distance ahead of him along its facing.
                Vector3 facing = _mount.rotation * Vector3.forward;
                _mount.position = new Vector3(HeroX + MountShift + _heroPunch * 0.12f, 0f, 0f) + new Vector3(facing.x, 0f, facing.z) * _saddleBack;
                string loop = running ? "Run" : "Idle";
                if (_mountAnim != null && !_mountAnim.IsPlaying(loop)) _mountAnim.CrossFade(loop, 0.2f);
            }
            if (_companion != null)
            {
                float x = HeroX + RideShift;
                if (_companionFlies)
                {
                    // Gliding ahead of the hero, a little toward the camera, like a falcon sent after the quarry.
                    float t = Time.time;
                    _companion.position = new Vector3(x + 1.3f + Mathf.Sin(t * 0.7f) * 0.25f, 1.5f + RideY + Mathf.Sin(t * 2.1f) * 0.15f, -1.3f);
                    // Turned mostly to the camera so the spread wings show (side on, a glider is a line).
                    _companion.rotation = Quaternion.Euler(12f + Mathf.Sin(t * 2.1f) * 6f, 160f, Mathf.Sin(t * 1.3f) * 12f);
                }
                else
                {
                    // At the hero's feet, a step toward the camera (in front of the horse when mounted).
                    _companion.position = new Vector3(x + (_mount != null ? 0.9f : 0.5f), 0f, -1.1f);
                    string loop = running ? "Run" : "Idle";
                    if (_companionAnim != null && !_companionAnim.IsPlaying(loop)) _companionAnim.CrossFade(loop, 0.2f);
                }
            }
        }

        /// <summary>Holds the rider's legs in the saddle after the clips have posed him (the clips move the upper body).</summary>
        private void LateUpdate()
        {
            if (_mount == null || _heroDown || _anim == null) return;
            GameObject look = _classLook != null ? _classLook : _armorLook;
            if (look == null) return;
            if (_ridePoseFor != look)
            {
                _ridePoseFor = look;
                _rideLegs.Clear();
                SkinnedMeshRenderer skin = look.GetComponentInChildren<SkinnedMeshRenderer>();
                FitRider(look, skin);
                if (skin != null)
                    foreach (string side in new[] { "L", "R" })
                    {
                        if (!RestPose(look, skin, "thigh." + side, out Transform thigh, out Vector3 hip, out Quaternion thighRest)
                            || !RestPose(look, skin, "shin." + side, out Transform shin, out Vector3 knee, out Quaternion shinRest)
                            || !RestPose(look, skin, "foot." + side, out Transform foot, out Vector3 ankle, out Quaternion footRest)) continue;
                        _rideLegs.Add(new RideLeg
                        {
                            Thigh = thigh, Shin = shin, Foot = foot, ThighRest = thighRest, ShinRest = shinRest, FootRest = footRest,
                            ThighDir = (knee - hip).normalized, ShinDir = (ankle - knee).normalized, Side = Mathf.Sign(hip.x),
                        });
                    }
            }
            if (_rideLegs.Count == 0) return;
            Quaternion frame = look.transform.rotation;
            float pitch = RideThighPitch * Mathf.Deg2Rad, open = _thighOut * Mathf.Deg2Rad, hang = RideShinPitch * Mathf.Deg2Rad, flare = _shinOut * Mathf.Deg2Rad;
            foreach (RideLeg leg in _rideLegs)
            {
                var outward = new Vector3(leg.Side, 0f, 0f);
                Vector3 thighTo = (Vector3.forward * Mathf.Sin(pitch) + Vector3.down * Mathf.Cos(pitch)) * Mathf.Cos(open) + outward * Mathf.Sin(open);
                Vector3 shinTo = (Vector3.forward * Mathf.Sin(hang) + Vector3.down * Mathf.Cos(hang)) * Mathf.Cos(flare) + outward * Mathf.Sin(flare);
                leg.Thigh.rotation = frame * Quaternion.FromToRotation(leg.ThighDir, thighTo) * leg.ThighRest;
                leg.Shin.rotation = frame * Quaternion.FromToRotation(leg.ShinDir, shinTo) * leg.ShinRest;
                leg.Foot.rotation = frame * leg.FootRest;
            }
            // The clips lift and drop the hips: the hip joints are held on the saddle, the body above them moves as it likes.
            if (_seat != null)
            {
                float hipY = 0f;
                foreach (RideLeg leg in _rideLegs) hipY += leg.Thigh.position.y / _rideLegs.Count;
                float want = _mount.position.y + _seat.Top * _mountScale + RideSeatClear + (RideY - _rideY);
                _hero.position += Vector3.up * (want - hipY);
            }

        }

        /// <summary>
        /// Seats this rider on this mount: his hip joints (the thighs' heads at rest) go SeatClear above the saddle, his
        /// thighs open until the knees clear the barrel at knee height by KneeClear, and his shins flare just enough to clear
        /// it below the knee (the barrel is widest at the belly).
        /// </summary>
        private void FitRider(GameObject look, SkinnedMeshRenderer skin)
        {
            _thighOut = 16f;
            _shinOut = RideShinOut;
            if (_seat == null || skin == null) return;
            if (!RestPoint(look, skin, "thigh.L", out Vector3 hip) || !RestPoint(look, skin, "shin.L", out Vector3 knee)
                || !RestPoint(look, skin, "foot.L", out Vector3 ankle)) return;
            float s = look.transform.lossyScale.y / Mathf.Max(0.0001f, transform.lossyScale.y);
            float hipY = hip.y * s, hipHalf = Mathf.Abs(hip.x) * s, thigh = Vector3.Distance(hip, knee) * s, shin = Vector3.Distance(knee, ankle) * s;
            float seatY = _seat.Top * _mountScale + SeatClear, pitch = RideThighPitch * Mathf.Deg2Rad;
            _rideY = Mathf.Max(0f, seatY - hipY);
            // The knee's height depends on how far the thigh opens: settle it in a few passes.
            float open = MinThighOut * Mathf.Deg2Rad, kneeY = seatY;
            for (int pass = 0; pass < 4; pass++)
            {
                kneeY = seatY - thigh * Mathf.Cos(pitch) * Mathf.Cos(open);
                float need = (BarrelHalf(kneeY) + KneeClear - hipHalf) / Mathf.Max(0.05f, thigh);
                open = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(need, -1f, 1f)), MinThighOut * Mathf.Deg2Rad, MaxThighOut * Mathf.Deg2Rad);
            }
            _thighOut = open * Mathf.Rad2Deg;
            // Down the shin, the barrel may be wider than at the knee: flare the shin to clear it.
            float kneeOut = hipHalf + thigh * Mathf.Sin(open), flare = RideShinOut * Mathf.Deg2Rad;
            for (float drop = SeatBin; drop <= shin; drop += SeatBin)
            {
                float wide = BarrelHalf(kneeY - drop) + KneeClear - kneeOut;
                if (wide > 0f) flare = Mathf.Max(flare, Mathf.Atan2(wide, drop));
            }
            _shinOut = Mathf.Min(flare * Mathf.Rad2Deg, MaxShinOut);
        }

        /// <summary>Where a bone's head sits at rest, in the look's own space (from the skin's bind poses).</summary>
        private static bool RestPoint(GameObject look, SkinnedMeshRenderer skin, string bone, out Vector3 point) =>
            RestPose(look, skin, bone, out _, out point, out _);

        /// <summary>A bone, and its head and rotation at rest in the look's own space (from the skin's bind poses).</summary>
        private static bool RestPose(GameObject look, SkinnedMeshRenderer skin, string bone, out Transform t, out Vector3 point, out Quaternion rotation)
        {
            point = Vector3.zero;
            rotation = Quaternion.identity;
            t = FindDeep(look.transform, bone);
            int i = t == null ? -1 : System.Array.IndexOf(skin.bones, t);
            if (i < 0) return false;
            Matrix4x4 inLook = look.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[i].inverse;
            point = inLook.MultiplyPoint3x4(Vector3.zero);
            rotation = Quaternion.LookRotation(inLook.GetColumn(2), inLook.GetColumn(1));
            return true;
        }

        /// <summary>
        /// The class's body for an armour band, or a wardrobe skin's own model when <paramref name="skinModel"/> names one
        /// that exists (Models/Classes/&lt;Class&gt;_Skin&lt;Look&gt;; the Vanguard's skins are armour looks, SetLooks).
        /// </summary>
        public void SetHeroClass(HeroClass cls, int band = 0, string skinModel = null, bool secondLook = false)
        {
            if (_rig == null || (cls == _class && secondLook == _second && (cls == HeroClass.Vanguard || (band == _classBand && skinModel == _classSkin)))) return;
            if (_hero.rotation != Quaternion.identity || _heroDown) return;   // swap once back on the feet
            _class = cls;
            _second = secondLook;
            _classBand = band;
            _classSkin = skinModel;
            if (_armorLook != null) Kill(_armorLook);
            if (_weaponLook != null) Kill(_weaponLook);
            if (_classLook != null) Kill(_classLook);
            _armorLook = _weaponLook = _classLook = null;
            _armorLookId = _weaponLookId = null;
            _heroParts.Clear();
            for (int i = 0; i < _partGlow.Length; i++) _partGlow[i] = -1f;
            _anim = null;
            _rig.localScale = cls == HeroClass.Vanguard ? VanguardBuild : Vector3.one;
            if (cls == HeroClass.Vanguard) return;   // GameRoot's next SetLooks rebuilds him

            string name = skinModel ?? ClassLookName(cls, band, secondLook);
            if (name == null) return;
            var prefab = Art.Load<GameObject>("Models/Classes/" + name);
            _classLook = Instantiate(prefab, _rig);
            _anim = _classLook.GetComponent<Animation>();
            if (_anim != null && _anim.GetClip("Idle") == null) _anim = null;
            CastClips.Ensure(_anim, cls);
            var material = Art.Load<Material>("Looks/" + name);
            _lookPrefab = prefab;
            _lookMaterial = material;
            _weaponPrefab = null;
            _weaponMaterial = null;
            Bounds b = default;
            bool first = true;
            foreach (Renderer r in _classLook.GetComponentsInChildren<Renderer>())
            {
                if (material != null) r.sharedMaterial = material;
                // Body parts shine with the armour's level, the blade or staff with the weapon's.
                int slot = SlotOf(r.name);
                _heroParts.Add((r, slot == NoSlot ? (int)EquipSlot.Armor : slot));
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            Vector3 origin = _rig.position;
            _classLook.transform.position -= new Vector3(b.center.x - origin.x, b.min.y - origin.y, b.center.z - origin.z);
            Play("Idle", 0f);
        }

        /// <summary>
        /// The class model for a band, or the nearest band drawn so far; the second look's ("KestrelAlt_T3") when asked
        /// and drawn, else the first look's.
        /// </summary>
        internal static string ClassLookName(HeroClass cls, int band, bool secondLook = false)
        {
            if (secondLook && NearestClassLook(cls + "Alt", band) is string alt) return alt;
            return NearestClassLook(cls.ToString(), band);
        }

        private static string NearestClassLook(string kind, int band)
        {
            for (int step = 0; step <= ItemLooks.MaxTier; step++)
                foreach (int t in new[] { band - step, band + step })
                {
                    if (t < 0 || t > ItemLooks.MaxTier) continue;
                    string name = kind + "_T" + t;
                    if (Art.Load<GameObject>("Models/Classes/" + name) != null) return name;
                }
            return null;
        }

        private void Play(string clip, float fade)
        {
            if (_anim == null || _anim.GetClip(clip) == null) return;
            if (fade <= 0f) _anim.Play(clip);
            else _anim.CrossFade(clip, fade);
        }

        /// <summary>A glaive chop per landed hit; a new one waits until the last is past its strike frame.</summary>
        private void Strike()
        {
            if (_anim == null || _heroDown || Time.time < _castUntil) return;
            AnimationState attack = _anim["Attack"];
            if (attack != null && _anim.IsPlaying("Attack") && attack.normalizedTime < 0.6f) return;
            _anim.CrossFade("Attack", 0.05f);
            _anim["Attack"].time = 0f;
        }

        private void Flinch()
        {
            if (_anim == null || _heroDown || _anim.IsPlaying("Attack") || Time.time < _castUntil) return;
            _anim.CrossFade("Hit", 0.05f);
        }

        /// <summary>A skill's cast clip (CastClips); plain blows and flinches wait until it is done.</summary>
        private void PlayCast(int slot)
        {
            if (_anim == null || _heroDown) return;
            string clip = CastClips.ClipName(slot);
            if (_anim.GetClip(clip) == null) return;
            _anim.CrossFade(clip, 0.06f);
            _anim[clip].time = 0f;
            _castUntil = Time.time + CastClips.LengthSeconds(_class, slot);
        }

        /// <summary>An enemy where the skills' effects find it: feet, the height blows land at, the top of the head.</summary>
        internal struct Spot
        {
            public int Id;
            public Vector3 Base;
            public Vector3 Centre;
            public Vector3 Top;
            public bool Boss;
            public bool Korstone;
            public long Hp;
        }

        internal List<Spot> EnemySpots()
        {
            var spots = new List<Spot>();
            if (_sim == null) return spots;
            foreach (Enemy enemy in _sim.Enemies)
            {
                if (!_views.TryGetValue(enemy.Id, out EnemyView view) || view.Root == null) continue;
                Vector3 p = view.Root.position;
                spots.Add(new Spot
                {
                    Id = enemy.Id, Base = new Vector3(p.x, 0f, p.z), Centre = p + Vector3.up * view.HitHeight,
                    Top = p + Vector3.up * (view.HitHeight * 1.5f + 0.2f), Boss = enemy.IsBoss, Korstone = enemy.IsKorstone, Hp = enemy.Hp,
                });
            }
            return spots;
        }

        internal HeroClass HeroClassShown => _class;
        internal Vector3 HeroGround => new Vector3(_hero.position.x, 0f, _hero.position.z);
        internal Vector3 HandPoint => HandPosition;
        internal Vector3 DrumPoint => _hero.position + new Vector3(0.35f, 1.2f, -0.35f);
        internal Vector3 WardPoint => new Vector3(_hero.position.x + 1.05f, 1.15f, _hero.position.z - 0.3f);

        /// <summary>The weapon from grip to tip (the Vanguard's glaive bones), or a line up from the hand.</summary>
        internal (Vector3 Grip, Vector3 Tip) WeaponLine
        {
            get
            {
                Transform body = _armorLook != null ? _armorLook.transform : _classLook != null ? _classLook.transform : null;
                Transform grip = body != null ? FindDeep(body, "WeaponBase") : null, tip = body != null ? FindDeep(body, "WeaponTip") : null;
                Transform fist = body != null && _armorLook != null ? FindDeep(body, "WeaponGrip") : null;
                if (grip != null && tip != null && fist != null && _weaponKind != WeaponKind.Glaive)
                {
                    // A sword: from the fist to its point.
                    (float share, float held) = SwordSpan(_weaponKind);
                    Vector3 axis = tip.position - grip.position;
                    float reach = axis.magnitude * share;
                    return (fist.position, fist.position + axis.normalized * reach * (1f - held));
                }
                if (grip != null && tip != null) return (grip.position, tip.position);
                Vector3 hand = HandPosition;
                return (hand, hand + new Vector3(0.3f, 1.3f, 0f));
            }
        }

        /// <summary>Shadow Stoop: the hero stands at x for a moment, turned back toward the lane's start.</summary>
        internal void Blink(float x, float seconds)
        {
            _blinkX = x;
            _blinkUntil = Time.time + seconds;
        }

        /// <summary>
        /// The Peerless spirit: a fresh copy of the hero's model under <paramref name="parent"/>, built from the same look
        /// prefabs, facing as he does, with its own cast clips and (for the Vanguard) the glaive laid on its grip.
        /// </summary>
        internal GameObject BuildSpirit(Transform parent)
        {
            GameObject look = _classLook != null ? _classLook : _armorLook;
            if (_lookPrefab == null || _rig == null || look == null) return null;
            var holder = new GameObject("SpiritRig").transform;
            holder.SetParent(parent, false);
            holder.localRotation = _rig.localRotation;
            holder.localScale = _rig.localScale;
            GameObject body = Instantiate(_lookPrefab, holder, false);
            body.transform.localPosition = look.transform.localPosition;
            body.transform.localRotation = look.transform.localRotation;
            body.transform.localScale = look.transform.localScale;
            if (_lookMaterial != null) foreach (Renderer r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _lookMaterial;
            var anim = body.GetComponent<Animation>();
            if (anim != null && anim.GetClip("Idle") != null) CastClips.Ensure(anim, _class);
            if (_weaponPrefab != null)
            {
                GameObject weapon = Instantiate(_weaponPrefab, holder, false);
                if (_weaponMaterial != null) foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _weaponMaterial;
                LayWeapon(weapon, body.transform, holder, anim != null, _weaponKind);
            }
            return body;
        }

        /// <summary>Editor preview: poses the hero at a clip's normalised time (no Play mode, so no Animation update).</summary>
        public bool PoseHero(string clip, float normalizedTime)
        {
            if (_anim == null) return false;
            AnimationClip c = _anim.GetClip(clip);
            if (c == null) return false;
            c.SampleAnimation(_anim.gameObject, normalizedTime * c.length);
            return true;
        }

        private ParticleSystem _sparks;
        private int _sparksThisFrame;
        private int _sparkFrame;

        /// <summary>Hit sparks: one pooled world-space particle system, capped per frame so packs stay cheap on phones.</summary>
        private void Sparks(Vector3 at, int count, Color color, float speed)
        {
            if (_sparks == null)
            {
                var material = Art.Load<Material>("FxSpark");
                if (material == null) return;
                var go = new GameObject("HitSparks");
                go.transform.SetParent(transform, false);
                _sparks = go.AddComponent<ParticleSystem>();
                _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = _sparks.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.gravityModifier = 1.1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 700;
                ParticleSystem.EmissionModule emission = _sparks.emission;
                emission.enabled = false;
                ParticleSystem.ShapeModule shape = _sparks.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
                ParticleSystem.ColorOverLifetimeModule fade = _sparks.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.5f;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _sparks.Play();
            }
            if (_sparkFrame != Time.frameCount) { _sparkFrame = Time.frameCount; _sparksThisFrame = 0; }
            count = Mathf.Min(count, 120 - _sparksThisFrame);
            if (count <= 0) return;
            _sparksThisFrame += count;
            var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true, startColor = color };
            for (int i = 0; i < count; i++)
            {
                emit.velocity = (Random.onUnitSphere + Vector3.up * 0.8f).normalized * Random.Range(2.5f, 6.5f) * speed;
                _sparks.Emit(emit, 1);
            }
        }

        private static Vector3 BaseScale(bool korstone, bool boss = false) =>
            korstone ? new Vector3(1.3f, 2.6f, 1.3f) : boss ? new Vector3(1.2f, 1.2f, 1.2f) : new Vector3(0.6f, 0.7f, 0.6f);

        private void SpawnView(int enemyId)
        {
            bool korstone = false, boss = false;
            EnemyKind kind = EnemyKind.Mob;
            foreach (Enemy enemy in _sim.Enemies)
                if (enemy.Id == enemyId) { korstone = enemy.IsKorstone; boss = enemy.IsBoss; kind = enemy.Kind; }

            Color color = korstone ? KorstoneColor : boss ? BossColor
                : kind == EnemyKind.Captain ? new Color(0.85f, 0.55f, 0.15f)
                : kind == EnemyKind.Image ? new Color(0.6f, 0.4f, 0.9f, 0.6f) : MobColor;
            GameObject model = korstone ? KorstoneModel() : null;
            float artScale = 1f;
            Color? artTint = null;
            string artName = null;
            MobArt art = korstone ? null : ArtFor(enemyId, kind, out artScale, out artTint, out artName);
            KorstoneFx korstoneFx = null;
            float korstoneHeight = 0f;
            Transform root;
            float barHeight;
            Vector3 s;
            if (art != null)
            {
                root = Instantiate(art.Model).transform;
                if (art.Height < 0f)
                {
                    // Measured once, unscaled at the origin: the model's own height, for the HP bar.
                    float top = 0.5f;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
                    art.Height = top;
                }
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = art.Material;
                    if (artTint.HasValue) r.material.color = artTint.Value;
                }
                s = Vector3.one * artScale;
                barHeight = art.Height + 0.3f / artScale;
                root.rotation = Quaternion.Euler(0f, EnemyYaw, 0f);
                if (artName != null && artName.StartsWith("Armor", System.StringComparison.Ordinal))
                {
                    ArmRival(root, artName);
                    s = Vector3.Scale(s, VanguardBuild);   // as broad as the hero Vanguard
                }
            }
            else if (korstone && TryKorstone(kind == EnemyKind.ElderKorstone, _sim.Stage.GearItemLevel, out root, out korstoneFx, out korstoneHeight))
            {
                model = root.gameObject;
                s = root.localScale;
                barHeight = korstoneHeight / s.y + 0.35f / s.y;
            }
            else
            {
                root = model != null ? Instantiate(model).transform : Primitive(boss ? PrimitiveType.Capsule : PrimitiveType.Cube, kind.ToString(), color);
                if (model != null)
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _korstoneMaterial;
                s = model != null ? Vector3.one : BaseScale(korstone, boss);
                barHeight = model != null ? 2.9f : 0.75f;
                if (korstone) root.rotation = Quaternion.Euler(0f, 25f, model != null ? 0f : 4f);
            }
            root.name = kind.ToString();
            if (kind == EnemyKind.ElderKorstone && korstoneFx == null) s *= 1.3f;
            bool standing = model != null || art != null;
            float y = standing ? 0f : korstone ? 1.3f : boss ? 1.2f : 0.35f;
            root.SetParent(transform, false);
            root.position = new Vector3(SpawnX, y, korstone ? 2.2f : 0f);
            root.localScale = s;

            // Bars are parented to a holder that cancels the body's scale, so they keep a fixed size.
            var holder = new GameObject("HpBar").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0f, barHeight, 0f);
            holder.localScale = new Vector3((korstone || boss ? 1.6f : 0.7f) / s.x, 0.09f / s.y, 0.05f / s.z);
            holder.rotation = Quaternion.identity;

            Transform back = Primitive(PrimitiveType.Cube, "Back", new Color(0.05f, 0.05f, 0.05f));
            back.SetParent(holder, false);
            Transform fill = Primitive(PrimitiveType.Cube, "Fill", korstone ? Palette.Warn : boss ? Palette.Bad : Palette.Good);
            fill.SetParent(holder, false);
            fill.localPosition = new Vector3(0f, 0f, -0.01f);

            float hitHeight = art != null ? art.Height * s.y * 0.55f : korstoneFx != null ? korstoneHeight * 0.45f : model != null ? 1.4f : 0f;
            Animation anim = art != null ? root.GetComponent<Animation>() : null;
            if (anim != null && anim.GetClip("Idle") == null) anim = null;
            if (anim != null)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.Play("Run");
            }
            _views[enemyId] = new EnemyView
            {
                Root = root, HpFill = fill, Scale = s, IsModel = standing, Y = y, IsMob = art != null, IsKorstone = korstone,
                HitHeight = hitHeight, ArtName = artName, Base = root.position, IsBoss = boss, Fx = korstoneFx, Anim = anim,
            };
            if (korstoneFx != null)
            {
                korstoneFx.Init(_korstoneStone, KorstoneLook.Tiers[_korstoneTier], korstoneHeight, _korstoneTier, Art.Load<Material>("FxSpark"));
                korstoneFx.Wave(90, 9f);
                GameAudio.Instance?.Play("KorstoneAwaken", 1f, 1f, 0.03f);
            }
        }

        /// <summary>
        /// The model for an enemy: mobs cycle their ground's three kinds by id; the Commanders (Tul-Gorak, the Mirage Queen,
        /// Old Greyjaw) have their own rigged models, Tul-Gorak's captains are deserters in war-red, and the Queen's images
        /// are her own model washed violet.
        /// </summary>
        private MobArt ArtFor(int enemyId, EnemyKind kind, out float scale, out Color? tint, out string name)
        {
            scale = MobScale;
            tint = null;
            name = null;
            switch (kind)
            {
                case EnemyKind.Mob:
                {
                    string[] set = MobSetFor(_sim.Stage.StageNumber);
                    string entry = set[enemyId % set.Length];
                    name = ModelOf(entry);
                    if (entry.Length > name.Length && ColorUtility.TryParseHtmlString(entry.Substring(name.Length), out Color shade)) tint = shade;
                    return LoadMob(name);
                }
                case EnemyKind.Captain:
                    scale = 1f;
                    tint = new Color(1f, 0.72f, 0.55f);
                    name = "Deserter";
                    return LoadMob(name);
                case EnemyKind.Boss:
                {
                    // Commanders have their own models (24 Sep 2026); older builds fall back to scaled mobs.
                    string boss = _sim.Stage.BossName ?? "";
                    // Fortress champions (sieges) wear existing shapes: the Gate's warden an ice wight in armour, the
                    // Yard's captain a deserter in steel, the lord of the Hall a darkened warlord.
                    if (boss.StartsWith("Gate Warden")) { scale = 1.15f; name = "IceWight"; tint = new Color(0.85f, 0.8f, 0.75f); return LoadMob(name) ?? LoadMob("Deserter"); }
                    if (boss.StartsWith("Yard Captain")) { scale = 1.3f; name = "Deserter"; tint = new Color(0.7f, 0.75f, 0.85f); return LoadMob(name); }
                    if (boss.StartsWith("Lord of")) { scale = 1.05f; name = "Gorak"; tint = new Color(0.55f, 0.5f, 0.6f); return LoadMob(name) ?? LoadMob("Deserter"); }
                    // A duel's champion (a guild war's "[TAG] Name", or the Pits' opponent, named by SetRival): the defender in
                    // their class's look for their band; a Vanguard in his armour for the band, his weapon laid in his
                    // fist when he is spawned (ArmRival). A deserter in steel if the look is missing.
                    if (boss.StartsWith("[") || (_rivalName != null && boss == _rivalName))
                    {
                        string look = _rivalClass != HeroClass.Vanguard ? ClassLookName(_rivalClass, _rivalBand, _rivalSecond)
                            : "Armor_T" + Mathf.Clamp(_rivalBand, 0, ItemLooks.MaxTier);
                        MobArt rival = null;
                        if (look != null && _rivalClass == HeroClass.Vanguard && _rivalSecond && LoadVanguardLook(AltName(look)) is MobArt her)
                        {
                            rival = her;
                            look = AltName(look);
                        }
                        else if (look != null) rival = _rivalClass == HeroClass.Vanguard ? LoadVanguardLook(look) : LoadClass(look);
                        if (rival != null) { scale = 1f; name = look; return rival; }
                        scale = 1.15f; name = "Deserter"; tint = new Color(0.72f, 0.78f, 0.92f);
                        return LoadMob(name);
                    }
                    // The Spire Warden, the Hollow Spire's ninth floor: a hollowed wight grown huge in the dark.
                    if (boss.StartsWith("The Spire Warden")) { scale = 1.5f; name = "IceWight"; tint = new Color(0.55f, 0.48f, 0.74f); return LoadMob(name) ?? LoadMob("Deserter"); }
                    // Nine-Winters, Whitefang Range's map boss: the ice wight lord, an ice wight grown tall and pale.
                    if (boss.StartsWith("Nine-Winters")) { scale = 1.5f; name = "IceWight"; tint = new Color(0.78f, 0.9f, 1f); return LoadMob(name) ?? LoadMob("Deserter"); }
                    name = boss.Contains("Greyjaw") ? "Greyjaw" : boss.Contains("Gorak") ? "Gorak" : boss.Contains("Mirage") ? "Queen"
                        : boss.Contains("Azhdar") ? "Azhdar" : boss.Contains("Lantern Widow") ? "LanternWidow"
                        : boss.Contains("Silkmother") ? "Silkmother" : boss.Contains("Last Carver") ? "LastCarver"
                        : boss.Contains("Rootfather") ? "Rootfather" : boss.Contains("Coil Mother") ? "CoilMother"
                        : boss.Contains("Hurm") ? "Hurm" : boss.Contains("Merchant-Prince") ? "MerchantPrince"
                        : boss.Contains("Varkesh") ? "Varkesh" : boss.Contains("Khan's Shadow") ? "KhanShadow" : null;
                    MobArt own = name != null ? LoadMob(name) : null;
                    if (own != null) { scale = 1f; return own; }
                    if (boss.Contains("Greyjaw")) { scale = 1.8f; name = "Wolf"; return LoadMob("Wolf"); }
                    if (boss.Contains("Gorak")) { scale = 1.25f; tint = new Color(1f, 0.6f, 0.5f); name = "Deserter"; return LoadMob("Deserter"); }
                    return null;
                }
                case EnemyKind.Image:
                    // The Mirage Queen's false images: her shape, washed violet.
                    scale = 0.9f;
                    tint = new Color(0.72f, 0.55f, 1f);
                    name = "Queen";
                    return LoadMob("Queen");
                default:
                    return null;
            }
        }

        private int _korstoneTier;
        private Material _korstoneStone;

        /// <summary>
        /// A Korstone in its tier's shape and colours (KorstoneLook), with its living effects. False when the shapes are
        /// not in the build, and the old single model is used.
        /// </summary>
        private bool TryKorstone(bool elder, int level, out Transform root, out KorstoneFx fx, out float height)
        {
            root = null;
            fx = null;
            height = 0f;
            _korstoneTier = KorstoneLook.TierFor(level);
            string shape = KorstoneLook.ShapeFor(_korstoneTier, elder);
            var prefab = Art.Load<GameObject>("Models/Korstones/" + shape);
            var baseMaterial = Art.Load<Material>("Korstones/" + shape);
            if (prefab == null || baseMaterial == null) return false;
            KorstoneLook.Tier tier = KorstoneLook.Tiers[_korstoneTier];
            _korstoneStone = new Material(baseMaterial);
            _korstoneStone.SetColor("_Tint", tier.Stone);
            _korstoneStone.SetColor("_GlowColor", tier.Glow);
            _korstoneStone.SetColor("_HotColor", tier.Hot);
            _korstoneStone.SetFloat("_Intensity", tier.Intensity);
            root = Instantiate(prefab).transform;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _korstoneStone;
            float top = 0.5f;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
            float scale = elder ? 1.3f : 1f;
            root.localScale = Vector3.one * scale;
            root.rotation = Quaternion.Euler(0f, 25f, 0f);
            height = top * scale;
            fx = root.gameObject.AddComponent<KorstoneFx>();
            return true;
        }

        /// <summary>Editor preview: a Korstone of the given level standing at a spot, effects settled for a still.</summary>
        public Transform PreviewKorstone(int level, bool elder, Vector3 position)
        {
            if (!TryKorstone(elder, level, out Transform root, out KorstoneFx fx, out float height)) return null;
            root.SetParent(transform, false);
            root.position = position;
            fx.Init(_korstoneStone, KorstoneLook.Tiers[_korstoneTier], height, _korstoneTier, Art.Load<Material>("FxSpark"));
            fx.Settle();
            return root;
        }

        /// <summary>Hero's casting hand, roughly: where bolts leave from.</summary>
        private Vector3 HandPosition => _hero.position + new Vector3(0.45f, 1.35f, -0.2f);

        /// <summary>
        /// Spell classes show their magic on every hit: the Wraithsworn throws a violet void bolt, the Drumcaller calls
        /// lightning down on the target.
        /// </summary>
        private void SpellFx(Vector3 at)
        {
            if (_class == HeroClass.Wraithsworn)
            {
                Vector3 from = HandPosition;
                Vector3 dir = (at - from).normalized;
                for (int i = 0; i < 10; i++)
                {
                    Vector3 p = Vector3.Lerp(from, at, i / 10f) + Random.insideUnitSphere * 0.06f;
                    SparkLine(p, dir * Random.Range(3f, 6f), new Color(0.62f, 0.2f, 1f));
                }
                Sparks(at, 10, new Color(0.7f, 0.35f, 1f), 1.2f);
                GameAudio.Instance?.Play("SpellVoid", 0.5f, 0.12f);
            }
            else if (_class == HeroClass.Drumcaller)
            {
                Vector3 top = at + new Vector3(Random.Range(-0.3f, 0.3f), 4.2f, 0f);
                Vector3 prev = top;
                for (int i = 1; i <= 12; i++)
                {
                    Vector3 p = Vector3.Lerp(top, at, i / 12f) + new Vector3(Random.Range(-0.18f, 0.18f), 0f, Random.Range(-0.1f, 0.1f));
                    SparkLine(p, (p - prev) * 3f, new Color(0.55f, 0.8f, 1f));
                    prev = p;
                }
                Sparks(at, 12, new Color(0.75f, 0.9f, 1f), 1.3f);
                GameAudio.Instance?.Play("SpellLightning", 0.45f, 0.15f);
            }
        }

        /// <summary>One spark placed exactly (for bolts and lightning), sharing the hit-spark system and its cap.</summary>
        private void SparkLine(Vector3 at, Vector3 velocity, Color color)
        {
            if (_sparks == null) Sparks(at, 0, color, 0f);
            if (_sparks == null) return;
            if (_sparkFrame != Time.frameCount) { _sparkFrame = Time.frameCount; _sparksThisFrame = 0; }
            if (_sparksThisFrame >= 140) return;
            _sparksThisFrame++;
            _sparks.Emit(new ParticleSystem.EmitParams { position = at, velocity = velocity, startColor = color, startLifetime = 0.22f, startSize = 0.09f }, 1);
        }

        /// <summary>The sound an enemy makes when it strikes.</summary>
        private static string AttackSound(EnemyView v)
        {
            switch (v.ArtName)
            {
                case "Wolf": return "MobBite";
                case "Boar": return "MobGore";
                case "Greyjaw": case "Gorak": return "BossSlam";
                case "Queen": case "SnowHag": return "SpellVoid";
                case "Scorpion": case "GlassSnake": return "MobBite";
                case "FrostBear": return "MobGore";
                case "MagmaHound": case "SilkSpider": case "Silkmother": return "MobBite";
                case "LastCarver": case "Rootfather": case "RedTreant": return "BossSlam";
                case "BirchStalker": case "MarshSerpent": case "CoilMother": case "LeechSwarm": case "SapHorror": return "MobBite";
                case "StoneGiant": case "Hurm": case "SiegeBeast": case "GildedConstruct": return "BossSlam";
                case "DebtWraith": case "MerchantPrince": return "SpellVoid";
                case "RisenTrooper": case "RisenCaptain": case "ThroneGuard": return "MobClash";
                case "Varkesh": case "KhanShadow": return "BossSlam";
                case "RisenRider": case "KhanHound": return "MobBite";
                case "OathChanter": return "SpellVoid";
                case "Azhdar": return "BossSlam";
                case "LanternWidow": case "HangingSpirit": case "FlameCultist": case "LanternWisp": return "SpellVoid";
                default: return "MobClash";
            }
        }

        private HeroClass _rivalClass;
        private int _rivalBand;
        private string _rivalName;
        private bool _rivalSecond;

        /// <summary>
        /// Who the next duel's champion (a guild war's or the Pits') is dressed as, and the champion's name as the replay's
        /// boss carries it (GameRoot sets it before the replay).
        /// </summary>
        public void SetRival(HeroClass cls, int band, string name, bool secondLook = false)
        {
            _rivalClass = cls;
            _rivalBand = band;
            _rivalSecond = secondLook;
            _rivalName = string.IsNullOrEmpty(name) ? null : name;
        }

        /// <summary>A Vanguard armour look (Models/Looks/Armor_T&lt;band&gt;) as an enemy: rigged with the same five clips.</summary>
        private static MobArt LoadVanguardLook(string look)
        {
            string key = "Look/" + look;
            if (MobArts.TryGetValue(key, out MobArt art)) return art;
            var model = Art.Load<GameObject>("Models/Looks/" + look);
            var material = Art.Load<Material>("Looks/" + look);
            art = model != null && material != null ? new MobArt { Model = model, Material = material, Height = -1f } : null;
            MobArts[key] = art;
            return art;
        }

        /// <summary>A Vanguard rival's weapon for his band, laid in his fist as on the hero, riding his hand.</summary>
        private static void ArmRival(Transform body, string armorLook)
        {
            string band = armorLook.Substring(armorLook.LastIndexOf("_T", System.StringComparison.Ordinal));
            var prefab = Art.Load<GameObject>("Models/Looks/Weapon" + band);
            if (prefab == null) return;
            GameObject weapon = Instantiate(prefab, body, false);
            var material = Art.Load<Material>("Looks/Weapon" + band);
            if (material != null) foreach (Renderer r in weapon.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
            LayWeapon(weapon, body, body, body.GetComponent<Animation>() != null, KindOfLook("Weapon" + band));
        }

        /// <summary>A class look (Models/Classes) as an enemy: rigged with the same five clips as the mobs.</summary>
        private static MobArt LoadClass(string look)
        {
            string key = "Class/" + look;
            if (MobArts.TryGetValue(key, out MobArt art)) return art;
            var model = Art.Load<GameObject>("Models/Classes/" + look);
            var material = Art.Load<Material>("Looks/" + look);
            art = model != null && material != null ? new MobArt { Model = model, Material = material, Height = -1f } : null;
            MobArts[key] = art;
            return art;
        }

        private static MobArt LoadMob(string name)
        {
            if (MobArts.TryGetValue(name, out MobArt art)) return art;
            var model = Art.Load<GameObject>("Models/Mobs/" + name);
            var material = Art.Load<Material>("Mobs/" + name);
            art = null;
            if (model != null && material != null) art = new MobArt { Model = model, Material = material, Height = -1f };
            MobArts[name] = art;
            return art;
        }

        private void Float(string content, Vector3 position, Color color, float scale)
        {
            if (_texts.Count >= MaxFloatingTexts) return;

            var go = new GameObject("FloatingText");
            go.transform.SetParent(transform, false);
            go.transform.position = position + new Vector3(Random.Range(-0.2f, 0.2f), 0f, -0.6f);
            go.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Ui.Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            mesh.text = Loc.T(content);
            mesh.fontSize = 64;
            mesh.characterSize = 0.05f * scale;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = color;

            _texts.Add(new FloatingText { Mesh = mesh });
        }

        private static GameObject KorstoneModel()
        {
            if (!_korstoneLoaded)
            {
                _korstoneLoaded = true;
                _korstoneModel = Art.Load<GameObject>("Models/Korstone");
                _korstoneMaterial = Art.Load<Material>("KorstoneEmber");
                if (_korstoneModel == null || _korstoneMaterial == null) _korstoneModel = null;
            }
            return _korstoneModel;
        }

        /// <summary>Swaps a renderer onto an instance of a Resources material, keeping the tint.</summary>
        private static void UseMaterial(Renderer renderer, string resource, Color color)
        {
            var shared = Art.Load<Material>(resource);
            if (shared == null) return;
            renderer.sharedMaterial = shared;
            renderer.material.color = color;
        }

        private static Transform Primitive(PrimitiveType type, string name, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (Application.isPlaying) Destroy(go.GetComponent<Collider>());
            else DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            _greyBox ??= Art.Load<Material>("GreyBox");
            if (_greyBox != null) renderer.sharedMaterial = _greyBox;
            renderer.material.color = color;
            return go.transform;
        }
    }
}
