using System.Collections.Generic;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Lane presentation: capsule hero with a glaive on the ember-glow material, cube mobs, and the Korstone model
    /// from Blender (Resources/Models/Korstone) with lit cracks. Falls back to a dark box if the model is missing.
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
        }

        private sealed class FloatingText
        {
            public TextMesh Mesh;
            public float Age;
        }

        private readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        private readonly List<FloatingText> _texts = new List<FloatingText>();
        private readonly List<Transform> _stripes = new List<Transform>();

        private LaneSim _sim;
        private Transform _hero;
        private Renderer _heroRenderer;
        private Renderer _weaponRenderer;
        private float _armorGlow = -1f;
        private float _weaponGlow = -1f;
        private float _heroPunch;
        private float _heroHurt;

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
            foreach (EnemyView view in _views.Values) Destroy(view.Root.gameObject);
            _views.Clear();
            _sim = sim;
            _hero.rotation = Quaternion.identity;
            _hero.position = new Vector3(HeroX, 1f, 0f);
            foreach (Enemy enemy in sim.Enemies) SpawnView(enemy.Id);
        }

        public void Init(LaneSim sim)
        {
            _sim = sim;

            Transform ground = Primitive(PrimitiveType.Cube, "Ground", new Color(0.30f, 0.34f, 0.26f));
            ground.position = new Vector3(3f, -0.25f, 1f);
            ground.localScale = new Vector3(40f, 0.5f, 8f);

            for (int i = 0; i < 10; i++)
            {
                Transform stripe = Primitive(PrimitiveType.Cube, "Stripe", new Color(0.36f, 0.40f, 0.30f));
                stripe.localScale = new Vector3(0.25f, 0.02f, 8f);
                stripe.position = new Vector3(-8f + i * 2.4f, 0.01f, 1f);
                _stripes.Add(stripe);
            }

            _hero = Primitive(PrimitiveType.Capsule, "Hero", HeroColor);
            _hero.position = new Vector3(HeroX, 1f, 0f);
            _heroRenderer = _hero.GetComponent<Renderer>();
            UseMaterial(_heroRenderer, "EmberGear", HeroColor);

            // Glaive: a pole and blade in the right hand, glowing on its own (hotter) material.
            Transform glaive = Primitive(PrimitiveType.Cube, "Glaive", new Color(0.62f, 0.62f, 0.66f));
            glaive.SetParent(_hero, false);
            glaive.localPosition = new Vector3(0.62f, 0.35f, -0.25f);
            glaive.localScale = new Vector3(0.07f, 2.1f, 0.07f);
            glaive.localRotation = Quaternion.Euler(0f, 0f, -8f);
            _weaponRenderer = glaive.GetComponent<Renderer>();
            UseMaterial(_weaponRenderer, "EmberWeapon", new Color(0.62f, 0.62f, 0.66f));
        }

        /// <summary>Upgrade glow for the hero: armor = average of the non-weapon slots, weapon on its own.</summary>
        public void SetGear(float armorGlow, float weaponGlow)
        {
            if (!Mathf.Approximately(armorGlow, _armorGlow)) { _armorGlow = armorGlow; _heroRenderer.material.SetFloat(UpgradeGlow.GlowId, armorGlow); }
            if (!Mathf.Approximately(weaponGlow, _weaponGlow)) { _weaponGlow = weaponGlow; _weaponRenderer.material.SetFloat(UpgradeGlow.GlowId, weaponGlow); }
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
                    if (_views.TryGetValue(e.EnemyId, out EnemyView hit))
                    {
                        hit.Punch = 1f;
                        Float(e.Amount.ToString(), hit.Root.position + Vector3.up * 1.2f, e.Crit ? Palette.Warn : Color.white, e.Crit ? 1.5f : 1f);
                    }
                    break;

                case LaneEventKind.EnemyDied:
                    if (_views.TryGetValue(e.EnemyId, out EnemyView dead))
                    {
                        Destroy(dead.Root.gameObject);
                        _views.Remove(e.EnemyId);
                    }
                    break;

                case LaneEventKind.KorstoneWave:
                    Float("WAVE " + e.Amount, new Vector3(3.4f, 3.4f, 0f), Palette.Bad, 1.8f);
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
                    break;

                case LaneEventKind.HeroHealed:
                    Float("+" + e.Amount, _hero.position + Vector3.up * 1.4f, Palette.Good, 1.2f);
                    break;

                case LaneEventKind.HeroDied:
                    foreach (EnemyView view in _views.Values) Destroy(view.Root.gameObject);
                    _views.Clear();
                    _hero.rotation = Quaternion.Euler(0f, 0f, 90f);
                    _hero.position = new Vector3(HeroX, 0.5f, 0f);
                    Float("DEFEATED", _hero.position + Vector3.up * 2f, Palette.Bad, 2f);
                    break;

                case LaneEventKind.HeroRespawned:
                    _hero.rotation = Quaternion.identity;
                    _hero.position = new Vector3(HeroX, 1f, 0f);
                    break;

                case LaneEventKind.SkillCast:
                    Float(e.Text, _hero.position + Vector3.up * 1.9f, new Color(0.6f, 0.85f, 1f), 1.3f);
                    break;
            }
        }

        private void Update()
        {
            if (_sim == null) return;
            float dt = Time.deltaTime;

            if (_sim.Phase == LanePhase.Running)
            {
                foreach (Transform stripe in _stripes)
                {
                    Vector3 p = stripe.position;
                    p.x -= 6f * dt;
                    if (p.x < -9f) p.x += 24f;
                    stripe.position = p;
                }
            }

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
                    target = new Vector3(1.2f, 1.2f, 0f);
                }
                else
                {
                    int row = mobIndex / MobsPerRow;
                    target = new Vector3(-0.4f + (mobIndex % MobsPerRow) * MobSpacing, 0.35f, row * 0.9f);
                    mobIndex++;
                }

                view.Root.position = Vector3.Lerp(view.Root.position, target, 1f - Mathf.Exp(-9f * dt));
                view.Punch = Mathf.MoveTowards(view.Punch, 0f, dt * 6f);
                view.Root.localScale = view.Scale * (1f + view.Punch * 0.18f);

                float ratio = Mathf.Clamp01(enemy.Hp / (float)enemy.MaxHp);
                view.HpFill.localScale = new Vector3(ratio, 1f, 1f);
                view.HpFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, 0f, -0.01f);
            }

            _heroPunch = Mathf.MoveTowards(_heroPunch, 0f, dt * 8f);
            _heroHurt = Mathf.MoveTowards(_heroHurt, 0f, dt * 5f);
            if (_sim.Phase != LanePhase.Dead)
                _hero.position = new Vector3(HeroX + _heroPunch * 0.35f, 1f + (_sim.Phase == LanePhase.Running ? Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.12f : 0f), 0f);
            Color heroBase = _sim.HasteActive ? new Color(1f, 0.55f, 0.2f) : HeroColor;
            _heroRenderer.material.color = Color.Lerp(heroBase, Color.red, _heroHurt * 0.7f);

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
            Transform root = model != null ? Instantiate(model).transform : Primitive(boss ? PrimitiveType.Capsule : PrimitiveType.Cube, kind.ToString(), color);
            root.name = kind.ToString();
            if (model != null)
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _korstoneMaterial;
            Vector3 s = model != null ? Vector3.one : BaseScale(korstone, boss);
            if (kind == EnemyKind.ElderKorstone) s *= 1.3f;
            root.SetParent(transform, false);
            root.position = new Vector3(SpawnX, korstone ? (model != null ? 0f : 1.3f) : boss ? 1.2f : 0.35f, korstone ? 2.2f : 0f);
            root.localScale = s;
            if (korstone) root.rotation = Quaternion.Euler(0f, 25f, model != null ? 0f : 4f);

            // Bars are parented to a holder that cancels the body's scale, so they keep a fixed size.
            var holder = new GameObject("HpBar").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0f, model != null ? 2.9f : 0.75f, 0f);
            holder.localScale = new Vector3((korstone || boss ? 1.6f : 0.7f) / s.x, 0.09f / s.y, 0.05f / s.z);
            holder.rotation = Quaternion.identity;

            Transform back = Primitive(PrimitiveType.Cube, "Back", new Color(0.05f, 0.05f, 0.05f));
            back.SetParent(holder, false);
            Transform fill = Primitive(PrimitiveType.Cube, "Fill", korstone ? Palette.Warn : boss ? Palette.Bad : Palette.Good);
            fill.SetParent(holder, false);
            fill.localPosition = new Vector3(0f, 0f, -0.01f);

            _views[enemyId] = new EnemyView { Root = root, HpFill = fill, Scale = s, IsModel = model != null };
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
            mesh.text = content;
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
                _korstoneModel = Resources.Load<GameObject>("Models/Korstone");
                _korstoneMaterial = Resources.Load<Material>("KorstoneEmber");
                if (_korstoneModel == null || _korstoneMaterial == null) _korstoneModel = null;
            }
            return _korstoneModel;
        }

        /// <summary>Swaps a renderer onto an instance of a Resources material, keeping the tint.</summary>
        private static void UseMaterial(Renderer renderer, string resource, Color color)
        {
            var shared = Resources.Load<Material>(resource);
            if (shared == null) return;
            renderer.sharedMaterial = shared;
            renderer.material.color = color;
        }

        private static Transform Primitive(PrimitiveType type, string name, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            _greyBox ??= Resources.Load<Material>("GreyBox");
            if (_greyBox != null) renderer.sharedMaterial = _greyBox;
            renderer.material.color = color;
            return go.transform;
        }
    }
}
