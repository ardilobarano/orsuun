#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules.Combat
{
    public sealed class HeroStats
    {
        public long MaxHp { get; set; } = 2000;
        public long Attack { get; set; } = 80;
        /// <summary>Flat reduction of every enemy hit, never below 1 damage.</summary>
        public long Defense { get; set; }
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

    public enum FinalEncounter
    {
        /// <summary>Packs only, no final encounter: Hunting Grounds.</summary>
        None,
        Korstone,
        Boss,
    }

    public enum ZoneType
    {
        Campaign,
        HuntingGround,
        KorstoneField,
        CommanderGround,
    }

    /// <summary>The one thing each Commander does that gear or a class must answer (GDD section 13).</summary>
    public enum BossMechanic
    {
        None,
        /// <summary>Takes no damage while any captain lives; +50% attack below 30% HP.</summary>
        CaptainShield,
        /// <summary>At 70% and 40% HP spawns two images that draw attacks and reflect 20% of the damage they take.</summary>
        MirrorImages,
        /// <summary>Calls a pack of Hollowed every 15 seconds.</summary>
        PackCaller,
    }

    public sealed class StageConfig
    {
        public int StageNumber { get; set; } = 1;
        public ZoneType Zone { get; set; } = ZoneType.Campaign;
        public FinalEncounter FinalEncounter { get; set; } = FinalEncounter.Korstone;
        public long BossHp { get; set; } = 9000;
        public long BossAttack { get; set; } = 110;
        public string BossName { get; set; } = "Old Greyjaw";
        public BossMechanic BossMechanic { get; set; } = BossMechanic.None;
        public string MaterialName { get; set; } = "Wolf Sinew";
        public int GearItemLevel { get; set; } = 1;
        /// <summary>Highest rarity this run may drop: Rare offline, Epic online.</summary>
        public Rarity GearRarityCap { get; set; } = Rarity.Epic;
        /// <summary>Korshard rank dropped by this zone's Korstones, 0 = Trooper .. 4 = Guard of the Khan.</summary>
        public int KorshardRank { get; set; }
        /// <summary>Every Nth Korstone is an Elder: 5x HP, waves of 6, a guaranteed Scroll of Mercy. 0 = never.</summary>
        public int ElderEvery { get; set; }
        /// <summary>Korstone Fields pay materials, Turnstones and Needles at this share of the base rate. 100 = campaign.</summary>
        public int MaterialYieldPercent { get; set; } = 100;
        public int PacksBeforeKorstone { get; set; } = 5;
        public int PackSizeMin { get; set; } = 4;
        public int PackSizeMax { get; set; } = 8;
        public long MobHp { get; set; } = 250;
        public long MobAttack { get; set; } = 45;
        public int MobAttackIntervalTicks { get; set; } = 20;
        public int MobRespawnTicks { get; set; } = 30;
        public long KorstoneHp { get; set; } = 4000;
        public int WaveSize { get; set; } = 3;
        public int RunTicks { get; set; } = 30;
        public int DeathTicks { get; set; } = 60;
        public long SornPerMob { get; set; } = 150;
        public long XpPerMob { get; set; } = 10;
    }

    public enum EnemyKind
    {
        Mob,
        Korstone,
        ElderKorstone,
        Boss,
        /// <summary>Tul-Gorak's guards: the boss is untouchable while one stands.</summary>
        Captain,
        /// <summary>The Mirage Queen's false images: they draw plain attacks and reflect damage.</summary>
        Image,
    }

    public sealed class Enemy
    {
        internal Enemy(int id, EnemyKind kind, long hp, long attack)
        {
            Id = id;
            Kind = kind;
            MaxHp = hp;
            Hp = hp;
            Attack = attack;
        }

        public int Id { get; }
        public EnemyKind Kind { get; }
        public bool IsKorstone => Kind == EnemyKind.Korstone || Kind == EnemyKind.ElderKorstone;
        public bool IsBoss => Kind == EnemyKind.Boss;
        public long MaxHp { get; }
        public long Hp { get; internal set; }
        public long Attack { get; internal set; }
        internal int NextAttackTick { get; set; }
        internal int WavesSpawned { get; set; }
        internal bool Enraged { get; set; }
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
        /// <summary>A hit was absorbed by a boss mechanic; Text says which.</summary>
        Shielded,
        /// <summary>A boss mechanic fired: images, enrage, pack call.</summary>
        BossMechanic,
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
    /// Deterministic, tick-based lane combat: packs, then a Korstone with waves (or a boss), then the loop repeats.
    /// No positions and no floats; the client turns the event stream into animation.
    /// </summary>
    public sealed class LaneSim
    {
        public const int TicksPerSecond = 20;
        private const int KorstoneWaves = 4;
        private const int PotionThresholdPercent = 40;
        private const int PotionHealPercent = 35;
        private const int PotionCooldownTicks = 40;
        private const int ImageReflectPercent = 20;
        private const int PackCallTicks = 15 * TicksPerSecond;

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
        private int _nextPackCallTick;
        private int _imagesSpawned;

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
        /// <summary>0..PacksBeforeKorstone-1 are packs, PacksBeforeKorstone is the final encounter.</summary>
        public int EncounterIndex { get; private set; }
        public bool IsKorstoneEncounter => _stage.FinalEncounter != FinalEncounter.None && EncounterIndex == _stage.PacksBeforeKorstone;
        public int KorstonesDestroyed { get; private set; }
        public int BossesKilled { get; private set; }
        public int MobsKilled { get; private set; }
        public int Deaths { get; private set; }
        public StageConfig Stage => _stage;
        /// <summary>Completed loops of the stage (final encounter cleared).</summary>
        public int Clears { get; private set; }
        /// <summary>True when the current final encounter is the stage boss rather than a Korstone.</summary>
        public bool IsBossEncounter => IsKorstoneEncounter && _stage.FinalEncounter == FinalEncounter.Boss;
        public bool IsElderNext => _stage.ElderEvery > 0 && (KorstonesDestroyed + 1) % _stage.ElderEvery == 0;
        public bool HasteActive => _tick < _hasteUntilTick;
        /// <summary>Damage landed on bosses, for Commander damage brackets.</summary>
        public long BossDamageDealt { get; private set; }

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
                // Plain attacks take the first thing in the way: images and captains sit in front of their boss.
                Enemy target = _enemies[0];
                foreach (Enemy e in _enemies)
                    if (!e.IsKorstone) { target = e; break; }

                Hit(target, 100);
                int interval = HasteActive ? Math.Max(1, _hero.AttackIntervalTicks / 2) : _hero.AttackIntervalTicks;
                _heroNextAttackTick = _tick + interval;
            }

            Enemy? boss = null;
            foreach (Enemy e in _enemies)
            {
                if (e.IsBoss) boss = e;
                if (e.IsKorstone || _tick < e.NextAttackTick) continue;
                e.NextAttackTick = _tick + _stage.MobAttackIntervalTicks;
                long damage = Math.Max(1, e.Attack - _hero.Defense);
                HeroHp -= damage;
                _events.Add(new LaneEvent(LaneEventKind.HeroDamaged, e.Id, damage));
                if (HeroHp <= 0) break;
            }

            if (boss != null && _stage.BossMechanic == BossMechanic.PackCaller && _tick >= _nextPackCallTick)
            {
                _nextPackCallTick = _tick + PackCallTicks;
                _events.Add(new LaneEvent(LaneEventKind.BossMechanic, boss.Id, text: _stage.BossName + " calls a pack"));
                for (int i = 0; i < 3; i++) SpawnMob();
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
                if (IsKorstoneEncounter) Clears++;
                int loop = _stage.FinalEncounter == FinalEncounter.None ? _stage.PacksBeforeKorstone : _stage.PacksBeforeKorstone + 1;
                EncounterIndex = (EncounterIndex + 1) % Math.Max(1, loop);
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

            if (IsBossEncounter)
            {
                var boss = new Enemy(_nextEnemyId++, EnemyKind.Boss, _stage.BossHp, _stage.BossAttack);
                boss.NextAttackTick = _tick + 15;
                if (_stage.BossMechanic == BossMechanic.CaptainShield)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var captain = new Enemy(_nextEnemyId++, EnemyKind.Captain, _stage.MobHp * 6, _stage.MobAttack);
                        captain.NextAttackTick = _tick + 10 + i * 5;
                        Spawn(captain);
                    }
                }
                Spawn(boss);
                _nextPackCallTick = _tick + PackCallTicks;
                _imagesSpawned = 0;
                return;
            }

            if (IsKorstoneEncounter)
            {
                bool elder = IsElderNext;
                Spawn(new Enemy(_nextEnemyId++, elder ? EnemyKind.ElderKorstone : EnemyKind.Korstone, elder ? _stage.KorstoneHp * 5 : _stage.KorstoneHp, 0));
                return;
            }

            int size = _stage.PackSizeMin + _rng.NextInt(_stage.PackSizeMax - _stage.PackSizeMin + 1);
            for (int i = 0; i < size; i++) SpawnMob();
        }

        private void SpawnMob()
        {
            var mob = new Enemy(_nextEnemyId++, EnemyKind.Mob, _stage.MobHp, _stage.MobAttack);
            // Stagger first hits so a pack does not land all its damage on one tick.
            mob.NextAttackTick = _tick + 10 + _rng.NextInt(_stage.MobAttackIntervalTicks);
            Spawn(mob);
        }

        private void Spawn(Enemy enemy, bool inFront = false)
        {
            if (inFront) _enemies.Insert(0, enemy); else _enemies.Add(enemy);
            _events.Add(new LaneEvent(LaneEventKind.EnemySpawned, enemy.Id));
        }

        private void Hit(Enemy enemy, int powerPercent)
        {
            if (enemy.IsBoss && _stage.BossMechanic == BossMechanic.CaptainShield)
            {
                foreach (Enemy e in _enemies)
                {
                    if (e.Kind != EnemyKind.Captain) continue;
                    _events.Add(new LaneEvent(LaneEventKind.Shielded, enemy.Id, text: "Shielded by captains"));
                    return;
                }
            }

            bool crit = _rng.RollBp(_hero.CritChanceBp);
            long damage = _hero.Attack * powerPercent / 100;
            if (crit) damage = damage * _hero.CritMultiplierPercent / 100;
            damage = Math.Max(1, damage * (90 + _rng.NextInt(21)) / 100);
            damage = Math.Min(damage, enemy.Hp);

            enemy.Hp -= damage;
            if (enemy.IsBoss) BossDamageDealt += damage;
            _events.Add(new LaneEvent(LaneEventKind.EnemyDamaged, enemy.Id, damage, crit));

            if (enemy.Kind == EnemyKind.Image)
            {
                long reflected = Math.Max(1, damage * ImageReflectPercent / 100);
                HeroHp -= reflected;
                _events.Add(new LaneEvent(LaneEventKind.HeroDamaged, enemy.Id, reflected));
            }

            if (enemy.Hp <= 0)
            {
                _enemies.Remove(enemy);
                _events.Add(new LaneEvent(LaneEventKind.EnemyDied, enemy.Id));
                switch (enemy.Kind)
                {
                    case EnemyKind.Korstone: LootKorstone(false); break;
                    case EnemyKind.ElderKorstone: LootKorstone(true); break;
                    case EnemyKind.Boss: LootBoss(); break;
                    case EnemyKind.Mob: LootMob(); break;
                }
                return;
            }

            if (enemy.IsBoss) BossThresholds(enemy);

            // A Korstone calls a wave each time it loses another 20% of its health.
            int waveSize = enemy.Kind == EnemyKind.ElderKorstone ? _stage.WaveSize * 2 : _stage.WaveSize;
            while (enemy.IsKorstone && enemy.WavesSpawned < KorstoneWaves
                   && enemy.Hp * 100 <= enemy.MaxHp * (100 - 20 * (enemy.WavesSpawned + 1)))
            {
                enemy.WavesSpawned++;
                _events.Add(new LaneEvent(LaneEventKind.KorstoneWave, enemy.Id, enemy.WavesSpawned));
                for (int i = 0; i < waveSize; i++) SpawnMob();
            }
        }

        private void BossThresholds(Enemy boss)
        {
            switch (_stage.BossMechanic)
            {
                case BossMechanic.CaptainShield:
                    if (!boss.Enraged && boss.Hp * 100 <= boss.MaxHp * 30)
                    {
                        boss.Enraged = true;
                        boss.Attack = boss.Attack * 3 / 2;
                        _events.Add(new LaneEvent(LaneEventKind.BossMechanic, boss.Id, text: _stage.BossName + " enrages"));
                    }
                    break;

                case BossMechanic.MirrorImages:
                    int threshold = _imagesSpawned == 0 ? 70 : _imagesSpawned == 1 ? 40 : -1;
                    if (threshold > 0 && boss.Hp * 100 <= boss.MaxHp * threshold)
                    {
                        _imagesSpawned++;
                        _events.Add(new LaneEvent(LaneEventKind.BossMechanic, boss.Id, text: _stage.BossName + " splits into images"));
                        for (int i = 0; i < 2; i++)
                            Spawn(new Enemy(_nextEnemyId++, EnemyKind.Image, boss.MaxHp * 15 / 100, boss.Attack / 2) { NextAttackTick = _tick + 20 }, inFront: true);
                    }
                    break;
            }
        }

        private void LootMob()
        {
            MobsKilled++;
            HuntYield.LootMob(_stage, _inventory, _rng, out string? drop);
            if (drop != null) _events.Add(new LaneEvent(LaneEventKind.Loot, text: drop));
        }

        private void LootKorstone(bool elder)
        {
            KorstonesDestroyed++;
            _events.Add(new LaneEvent(LaneEventKind.Loot, text: HuntYield.LootKorstone(_stage, _inventory, _rng, elder)));
        }

        private void LootBoss()
        {
            BossesKilled++;
            if (_stage.Zone == ZoneType.CommanderGround) return; // Commander chests are paid by damage bracket, outside the lane.
            _events.Add(new LaneEvent(LaneEventKind.Loot, text: HuntYield.LootBoss(_stage, _inventory, _rng)));
        }
    }
}
