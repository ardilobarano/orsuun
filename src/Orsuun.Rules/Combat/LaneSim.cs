#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules.Combat
{
    /// <summary>Playable classes in the lane (GDD section 4), one branch each for now. All four since 24 Sep 2026.</summary>
    public enum HeroClass
    {
        /// <summary>Wrath: melee bruiser with a glaive.</summary>
        Vanguard = 0,
        /// <summary>Talon: paired knives, best single target.</summary>
        Kestrel = 1,
        /// <summary>Voidpact: ranged caster, highest burst, low defense.</summary>
        Wraithsworn = 2,
        /// <summary>Thunder Rite: storm caller, crit and attack rhythm.</summary>
        Drumcaller = 3,
    }

    public sealed class HeroStats
    {
        /// <summary>The class these stats belong to; it also picks the skill kit (SkillDef.For).</summary>
        public HeroClass Class { get; set; } = HeroClass.Vanguard;
        /// <summary>An aimed Burst on a Korstone or boss hits for this share of its power (LaneSim.AimedWeakPointPercent).</summary>
        public int WeakPointPercent { get; set; } = LaneSim.AimedWeakPointPercent;
        public long MaxHp { get; set; } = 2000;
        public long Attack { get; set; } = 80;
        /// <summary>Flat reduction of every enemy hit, never below 1 damage.</summary>
        public long Defense { get; set; }
        public int AttackIntervalTicks { get; set; } = 12;
        public int CritChanceBp { get; set; } = 500;
        public int CritMultiplierPercent { get; set; } = 200;
        /// <summary>Extra damage against everything that is not a Commander (Beast-Slayer shards).</summary>
        public int BeastDamagePercent { get; set; }
        /// <summary>Chance to take nothing from a hit (Evasion shards).</summary>
        public int EvasionBp { get; set; }
        /// <summary>Damage from Commanders and their images is scaled by this (Warding shards). 100 = full.</summary>
        public int CommanderDamageTakenPercent { get; set; } = 100;
        /// <summary>Skill grades (Rules.SkillGrades) by skill slot: extra power of a burst or area, half as much longer haste.</summary>
        public int[] SkillGradeBonusPercent { get; set; } = new int[SkillGrades.Slots];
        /// <summary>The hero's level: the fourth and fifth skills unlock by it (SkillDef.UnlockLevel).</summary>
        public int Level { get; set; } = 1;
        /// <summary>Riding a wardrobe mount (owner, 26 Sep 2026): plain attacks only, no skills until he dismounts.</summary>
        public bool Mounted { get; set; }
        /// <summary>The weapon's average damage roll (WeaponRolls): plain attacks deal this much more (or less), in percent.</summary>
        public int AverageDamagePercent { get; set; }
        /// <summary>The weapon's skill damage roll: every skill's damage, its poison included, this much more (or less).</summary>
        public int SkillDamagePercent { get; set; }
        /// <summary>The Bannerkin walking behind (Rules.Bannerkin), or null.</summary>
        public KinStats? Kin { get; set; }
    }

    public enum SkillKind
    {
        /// <summary>One heavy hit on the toughest enemy (the Korstone when one is up).</summary>
        Burst,
        /// <summary>Hits every enemy.</summary>
        Area,
        /// <summary>Halves the attack interval for a while.</summary>
        Haste,
        /// <summary>Attack rises by PowerPercent for the duration (Honed Edge).</summary>
        Empower,
        /// <summary>Hits every enemy for PowerPercent, and none of them but a boss strikes for the duration (Bull Rush).</summary>
        Charge,
        /// <summary>Every enemy takes PowerPercent each second for the duration (Venom Cloud).</summary>
        Poison,
        /// <summary>PowerPercent on the toughest (aimed) or the front enemy, doubled below LaneSim.ExecuteBelowPercent health (Shadow Stoop).</summary>
        Execute,
        /// <summary>For the duration enemies (not a boss) strike half as often, and all take PowerPercent more damage (Grave Chains).</summary>
        Bind,
        /// <summary>A veil takes damage up to PowerPercent of max HP for the duration (Shroud of Night).</summary>
        Shield,
        /// <summary>Crit chance rises by PowerPercent points for the duration (Hunter's Blessing).</summary>
        Focus,
        /// <summary>For the duration blows land LaneSim.WardReductionPercent lighter and PowerPercent of each returns to its striker (Mirror Ward).</summary>
        Ward,
    }

    public sealed class SkillDef
    {
        /// <summary>Owner, 26 Sep 2026: five skills a class; the fourth unlocks at level 30, the fifth at level 60.</summary>
        public const int FourthSkillLevel = 30;
        public const int FifthSkillLevel = 60;

        public SkillDef(string name, SkillKind kind, int cooldownTicks, int powerPercent, int durationTicks = 0, int unlockLevel = 1)
        {
            Name = name;
            Kind = kind;
            CooldownTicks = cooldownTicks;
            PowerPercent = powerPercent;
            DurationTicks = durationTicks;
            UnlockLevel = unlockLevel;
        }

        public string Name { get; }
        public SkillKind Kind { get; }
        public int CooldownTicks { get; }
        public int PowerPercent { get; }
        public int DurationTicks { get; }
        /// <summary>The hero level the skill unlocks at (1: from the start).</summary>
        public int UnlockLevel { get; }

        /// <summary>The kit a class fights with.</summary>
        public static SkillDef[] For(HeroClass cls)
        {
            switch (cls)
            {
                case HeroClass.Kestrel: return KestrelTalon();
                case HeroClass.Wraithsworn: return WraithswornVoidpact();
                case HeroClass.Drumcaller: return DrumcallerThunderRite();
                default: return VanguardWrath();
            }
        }

        /// <summary>
        /// Wraithsworn, Voidpact: GDD "ranged caster, highest burst, low defense; top Korstone kill speed with manual
        /// casting". Void Lance is the heaviest and slowest burst in the game and finds weak points hardest.
        /// </summary>
        public static SkillDef[] WraithswornVoidpact() => new[]
        {
            new SkillDef("Void Lance", SkillKind.Burst, 8 * LaneSim.TicksPerSecond, 750),
            new SkillDef("Grave Tide", SkillKind.Area, 10 * LaneSim.TicksPerSecond, 170),
            new SkillDef("Pact Frenzy", SkillKind.Haste, 16 * LaneSim.TicksPerSecond, 0, 5 * LaneSim.TicksPerSecond),
            new SkillDef("Grave Chains", SkillKind.Bind, 16 * LaneSim.TicksPerSecond, 30, 6 * LaneSim.TicksPerSecond, FourthSkillLevel),
            new SkillDef("Shroud of Night", SkillKind.Shield, 20 * LaneSim.TicksPerSecond, 25, 8 * LaneSim.TicksPerSecond, FifthSkillLevel),
        };

        /// <summary>
        /// Drumcaller, Thunder Rite: GDD "offensive buffer: crit, attack". Storm Drum hits the whole pack hard, War
        /// Rhythm is the longest haste, Sky Hammer calls lightning on the toughest foe.
        /// </summary>
        public static SkillDef[] DrumcallerThunderRite() => new[]
        {
            new SkillDef("Sky Hammer", SkillKind.Burst, 7 * LaneSim.TicksPerSecond, 450),
            new SkillDef("Storm Drum", SkillKind.Area, 8 * LaneSim.TicksPerSecond, 200),
            new SkillDef("War Rhythm", SkillKind.Haste, 14 * LaneSim.TicksPerSecond, 0, 7 * LaneSim.TicksPerSecond),
            new SkillDef("Hunter's Blessing", SkillKind.Focus, 18 * LaneSim.TicksPerSecond, 20, 8 * LaneSim.TicksPerSecond, FourthSkillLevel),
            new SkillDef("Mirror Ward", SkillKind.Ward, 20 * LaneSim.TicksPerSecond, 40, 8 * LaneSim.TicksPerSecond, FifthSkillLevel),
        };

        /// <summary>
        /// Kestrel, Talon branch (paired knives): GDD "best single target, fastest bosses". Heartseeker is a bigger
        /// burst than Rending Arc and finds a Korstone's or boss's weak point harder (HeroStats.WeakPointPercent 900);
        /// Knife Fan is a thinner area hit than Iron Whirl. Aimed play pays about 125%, as the Vanguard's does.
        /// </summary>
        public static SkillDef[] KestrelTalon() => new[]
        {
            new SkillDef("Heartseeker", SkillKind.Burst, 6 * LaneSim.TicksPerSecond, 600),
            new SkillDef("Knife Fan", SkillKind.Area, 10 * LaneSim.TicksPerSecond, 130),
            new SkillDef("Kestrel's Dive", SkillKind.Haste, 14 * LaneSim.TicksPerSecond, 0, 5 * LaneSim.TicksPerSecond),
            new SkillDef("Venom Cloud", SkillKind.Poison, 15 * LaneSim.TicksPerSecond, 45, 6 * LaneSim.TicksPerSecond, FourthSkillLevel),
            new SkillDef("Shadow Stoop", SkillKind.Execute, 16 * LaneSim.TicksPerSecond, 500, 0, FifthSkillLevel),
        };

        /// <summary>The Vanguard, Wrath branch (world bible): the three first kit, then Honed Edge and Bull Rush.</summary>
        public static SkillDef[] VanguardWrath() => new[]
        {
            new SkillDef("Rending Arc", SkillKind.Burst, 6 * LaneSim.TicksPerSecond, 400),
            new SkillDef("Iron Whirl", SkillKind.Area, 9 * LaneSim.TicksPerSecond, 180),
            new SkillDef("Blood Fury", SkillKind.Haste, 15 * LaneSim.TicksPerSecond, 0, 5 * LaneSim.TicksPerSecond),
            new SkillDef("Honed Edge", SkillKind.Empower, 20 * LaneSim.TicksPerSecond, 25, 8 * LaneSim.TicksPerSecond, FourthSkillLevel),
            new SkillDef("Bull Rush", SkillKind.Charge, 14 * LaneSim.TicksPerSecond, 180, 2 * LaneSim.TicksPerSecond, FifthSkillLevel),
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
        /// <summary>Evening Bells and events: 100 = normal.</summary>
        public int KorstoneChestPercent { get; set; } = 100;
        public int XpPercent { get; set; } = 100;
        public int MaterialPercent { get; set; } = 100;
        public int DamagePercent { get; set; } = 100;
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
        /// <summary>The Bannerkin cast (Text: Hunter's Blessing or Mending Song; Amount: HP healed).</summary>
        KinCast,
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

        /// <summary>
        /// Weak point (24 Sep 2026): an aimed Rending Arc on a Korstone or boss strikes its crack for 5x its power.
        /// Auto-cast never aims, so only a present player gets it. Measured (ActivePlayTests): aimed play runs at
        /// 100/112/120/126/130/137% of auto-cast pace for 100/200/300/400/500/700%; 500 gives the GDD's ~130%.
        /// </summary>
        public const int AimedWeakPointPercent = 500;

        /// <summary>An Execute (Shadow Stoop) hits twice as hard on an enemy below this share of its health.</summary>
        public const int ExecuteBelowPercent = 30;
        /// <summary>A Ward (Mirror Ward) takes this share off every blow while it stands.</summary>
        public const int WardReductionPercent = 20;

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
        // The fourth and fifth skills (owner, 26 Sep 2026): each a timed state of the lane.
        private int _empowerUntilTick, _empowerPercent;
        private int _focusUntilTick, _focusBp;
        private int _poisonUntilTick, _poisonPercent, _poisonNextTick;
        private int _bindUntilTick, _bindPercent;
        private int _shieldUntilTick;
        private long _shieldHp;
        private int _wardUntilTick, _wardPercent;
        private readonly List<(Enemy Striker, long Amount)> _reflected = new List<(Enemy, long)>();
        private int _potionReadyAtTick;
        private int _kinFocusReadyAtTick, _kinHealReadyAtTick;
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

        /// <summary>Ticks since this lane was created. Online, one lane is one loop, so this is the tick within the loop.</summary>
        public int CurrentTick => _tick;

        public SkillDef[] Skills { get; }
        public bool[] AutoCast { get; }
        /// <summary>The hero fighting this lane (read only: SetHero swaps it).</summary>
        public HeroStats Hero => _hero;
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
        /// <summary>
        /// Full encounter cycles finished: the packs and the final encounter, or only the packs where there is none
        /// (Hunting Grounds). The unit of an active-play loop; equal to Clears on Korstone stages.
        /// </summary>
        public int Cycles { get; private set; }
        /// <summary>True when the current final encounter is the stage boss rather than a Korstone.</summary>
        public bool IsBossEncounter => IsKorstoneEncounter && _stage.FinalEncounter == FinalEncounter.Boss;
        public bool IsElderNext => _stage.ElderEvery > 0 && (KorstonesDestroyed + 1) % _stage.ElderEvery == 0;
        public bool HasteActive => _tick < _hasteUntilTick;
        public bool EmpowerActive => _tick < _empowerUntilTick;
        public bool FocusActive => _tick < _focusUntilTick;
        public bool PoisonActive => _tick < _poisonUntilTick;
        public bool BindActive => _tick < _bindUntilTick;
        public bool ShieldActive => _tick < _shieldUntilTick && _shieldHp > 0;
        public bool WardActive => _tick < _wardUntilTick;
        /// <summary>Whether this hero's level has reached the skill (SkillDef.UnlockLevel).</summary>
        public bool IsUnlocked(int skillIndex) => Skills[skillIndex].UnlockLevel <= _hero.Level;
        /// <summary>On a mount the hero only makes plain attacks (owner, 26 Sep 2026); skills wait until he dismounts.</summary>
        public bool Mounted => _hero.Mounted;
        /// <summary>Damage landed on bosses, for Commander damage brackets.</summary>
        public long BossDamageDealt { get; private set; }

        /// <summary>Swaps in new stats after a Forge attempt or a turn, keeping the HP ratio.</summary>
        public void SetHero(HeroStats hero)
        {
            HeroHp = Math.Max(1, HeroHp * hero.MaxHp / _hero.MaxHp);
            _hero = hero;
        }

        public int CooldownTicksLeft(int skillIndex) => Math.Max(0, _readyAtTick[skillIndex] - _tick);

        /// <summary>A cast the player taps: aimed, so a Burst goes to the toughest enemy (usually the Korstone or boss).</summary>
        public bool TryCast(int skillIndex) => Cast(skillIndex, aimed: true);

        /// <summary>
        /// GDD section 4: auto-cast fires on cooldown with no target logic, so an auto Burst lands on whatever plain
        /// attacks are hitting (the first thing in the way). Holding bursts for the Korstone is the active-play edge.
        /// </summary>
        private bool Cast(int skillIndex, bool aimed)
        {
            if (Phase != LanePhase.Fighting || _hero.Mounted || CooldownTicksLeft(skillIndex) > 0 || !IsUnlocked(skillIndex)) return false;

            SkillDef skill = Skills[skillIndex];
            _readyAtTick[skillIndex] = _tick + skill.CooldownTicks;
            int grade = skillIndex < _hero.SkillGradeBonusPercent.Length ? _hero.SkillGradeBonusPercent[skillIndex] : 0;
            int power = skill.PowerPercent * (100 + grade) / 100;
            _events.Add(new LaneEvent(LaneEventKind.SkillCast, amount: skillIndex, text: skill.Name));

            switch (skill.Kind)
            {
                case SkillKind.Burst:
                    Enemy? target = aimed ? Toughest() : FrontTarget();
                    if (target != null)
                        Hit(target, aimed && (target.IsKorstone || target.IsBoss) ? power * _hero.WeakPointPercent / 100 : power);
                    break;

                case SkillKind.Area:
                    foreach (Enemy e in _enemies.ToArray())
                        if (e.Hp > 0) Hit(e, power);
                    break;

                case SkillKind.Haste:
                    _hasteUntilTick = _tick + skill.DurationTicks * (100 + grade / 2) / 100;
                    break;

                // Grades raise the power of the newer kinds too; a stun or a bind lasts half as much longer.
                case SkillKind.Empower:
                    _empowerUntilTick = _tick + skill.DurationTicks;
                    _empowerPercent = power;
                    break;

                case SkillKind.Charge:
                    foreach (Enemy e in _enemies.ToArray())
                        if (e.Hp > 0 && _enemies.Contains(e)) Hit(e, power);
                    int stunUntil = _tick + skill.DurationTicks * (100 + grade / 2) / 100;
                    // Map bosses and Commanders shrug off the stun (their captains and images do not).
                    foreach (Enemy e in _enemies)
                        if (!e.IsKorstone && !e.IsBoss) e.NextAttackTick = Math.Max(e.NextAttackTick, stunUntil);
                    break;

                case SkillKind.Poison:
                    _poisonUntilTick = _tick + skill.DurationTicks;
                    _poisonPercent = power;
                    _poisonNextTick = _tick + TicksPerSecond;
                    break;

                case SkillKind.Execute:
                    Enemy? mark = aimed ? Toughest() : FrontTarget();
                    if (mark != null) Hit(mark, mark.Hp * 100 < mark.MaxHp * ExecuteBelowPercent ? power * 2 : power);
                    break;

                case SkillKind.Bind:
                    _bindUntilTick = _tick + skill.DurationTicks * (100 + grade / 2) / 100;
                    _bindPercent = power;
                    break;

                case SkillKind.Shield:
                    _shieldUntilTick = _tick + skill.DurationTicks;
                    _shieldHp = _hero.MaxHp * power / 100;
                    break;

                case SkillKind.Focus:
                    _focusUntilTick = _tick + skill.DurationTicks;
                    _focusBp = power * 100;
                    break;

                case SkillKind.Ward:
                    _wardUntilTick = _tick + skill.DurationTicks;
                    _wardPercent = power;
                    break;
            }

            return true;
        }

        /// <summary>The first thing in the way: plain attacks and auto-cast hit it. Images and captains sit in front of their boss.</summary>
        private Enemy? FrontTarget()
        {
            if (_enemies.Count == 0) return null;
            foreach (Enemy e in _enemies)
                if (!e.IsKorstone) return e;
            return _enemies[0];
        }

        private Enemy? Toughest()
        {
            Enemy? toughest = null;
            foreach (Enemy e in _enemies)
                if (toughest == null || e.Hp > toughest.Hp) toughest = e;
            return toughest;
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

        /// <summary>
        /// The Bannerkin's casts (Rules.Bannerkin), each on its own clock and with no roll: Hunter's Blessing lends its
        /// crit (a stronger blessing already running keeps its strength), Mending Song gives HP back when the hero is hurt.
        /// </summary>
        private void KinTick(KinStats kin)
        {
            if (_tick >= _kinFocusReadyAtTick && kin.FocusTicks > 0)
            {
                _kinFocusReadyAtTick = _tick + kin.FocusCooldownTicks;
                bool running = _tick < _focusUntilTick;
                _focusBp = running ? Math.Max(_focusBp, kin.FocusBp) : kin.FocusBp;
                _focusUntilTick = Math.Max(_focusUntilTick, _tick + kin.FocusTicks);
                _events.Add(new LaneEvent(LaneEventKind.KinCast, text: "Hunter's Blessing"));
            }
            if (_tick >= _kinHealReadyAtTick && kin.HealPercent > 0 && HeroHp < _hero.MaxHp)
            {
                _kinHealReadyAtTick = _tick + kin.HealCooldownTicks;
                long heal = Math.Min(_hero.MaxHp - HeroHp, _hero.MaxHp * kin.HealPercent / 100);
                HeroHp += heal;
                _events.Add(new LaneEvent(LaneEventKind.KinCast, amount: heal, text: "Mending Song"));
                _events.Add(new LaneEvent(LaneEventKind.HeroHealed, amount: heal));
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
                if (AutoCast[i]) Cast(i, aimed: false);
            if (_hero.Kin != null) KinTick(_hero.Kin);

            if (PoisonActive && _tick >= _poisonNextTick)
            {
                _poisonNextTick += TicksPerSecond;
                foreach (Enemy e in _enemies.ToArray())
                    if (e.Hp > 0 && _enemies.Contains(e)) Hit(e, _poisonPercent, canCrit: false);
            }

            if (_tick >= _heroNextAttackTick && _enemies.Count > 0)
            {
                Hit(FrontTarget()!, 100, plain: true);
                int interval = HasteActive ? Math.Max(1, _hero.AttackIntervalTicks / 2) : _hero.AttackIntervalTicks;
                _heroNextAttackTick = _tick + interval;
            }

            Enemy? boss = null;
            foreach (Enemy e in _enemies)
            {
                if (e.IsBoss) boss = e;
                if (e.IsKorstone || _tick < e.NextAttackTick) continue;
                // Chains slow everything but a boss, which breaks them (it still takes the extra damage).
                e.NextAttackTick = _tick + (BindActive && !e.IsBoss ? _stage.MobAttackIntervalTicks * 2 : _stage.MobAttackIntervalTicks);
                if (_hero.EvasionBp > 0 && _rng.RollBp(_hero.EvasionBp))
                {
                    _events.Add(new LaneEvent(LaneEventKind.HeroDamaged, e.Id, 0, text: "evaded"));
                    continue;
                }
                long damage = Math.Max(1, e.Attack - _hero.Defense);
                if (e.IsBoss || e.Kind == EnemyKind.Image || e.Kind == EnemyKind.Captain)
                    damage = Math.Max(1, damage * _hero.CommanderDamageTakenPercent / 100);
                damage = Mitigate(e, damage, out bool veiled);
                HeroHp -= damage;
                _events.Add(new LaneEvent(LaneEventKind.HeroDamaged, e.Id, damage, text: veiled ? "veiled" : null));
                if (HeroHp <= 0) break;
            }

            // A Ward's returned blows land once the strikers are done (they may kill them).
            if (_reflected.Count > 0)
            {
                foreach ((Enemy striker, long amount) in _reflected.ToArray())
                    if (striker.Hp > 0 && _enemies.Contains(striker) && !Shielded(striker)) Wound(striker, amount, false);
                _reflected.Clear();
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
                // A cloud or chains stay with the pack they were cast on.
                _poisonUntilTick = 0;
                _bindUntilTick = 0;
                _events.Add(new LaneEvent(LaneEventKind.EncounterCleared));
                if (IsKorstoneEncounter) Clears++;
                int loop = _stage.FinalEncounter == FinalEncounter.None ? _stage.PacksBeforeKorstone : _stage.PacksBeforeKorstone + 1;
                EncounterIndex = (EncounterIndex + 1) % Math.Max(1, loop);
                if (EncounterIndex == 0) Cycles++;
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

        /// <summary>The captains' shield on their boss: a blow on it is turned aside (said once per blow).</summary>
        private bool Shielded(Enemy enemy)
        {
            if (!enemy.IsBoss || _stage.BossMechanic != BossMechanic.CaptainShield) return false;
            foreach (Enemy e in _enemies)
            {
                if (e.Kind != EnemyKind.Captain) continue;
                _events.Add(new LaneEvent(LaneEventKind.Shielded, enemy.Id, text: "Shielded by captains"));
                return true;
            }
            return false;
        }

        /// <summary>A Ward softens a blow and queues its return; a Shield then takes what it can. Returns what reaches the hero.</summary>
        private long Mitigate(Enemy striker, long damage, out bool veiled)
        {
            veiled = false;
            if (WardActive)
            {
                damage = Math.Max(1, damage * (100 - WardReductionPercent) / 100);
                _reflected.Add((striker, Math.Max(1, damage * _wardPercent / 100)));
            }
            if (ShieldActive)
            {
                long absorbed = Math.Min(_shieldHp, damage);
                _shieldHp -= absorbed;
                damage -= absorbed;
                veiled = absorbed > 0;
            }
            return damage;
        }

        /// <param name="canCrit">False for poison ticks: a cloud does not find weak points.</param>
        /// <param name="plain">A plain attack, which the weapon's average damage roll changes; every other hit is a skill's.</param>
        private void Hit(Enemy enemy, int powerPercent, bool canCrit = true, bool plain = false)
        {
            if (Shielded(enemy)) return;

            long attack = EmpowerActive ? _hero.Attack * (100 + _empowerPercent) / 100 : _hero.Attack;
            bool crit = canCrit && _rng.RollBp(_hero.CritChanceBp + (FocusActive ? _focusBp : 0));
            long damage = attack * powerPercent / 100 * _stage.DamagePercent / 100;
            int roll = plain ? _hero.AverageDamagePercent : _hero.SkillDamagePercent;
            if (roll != 0) damage = damage * (100 + roll) / 100;
            if (crit) damage = damage * _hero.CritMultiplierPercent / 100;
            if (!enemy.IsBoss && _hero.BeastDamagePercent > 0) damage = damage * (100 + _hero.BeastDamagePercent) / 100;
            damage = Math.Max(1, damage * (90 + _rng.NextInt(21)) / 100);
            if (BindActive) damage = damage * (100 + _bindPercent) / 100;
            Wound(enemy, damage, crit);
        }

        /// <summary>Damage that has landed: the enemy loses it, and dies, drops, splits or calls a wave as it must.</summary>
        private void Wound(Enemy enemy, long damage, bool crit)
        {
            damage = Math.Min(damage, enemy.Hp);

            enemy.Hp -= damage;
            if (enemy.IsBoss) BossDamageDealt += damage;
            _events.Add(new LaneEvent(LaneEventKind.EnemyDamaged, enemy.Id, damage, crit));

            if (enemy.Kind == EnemyKind.Image)
            {
                long reflected = Math.Max(1, damage * ImageReflectPercent / 100 * _hero.CommanderDamageTakenPercent / 100);
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
