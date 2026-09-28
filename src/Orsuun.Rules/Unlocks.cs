#nullable enable
using System.Collections.Generic;

namespace Orsuun.Rules
{
    /// <summary>The screens that open as a hero levels (Unlocks).</summary>
    public enum Feature
    {
        Bounties,
        Shards,
        Commanders,
        Guild,
        Exchange,
        War,
        Dungeons,
        Pits,
        Fishing,
    }

    /// <summary>
    /// Feature unlocks (owner, 27 Sep 2026: "Feature unlocks by level": "War, Guild, Trade, the Pits and dungeons open
    /// as the hero levels, each with a one-line tip when it opens. A new player sees fewer buttons in the first hour").
    /// The hunt, the Forge, the inventory, PUSH, ZONES' hunting grounds, the Caravan, the Campaign Trail and MENU are open
    /// from the start. A locked button shows the level it opens at; the level-up that opens it says so with its tip. The
    /// client gates the screens; the server does not (a guild invite, a trade call or a letter may still lead there).
    /// </summary>
    public static class Unlocks
    {
        public static readonly Feature[] All =
        {
            Feature.Bounties, Feature.Shards, Feature.Fishing, Feature.Commanders, Feature.Guild, Feature.Exchange, Feature.War, Feature.Dungeons, Feature.Pits,
        };

        public static int Level(Feature feature) => feature switch
        {
            Feature.Bounties => 3,
            Feature.Shards => 5,
            Feature.Commanders => 8,
            Feature.Guild => 10,
            Feature.Exchange => 12,
            Feature.War => 15,
            Feature.Dungeons => 18,
            Feature.Pits => 20,
            Feature.Fishing => 6,
            _ => 1,
        };

        public static bool Open(Feature feature, int level) => level >= Level(feature);

        /// <summary>The feature's name as the unlock notice shows it.</summary>
        public static string Name(Feature feature) => feature switch
        {
            Feature.Bounties => "BOUNTIES",
            Feature.Shards => "KORSHARDS",
            Feature.Commanders => "COMMANDERS",
            Feature.Guild => "GUILDS",
            Feature.Exchange => "SALT EXCHANGE",
            Feature.War => "WAR OF BANNERS",
            Feature.Dungeons => "DUNGEONS",
            Feature.Pits => "THE PITS",
            Feature.Fishing => "OLD NERGUI'S RIVER",
            _ => "",
        };

        /// <summary>One line on what it is and where it is.</summary>
        public static string Tip(Feature feature) => feature switch
        {
            Feature.Bounties => "Daily tasks pay Hunt Marks. Claim them on BOUNTIES.",
            Feature.Shards => "Set the Korshards your Korstones drop into your gear for more power: SHARDS.",
            Feature.Commanders => "Commanders rise on the steppe: fight them from ZONES for chests and skins.",
            Feature.Guild => "Join a guild for raids, guild wars and a shared treasury: GUILD.",
            Feature.Exchange => "Buy and sell gear with other heroes on the Salt Exchange: TRADE.",
            Feature.War => "Your Banner fights for the fortresses. Lay siege in WAR.",
            Feature.Dungeons => "Dungeons open in ZONES: deep floors, two keys a day and the Chained Smith.",
            Feature.Pits => "Duel other heroes in the Pits, from WAR, for Laurels and a season title.",
            Feature.Fishing => "Old Nergui fishes the river in ZONES: catch fish to eat for hunting boosts, and mussels with pearls inside.",
            _ => "",
        };

        /// <summary>What a locked button says when tapped.</summary>
        public static string Locked(Feature feature) => $"Opens at level {Level(feature)}. {Tip(feature)}";

        /// <summary>The features a level-up from `from` to `to` opens, in order.</summary>
        public static List<Feature> Between(int from, int to)
        {
            var opened = new List<Feature>();
            foreach (Feature feature in All)
                if (Level(feature) > from && Level(feature) <= to) opened.Add(feature);
            return opened;
        }
    }
}
