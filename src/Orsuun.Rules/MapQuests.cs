#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>What a map quest's step asks for.</summary>
    public enum QuestKind
    {
        /// <summary>Seconds of live hunting on the map.</summary>
        Hunt,
        /// <summary>Korstones broken on the map (online or off, like the bounty).</summary>
        Korstones,
        /// <summary>A fight with the map's Commander (MapQuests.CommanderOf).</summary>
        Commander,
    }

    /// <summary>A step of a map's quest: what it asks, how much, the camp of the map's layout where its story is (the full
    /// map's scroll marker) and the quest giver's words.</summary>
    public sealed class QuestStepDef
    {
        public QuestStepDef(QuestKind kind, long target, string camp, string ask)
        {
            Kind = kind;
            Target = target;
            Camp = camp;
            Ask = ask;
        }

        public QuestKind Kind { get; }
        public long Target { get; }
        public string Camp { get; }
        public string Ask { get; }
    }

    /// <summary>A map's quest chain: its giver, its name, three steps, and the giver's last words.</summary>
    public sealed class MapQuestDef
    {
        public MapQuestDef(int map, string giver, string title, QuestStepDef[] steps, string ending)
        {
            Map = map;
            Giver = giver;
            Title = title;
            Steps = steps;
            Ending = ending;
        }

        public int Map { get; }
        public string Giver { get; }
        public string Title { get; }
        public QuestStepDef[] Steps { get; }
        public string Ending { get; }
    }

    /// <summary>
    /// Map quests (owner, 30 Sep 2026: picked "Map quests": "Each big map's townsfolk give a short quest chain (hunt at a
    /// named camp, break Korstones, face the map's Commander) with sorn, gear and a line of story; a scroll marker shows the
    /// quest's camp on the full map"). Every campaign map has a chain of three steps from one of its folk: hunt on the map
    /// (the story names a camp on its trail), break Korstones there, then fight its Commander. The server counts a step only
    /// while it is the chain's current one and the deed is done on that map (hunting and Korstones where the hero hunts, the
    /// fight by the Commander's map); a step done is claimed on the quest card for sorn, XP and materials by the map's last
    /// stage, and the last step adds an Epic piece of the map's level. A chain opens with its map. Assumptions (not stated by
    /// the owner): the targets, the pay, the words, and that "at a named camp" is hunting anywhere on the map (the lane
    /// walks the whole trail past every camp; the camp is where the story is).
    /// </summary>
    public static class MapQuests
    {
        public const long HuntSeconds = 10 * 60, Korstones = 8;
        public const int StepMobs = 60, FinalMobs = 150, StepMaterials = 5, FinalMaterials = 10;

        private static QuestStepDef Hunt(string camp, string ask) => new QuestStepDef(QuestKind.Hunt, HuntSeconds, camp, ask);
        private static QuestStepDef Stones(string camp, string ask) => new QuestStepDef(QuestKind.Korstones, Korstones, camp, ask);
        private static QuestStepDef Face(string camp, string ask) => new QuestStepDef(QuestKind.Commander, 1, camp, ask);

        /// <summary>The twelve chains, by map (camps are the layouts' names; each Commander's camp is where it rises).</summary>
        public static readonly MapQuestDef[] All =
        {
            new MapQuestDef(1, "Headwoman Saruul of Yurt Village", "Smoke over the Oathfields", new[]
            {
                Hunt("Bandit Camp", "Deserters hold the Bandit Camp and raid our herds by night. Hunt the fields around it until they think twice."),
                Stones("Watchtower Hill", "The Korstones by Watchtower Hill hum louder every night, and the wolves come out of them hollow. Break them before more wake."),
                Face("Wolf Den", "Old Greyjaw leads the hollow packs from the Wolf Den. When he rises, face him, and the Oathfields will sleep again."),
            }, "The herds graze in peace tonight. Take this, rider: the Oathfields will remember your name."),
            new MapQuestDef(2, "Scout Arslan of the Palisade", "The Drums of Gorak Pass", new[]
            {
                Hunt("Hound Pens", "Gorak's war hounds are loosed from the Hound Pens at dusk. Thin them out, or no rider crosses the pass."),
                Stones("Skull Rocks", "Their shamans feed the Korstones at Skull Rocks with blood. Break every stone you find in the pass."),
                Face("The Drum Ground", "When the drums beat on the Drum Ground, Tul-Gorak himself comes down. Meet him there."),
            }, "The drums are quiet. The Palisade owes you a debt, and I pay mine."),
            new MapQuestDef(3, "Caravan Master Dilek", "The Last Caravan", new[]
            {
                Hunt("Wreck of the Last Caravan", "My brother's caravan never left the salt. Its drivers still walk the wreck, hollow and hungry. Give them rest."),
                Stones("Pillar Field", "Korstones grow in the Pillar Field like salt crystals. Break them, and the mirages thin."),
                Face("Oasis Shrine", "The Mirage Queen drank the Oasis Shrine dry. When she shows her true face, strike it."),
            }, "Water runs at the Oasis again. My brother can rest, and so can I."),
            new MapQuestDef(4, "Elder Oyun of Whitefang Hamlet", "Nine Winters Long", new[]
            {
                Hunt("Bear Caves", "The bears of the Bear Caves woke mad in midwinter, and our hunters no longer come home. Hunt them back into the dark."),
                Stones("Hag's Hollow", "The snow hags of Hag's Hollow sing over Korstones to keep the cold. Break their stones."),
                Face("Shrine Pass", "Nine-Winters walks down Shrine Pass when the wind turns. Face him, and spring may find us."),
            }, "The ice on the lake is cracking. Nine winters were enough. Thank you, rider."),
            new MapQuestDef(5, "Watch Captain Kerem of the Burnt Watchtower", "The Burnt Watch", new[]
            {
                Hunt("Cultist Pyres", "Flame cultists light new pyres every night to call the wyrm. Hunt them at the Cultist Pyres before the fires join."),
                Stones("Obsidian Spires", "The Korstones among the Obsidian Spires glow like coals. The cultists feed them: break them."),
                Face("Lava Ford", "Azhdar crawls up from the Lava Ford when the ground shakes. Hold the ford against it."),
            }, "The watch fires burn for us again, not for the wyrm. The Marches are yours to walk."),
            new MapQuestDef(6, "Grave-Keeper Nomin", "Lanterns in the Mist", new[]
            {
                Hunt("Old Graves", "The dead of the Old Graves no longer stay under their stones. Walk among them and put them down."),
                Stones("The Forgotten Shrine", "Korstones have grown through the floor of the Forgotten Shrine. Their whispers keep the dead awake."),
                Face("Widow's Pond", "The Lantern Widow waits at her pond for a husband who never came home. Answer her in his place."),
            }, "The lanterns have gone out over the pond. Let the Whisperwood be quiet a while."),
            new MapQuestDef(7, "Woodcutter Temur", "The Bleeding Wood", new[]
            {
                Hunt("Stalker Trail", "Something follows the woodcutters home along the Stalker Trail. Hunt the trail until it stops following."),
                Stones("Sap Pools", "The Sap Pools boil red where Korstones sit under them. Break the stones, and the sap may run clear."),
                Face("Root Hollow", "The Rootfather wakes in Root Hollow, and the whole wood bleeds. Face him when he rises."),
            }, "The birches weep no more. I will cut only dead wood from now on: you have my word."),
            new MapQuestDef(8, "Ferryman Baatar of the Sunken Yurts", "Under the Reeds", new[]
            {
                Hunt("Bog Rider Camp", "The bog riders were our herders once, before the water rose. Their camp still rides out at night: hunt it."),
                Stones("Leech Marsh", "The leeches swarm thickest where Korstones lie in the Leech Marsh. Break them, and the swarms will scatter."),
                Face("Serpent Pools", "The Coil Mother drowned our steppe from the Serpent Pools. When she surfaces, strike."),
            }, "The water is going down: I can see the old yurt roofs. My ferry may soon be a cart again."),
            new MapQuestDef(9, "Stone-Singer Altan", "The Unburied", new[]
            {
                Hunt("Bone Pickers' Nest", "The bone pickers strip the old giants bare from their nest. Drive them off before nothing is left to bury."),
                Stones("Field of Ribs", "Korstones sprout between the ribs in the Field of Ribs, and the giants stir. Break them."),
                Face("The Broken Colossus", "Hurm will not stay in his grave. When he stands up by the Broken Colossus, lay him down again."),
            }, "I will sing the old giants to sleep tonight. Hurm too, if he lets me."),
            new MapQuestDef(10, "Moneychanger Esen", "Debts of the Drowned", new[]
            {
                Hunt("Debtors' Row", "The debt wraiths of Debtors' Row still count what they owe. Settle their accounts with steel."),
                Stones("Gilded Stalls", "The constructs of the Gilded Stalls are wound with Korstones. Break the stones, and they will stop."),
                Face("Drowned Market", "The Last Merchant-Prince still keeps court in the Drowned Market. Close his last sale."),
            }, "Every debt in the Bazaar is paid. A moneychanger rarely says this: keep the change."),
            new MapQuestDef(11, "Standard-Bearer Tsogt", "The Left Wing Rides Again", new[]
            {
                Hunt("Rider Barrows", "My old comrades rise from the Rider Barrows and form up as if the war never ended. Send them back to rest."),
                Stones("Violet Field", "The Violet Field glows with Korstones where the Left Wing fell. Break them: they are what calls the dead."),
                Face("Captain's Mound", "Varkesh led us once. He still commands from the Captain's Mound. Face him, and tell him the war is over."),
            }, "I carried this standard for Varkesh. Now I will plant it on his mound and go home."),
            new MapQuestDef(12, "The Last Oathkeeper", "The Hollow Throne", new[]
            {
                Hunt("Chanters' Ring", "The oath chanters in their ring still swear the old oaths to an empty throne. Silence them."),
                Stones("Oath Stones", "Korstones have grown round the Oath Stones and give the chanters their voice. Break them."),
                Face("Throne Steps", "The Khan's Shadow sits on the Throne Steps where the Khan once sat. End it, and the steppe is free."),
            }, "The throne is empty at last, and it will stay so. Ride out, hero: the steppe is yours."),
        };

        public static MapQuestDef? For(int map) => All.FirstOrDefault(q => q.Map == map);

        /// <summary>The campaign map a hunting place belongs to (0: a zone, not a map).</summary>
        public static int MapOfPlace(int parkId) => Content.IsZone(parkId) ? 0 : Content.MapOfStage(parkId).Id;

        /// <summary>The Commander of a map: the war camp's three stand on the first three maps (Content.CommanderPlaces).</summary>
        public static int CommanderOf(int map) => map switch { 1 => 3, 2 => 1, 3 => 2, _ => map };

        /// <summary>The map whose Commander a boss is (0: none).</summary>
        public static int MapOfCommander(int bossId) => Enumerable.Range(1, Content.Maps.Length).FirstOrDefault(m => CommanderOf(m) == bossId);

        /// <summary>A chain opens with its map (its first stage open to hunt).</summary>
        public static bool Open(int map, int highestStageCleared) => Content.IsUnlocked((map - 1) * MapDef.StagesPerMap + 1, highestStageCleared);

        /// <summary>What the step asks, in a line (the giver's words are its Ask).</summary>
        public static string Task(MapQuestDef quest, QuestStepDef step) => step.Kind switch
        {
            QuestKind.Hunt => $"Hunt {step.Target / 60} minutes on {Content.Maps[quest.Map - 1].Name}",
            QuestKind.Korstones => $"Break {step.Target} Korstones on {Content.Maps[quest.Map - 1].Name}",
            _ => $"Fight {Content.Boss(CommanderOf(quest.Map))?.Name ?? "the Commander"} when it rises",
        };

        private static StageConfig LastStage(int map) => Content.Stage(map * MapDef.StagesPerMap);

        public static long Sorn(int map, bool last) => LastStage(map).SornPerMob * (last ? FinalMobs : StepMobs);
        public static long Xp(int map, bool last) => LastStage(map).XpPerMob * (last ? FinalMobs : StepMobs);
        public static int Materials(bool last) => last ? FinalMaterials : StepMaterials;

        /// <summary>The Epic piece at a chain's end: the map's last stage's item level, a slot at random.</summary>
        public static ItemState Piece(int map, Inventory inventory, IRandom rng)
        {
            StageConfig stage = LastStage(map);
            stage.GearRarityCap = Rarity.Epic;
            return HuntYield.DropGear(stage, inventory, rng, Rarity.Epic, Rarity.Epic);
        }
    }

    /// <summary>
    /// A hero's map quests: for each map begun, the step being worked on (Steps: the chain is done) and how far along it is.
    /// Stored as "map:step:progress;..."; maps not begun are at step 0 with nothing done.
    /// </summary>
    public sealed class QuestProgress
    {
        private readonly SortedDictionary<int, (int Step, long Done)> _maps = new SortedDictionary<int, (int, long)>();

        public static QuestProgress Parse(string? text)
        {
            var p = new QuestProgress();
            foreach (string entry in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = entry.Split(':');
                if (f.Length == 3 && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int map)
                    && int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step)
                    && long.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long done)
                    && MapQuests.For(map) != null)
                    p._maps[map] = (Math.Max(0, step), Math.Max(0, done));
            }
            return p;
        }

        public string Serialize() => string.Join(";", _maps.Select(kv =>
            kv.Key.ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.Step.ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.Done.ToString(CultureInfo.InvariantCulture)));

        public int Step(int map) => _maps.TryGetValue(map, out var s) ? s.Step : 0;
        public long Done(int map) => _maps.TryGetValue(map, out var s) ? s.Done : 0;

        /// <summary>The map's current step (null: the chain is done or the map has none).</summary>
        public QuestStepDef? Current(int map)
        {
            MapQuestDef? quest = MapQuests.For(map);
            int step = Step(map);
            return quest != null && step < quest.Steps.Length ? quest.Steps[step] : null;
        }

        public bool Finished(int map) => MapQuests.For(map) is { } q && Step(map) >= q.Steps.Length;

        /// <summary>The current step is done and waits to be claimed.</summary>
        public bool Ready(int map) => Current(map) is { } step && Done(map) >= step.Target;

        /// <summary>Counts a deed on a map toward its current step, if that step asks for it; true when it moved.</summary>
        public bool Add(int map, QuestKind kind, long amount)
        {
            if (amount <= 0 || !(Current(map) is { } step) || step.Kind != kind) return false;
            long done = Done(map);
            if (done >= step.Target) return false;
            _maps[map] = (Step(map), Math.Min(step.Target, done + amount));
            return true;
        }

        /// <summary>Moves a map's chain past its claimed step.</summary>
        public void Advance(int map) => _maps[map] = (Step(map) + 1, 0);
    }
}
