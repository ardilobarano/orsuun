using System.Collections.Generic;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>Grey-box presentation of the lane: capsule hero, cube mobs, a tall dark Korstone.</summary>
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
        private float _heroPunch;
        private float _heroHurt;

        private static Material _greyBox;

        private static readonly Color HeroColor = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color MobColor = new Color(0.75f, 0.25f, 0.22f);
        private static readonly Color KorstoneColor = new Color(0.12f, 0.10f, 0.12f);

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
                    target = new Vector3(3.4f, 1.3f, 2.2f);
                }
                else
                {
                    int row = mobIndex / MobsPerRow;
                    target = new Vector3(-0.4f + (mobIndex % MobsPerRow) * MobSpacing, 0.35f, row * 0.9f);
                    mobIndex++;
                }

                view.Root.position = Vector3.Lerp(view.Root.position, target, 1f - Mathf.Exp(-9f * dt));
                view.Punch = Mathf.MoveTowards(view.Punch, 0f, dt * 6f);
                view.Root.localScale = BaseScale(enemy.IsKorstone) * (1f + view.Punch * 0.18f);

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

        private static Vector3 BaseScale(bool korstone) => korstone ? new Vector3(1.3f, 2.6f, 1.3f) : new Vector3(0.6f, 0.7f, 0.6f);

        private void SpawnView(int enemyId)
        {
            bool korstone = false;
            foreach (Enemy enemy in _sim.Enemies)
                if (enemy.Id == enemyId) korstone = enemy.IsKorstone;

            Transform root = Primitive(PrimitiveType.Cube, korstone ? "Korstone" : "Mob", korstone ? KorstoneColor : MobColor);
            root.SetParent(transform, false);
            root.position = new Vector3(SpawnX, korstone ? 1.3f : 0.35f, korstone ? 2.2f : 0f);
            root.localScale = BaseScale(korstone);
            if (korstone) root.rotation = Quaternion.Euler(0f, 25f, 4f);

            // Bars are parented to a holder that cancels the body's scale, so they keep a fixed size.
            var holder = new GameObject("HpBar").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0f, 0.75f, 0f);
            Vector3 s = BaseScale(korstone);
            holder.localScale = new Vector3((korstone ? 1.6f : 0.7f) / s.x, 0.09f / s.y, 0.05f / s.z);
            holder.rotation = Quaternion.identity;

            Transform back = Primitive(PrimitiveType.Cube, "Back", new Color(0.05f, 0.05f, 0.05f));
            back.SetParent(holder, false);
            Transform fill = Primitive(PrimitiveType.Cube, "Fill", korstone ? Palette.Warn : Palette.Good);
            fill.SetParent(holder, false);
            fill.localPosition = new Vector3(0f, 0f, -0.01f);

            _views[enemyId] = new EnemyView { Root = root, HpFill = fill };
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
