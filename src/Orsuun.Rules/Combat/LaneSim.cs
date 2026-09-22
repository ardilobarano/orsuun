#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules.Combat
{
    public sealed class HeroStats
    {
        public long MaxHp { get; set; } = 2000;
        public long Attack { get; set; } = 80;
        public int AttackIntervalTicks { get; set; } = 12;
        public int CritChanceBp { get; set; } = 500;
        public int CritMultiplierPercent { get; set; } = 200;
    }

    public enum SkillKind
    {
        /// <summary>One heavy hit on the toughest enemy (the Korstone when one is up).</summary>
        Burst,
        /// <summary>Hits every enemy.</summary>
        Area,
        /// <summary>Halves the attack interval for a while.</summary>
        Haste,
    }

    public sealed class SkillDef
    {
        public SkillDef(string name, SkillKind kind, int cooldownTicks, int powerPercent, int durationTicks = 0)
        {
            Name = name;
            Kind = kind;
            CooldownTicks = cooldownTicks;
            PowerPercent = powerPercent;
            DurationTicks = durationTicks;
        }

        public string Name { get; }
        public SkillKind Kind { get; }
        public int CooldownTicks { get; }
        public int PowerPercent { get; }
        public int DurationTicks { get; }

        /// <summary>Grey-box kit of the Vanguard, Wrath branch.</summary>
        public static SkillDef[] VanguardWrath() => new[]
        {
            new SkillDef("Rending Arc", SkillKind.Burst, 6 * LaneSim.TicksPerSecond, 400),
            new SkillDef("Iron Whirl", SkillKind.Area, 9 * LaneSim.TicksPerSecond, 180),
            new SkillDef("Blood Fury", SkillKind.Haste, 15 * LaneSim.TicksPerSecond, 0, 5 * LaneSim.TicksPerSecond),
        };
    }

    public sealed class StageConfig
    {
        public int PacksBeforeKorstone { get; set; } = 5;
        public int PackSizeMin { get; set; } = 4;
        public int PackSizeMax { get; set; } = 8;
        public long MobHp { get; set; } = 250;
        public long MobAttack { get; set; } = 45;
        public int MobAttackIntervalTicks { get; set; } = 20;
        public long KorstoneHp { get; set; } = 4000;
        public int WaveSize { get; set; } = 3;
        public int RunTicks { get; set; } = 30;
        public int DeathTicks { get; set; } = 60;
        public long SornPerMob { get; set; } = 150;
    }

    public sealed class Enemy
    {
        internal Enemy(int id, bool isKorstone, long hp, long attack)
        {
            Id = id;
            IsKorstone = isKorstone;
            MaxHp = hp;
            Hp = hp;
            Attack = attack;
        }

        public int Id { get; }
        public bool IsKorstone { get; }
        public long MaxHp { get; }
        public long Hp { get; internal set; }
        public long Attack { get; }
        internal int NextAttackTick { get; set; }
        internal int WavesSpawned { get; set; }
    }

    public enum LanePhase
    {
        Running,
        Fighting,
        Dead,
    }

    public enum LaneEventKind
    {
        EnemySpawned,
        EnemyDamaged,
        EnemyDied,
        KorstoneWave,
        HeroDamaged,
        HeroHealed,
        HeroDied,
        HeroRespawned,
        SkillCast,
        Loot,
        EncounterCleared,
    }

    public readonly struct LaneEvent
    {
        public LaneEvent(LaneEventKind kind, int enemyId = 0, long amount = 0, bool crit = false, string? text = null)
        {
            Kind = kind;
            EnemyId = enemyId;
            Amount = amount;
            Crit = crit;
            Text = text;
        }

        public LaneEventKind Kind { get; }
        public int EnemyId { get; }
        public long Amount { get; }
        public bool Crit { get; }
        public string? Text { get; }
    }

    /// <summary>
    /// Deterministic, tick-based lane combat: packs, then a Korstone with waves, then the loop repeats.
    /// No positions and no floats; the client turns the event stream into animation.
    /// </summary>
    public sealed class LaneSim
    {
        public const int TicksPerSecond = 20;
        private const int KorstoneWaves = 4;
        private const int PotionThresholdPercent = 40;
        private const int PotionHealPercent = 35;
        private const int PotionCooldownTicks = 40;

        private readonly StageConfig _stage;
        private readonly Inventory _inventory;
        private readonly IRandom _rng;
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<LaneEvent> _events = new List<LaneEvent>();
        private readonly int[] _readyAtTick;

        private HeroStats _hero;
        private int _tick;
        private int _phaseTicksLeft;
        private int _heroNextAttackTick;
        private int _hasteUntilTick;
        private int _potionReadyAtTick;
        private int _nextEnemyId = 1;

        public LaneSim(StageConfig stage, HeroStats hero, SkillDef[] skills, Inventory inventory, IRandom rng)
        {
            _stage = stage;
            _hero = hero;
            _inventory = inventory;
            _rng = rng;
            Skills = skills;
            AutoCast = new bool[skills.Length];
            _readyAtTick = new int[skills.Length];
            HeroHp = hero.MaxHp;
            Phase = LanePhase.Running;
            _phaseTicksLeft = stage.RunTicks;
        }

        public SkillDef[] Skills { get; }
        public bool[] AutoCast { get; }
        public LanePhase Phase { get; private set; }
        public long HeroHp { get; private set; }
        public long HeroMaxHp => _hero.MaxHp;
        public IReadOnlyList<Enemy> Enemies => _enemies;
        /// <summary>0..PacksBeforeKorstone-1 are packs, PacksBeforeKorstone is the Korstone.</summary>
        public int EncounterIndex { get; private set; }
        public bool IsKorstoneEncounter => EncounterIndex == _stage.PacksBeforeKorstone;
        public int KorstonesDestroyed { get; private set; }
        public int MobsKilled { get; private set; }
        public int Deaths { get; private set; }
        public bool HasteActive => _tick < _hasteUntilTick;

        /// <summary>Swaps in new stats after a Forge attempt or a turn, keeping the HP ratio.</summary>
        public void SetHero(HeroStats hero)
        {
            HeroHp = Math.Max(1, HeroHp * hero.MaxHp / _hero.MaxHp);
            _hero = hero;
        }

        public int CooldownTicksLeft(int skillIndex) => Math.Max(0, _readyAtTick[skillIndex] - _tick);

        public bool TryCast(int skillIndex)
        {
            if (Phase != LanePhase.Fighting || CooldownTicksLeft(skillIndex) > 0) return false;

            SkillDef skill = Skills[skillIndex];
            _readyAtTick[skillIndex] = _tick + skill.CooldownTicks;
            _events.Add(new LaneEvent(LaneEventKind.SkillCast, amount: skillIndex, text: skill.Name));

            switch (skill.Kind)
            {
                case SkillKind.Burst:
                    Enemy? toughest = null;
                    foreach (Enemy e in _enemies)
                        if (toughest == null || e.Hp > toughest.Hp) toughest = e;
                    if (toughest != null) Hit(toughest, skill.PowerPercent);
                    break;

                case SkillKind.Area:
                    foreach (Enemy e in _enemies.ToArray())
                        if (e.Hp > 0) Hit(e, skill.PowerPercent);
                    break;

                case SkillKind.Haste:
                    _hasteUntilTick = _tick + skill.DurationTicks;
                    break;
            }

            return true;
        }

        public void Tick()
        {
            _tick++;
            switch (Phase)
            {
                case LanePhase.Running:
                    HeroHp = Math.Min(_hero.MaxHp, HeroHp + Math.Max(1, _hero.MaxHp / 100));
                    if (--_phaseTicksLeft <= 0) SpawnEncounter();
                    break;

                case LanePhase.Dead:
                    if (--_phaseTicksLeft <= 0)
                    {
                        HeroHp = _hero.MaxHp;
                        EncounterIndex = 0;
                        StartRunning();
                        _events.Add(new LaneEvent(LaneEventKind.HeroRespawned));
                    }
                    break;

                case LanePhase.Fighting:
                    FightTick();
                    break;
            }
        }

        /// <summary>Returns everything that happened since the last drain. Call once per tick or per frame.</summary>
        public List<LaneEvent> DrainEvents()
        {
            var copy = new List<LaneEvent>(_events);
            _events.Clear();
            return copy;
        }

        private void FightTick()
        {
            for (int i = 0; i < Skills.Length; i++)
                if (AutoCast[i]) TryCast(i);

            if (_tick >= _heroNextAttackTick && _enemies.Count > 0)
            {
                Enemy target = _enemies[0];
                foreach (Enemy e in _enemies)
                    if (!e.IsKorstone) { target = e; break; }

                Hit(target, 100);
                int interval = HasteActive ? Math.Max(1, _hero.AttackIntervalTicks / 2) : _hero.AttackIntervalTicks;
                _heroNextAttackTick = _tick + interval;
            }

            foreach (Enemy e in _enemies)
            {
                if (e.IsKorstone || _tick < e.NextAttackTick) continue;
                e.NextAttackTick = _tick + _stage.MobAttackIntervalTicks;
                HeroHp -= e.Attack;
                _events.Add(new LaneEvent(LaneEventKind.HeroDamaged, e.Id, e.Attack));
                if (HeroHp <= 0) break;
            }

            if (HeroHp <= 0)
            {
                HeroHp = 0;
                Deaths++;
                _enemies.Clear();
                Phase = LanePhase.Dead;
                _phaseTicksLeft = _stage.DeathTicks;
                _events.Add(new LaneEvent(LaneEventKind.HeroDied));
                return;
            }

            if (HeroHp * 100 < _hero.MaxHp * PotionThresholdPercent && _inventory.Potions > 0 && _tick >= _potionReadyAtTick)
            {
                _inventory.Potions--;
                _potionReadyAtTick = _tick + PotionCooldownTicks;
                long heal = _hero.MaxHp * PotionHealPercent / 100;
                HeroHp = Math.Min(_hero.MaxHp, HeroHp + heal);
                _events.Add(new LaneEvent(LaneEventKind.HeroHealed, amount: heal));
            }

            if (_enemies.Count == 0)
            {
                _events.Add(new LaneEvent(LaneEventKind.EncounterCleared));
                EncounterIndex = (EncounterIndex + 1) % (_stage.PacksBeforeKorstone + 1);
                StartRunning();
            }
        }

        private void StartRunning()
        {
            Phase = LanePhase.Running;
            _phaseTicksLeft = _stage.RunTicks;
        }

        private void SpawnEncounter()
        {
            Phase = LanePhase.Fighting;
            _heroNextAttackTick = _tick + 4;

            if (IsKorstoneEncounter)
            {
                Spawn(new Enemy(_nextEnemyId++, true, _stage.KorstoneHp, 0));
                return;
            }

            int size = _stage.PackSizeMin + _rng.NextInt(_stage.PackSizeMax - _stage.PackSizeMin + 1);
            for (int i = 0; i < size; i++) SpawnMob();
        }

        private void SpawnMob()
        {
            var mob = new Enemy(_nextEnemyId++, false, _stage.MobHp, _stage.MobAttack);
            // Stagger first hits so a pack does not land all its damage on one tick.
            mob.NextAttackTick = _tick + 10 + _rng.NextInt(_stage.MobAttackIntervalTicks);
            Spawn(mob);
        }

        private void Spawn(Enemy enemy)
        {
            _enemies.Add(enemy);
            _events.Add(new LaneEvent(LaneEventKind.EnemySpawned, enemy.Id));
        }

        private void Hit(Enemy enemy, int powerPercent)
        {
            bool crit = _rng.RollBp(_hero.CritChanceBp);
            long damage = _hero.Attack * powerPercent / 100;
            if (crit) damage = damage * _hero.CritMultiplierPercent / 100;
            damage = Math.Max(1, damage * (90 + _rng.NextInt(21)) / 100);

            enemy.Hp -= damage;
            _events.Add(new LaneEvent(LaneEventKind.EnemyDamaged, enemy.Id, damage, crit));

            if (enemy.Hp <= 0)
            {
                _enemies.Remove(enemy);
                _events.Add(new LaneEvent(LaneEventKind.EnemyDied, enemy.Id));
                if (enemy.IsKorstone) LootKorstone(); else LootMob();
                return;
            }

            // A Korstone calls a wave each time it loses another 20% of its health.
            while (enemy.IsKorstone && enemy.WavesSpawned < KorstoneWaves
                   && enemy.Hp * 100 <= enemy.MaxHp * (100 - 20 * (enemy.WavesSpawned + 1)))
            {
                enemy.WavesSpawned++;
                _events.Add(new LaneEvent(LaneEventKind.KorstoneWave, enemy.Id, enemy.WavesSpawned));
                for (int i = 0; i < _stage.WaveSize; i++) SpawnMob();
            }
        }

        private void LootMob()
        {
            MobsKilled++;
            HuntYield.LootMob(_stage, _inventory, _rng, out string? drop);
            if (drop != null) _events.Add(new LaneEvent(LaneEventKind.Loot, text: drop));
        }

        private void LootKorstone()
        {
            KorstonesDestroyed++;
            _events.Add(new LaneEvent(LaneEventKind.Loot, text: HuntYield.LootKorstone(_stage, _inventory, _rng)));
        }
    }
}
