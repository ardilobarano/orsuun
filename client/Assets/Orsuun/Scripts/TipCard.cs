using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// What's new tips (owner, 28 Sep 2026: picked "What's new tips": "A short tip card the first time a hero meets the
    /// town, the Bazaar Call, the fishing catch and the Bannerkin"; 29 Sep 2026: "What's new tip for maps", the first big map).
    /// Each tip shows once for each hero on this device
    /// (PlayerPrefs, keyed like the goal line), on its own canvas above the screens; GOT IT closes it. -tip Town|Bazaar|
    /// Catch|Bannerkin|Map shows one for screenshots whether it was seen or not.
    /// </summary>
    public sealed class TipCard : MonoBehaviour
    {
        public enum Tip { Town, Bazaar, Catch, Bannerkin, Map, Party, Commander, Elite, Quest, Rain }

        private static readonly string[] Titles = { "THE TOWN SQUARE", "THE BAZAAR CALL", "THE CATCH", "THE BANNERKIN", "THE OPEN MAP", "HUNTING PARTIES", "WORLD BOSSES",
            "GOLDEN BANNERS", "MAP QUESTS", "KORSTONE RAIN" };
        private static readonly string[] Bodies =
        {
            "Walk to the townsfolk: Dorun forges, Ilke keeps the Caravan, Tamir teaches skills and Bora runs the Pits. Each gives an errand a day. Heroes who are in town stand here too, and the hunt goes on while you visit.",
            "Heroes of level 20 call their wares here, once every 30 seconds. LINK shows one of your pieces with your words. Tap a piece someone linked to see it, then TRADE or WHISPER.",
            "Hold anywhere to lift the bronze box, let go and it sinks. Keep the fish inside it until the bar fills. The rarer the fish, the wilder it swims.",
            "She walks behind you on the hunt: Hunter's Blessing lends you crits and Mending Song heals you. Her six pieces are her own: forge them like yours to make her stronger. Duels and the Pits leave her out.",
            "Your hunt walks a whole region now. The minimap shows its camps, blue where other heroes hunt: tap it for the full map. Tap a hero in the field to inspect, whisper or trade. A glowing chest by the trail is a cache: tap it to open it.",
            // What's new tips for the parties, the maps' Commanders and the elite camps (30 Sep 2026).
            "Hunt with up to three others: friends, guildmates, or heroes on this map's PARTY BOARD. Partymates on your map walk and fight beside you, and each adds 5% XP and sorn. PARTY CHAT reaches them anywhere.",
            "Now and then a map's Commander rises at its landmark for ten minutes. Every hero on the map may fight it once: tap the red call, or its name on the full map. Its health is shared, so every blow counts. COMMANDER ALERTS in SETTINGS tells your phone when one rises.",
            "A camp under a golden banner holds elite packs for ten minutes: tougher and harder-hitting, and they pay their loot twice more, sometimes with gear. Hunt past it while the banner flies.",
            // Map quests (30 Sep 2026).
            "Someone on every map needs a hero: hunt there, break its Korstones, then face its Commander. Tap QUEST under the map for the story and your pay, and claim each step when it is done. The last step pays an Epic piece.",
            // Korstone Rain (7 Oct 2026).
            "Now and then a Giant Korstone falls from the sky on a map. Every hero hunting there may strike it three times, and its health is shared: strike it together. When it breaks, every striker is showered by their share of sorn, Turnstones and Korshards, sometimes of a rarer rank, and every other hunter on the map gets a little too.",
        };
        private static readonly string[] Icons = { "Caravan", "NavTrade", "FishTaimen", "BookDrumcaller", "NavZones", "NavGuild", "NavWar", "KhansAlloy", QuestPanel.IconName, "ShardCommander" };

        private GameRoot _root;
        private GameObject _canvas;
        private Text _title, _body;
        private RawImage _icon;
        private System.Action _closed;

        public bool Showing => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TipCanvas", 38).gameObject;
            Transform canvas = _canvas.transform;
            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.62f));
            Transform box = Ui.Framed("Box", canvas, 0.08f, 0.3f, 0.92f, 0.7f, Palette.PanelDark).transform;
            Ui.Label("New", box, 0.05f, 0.88f, 0.95f, 0.97f, "NEW", 20, TextAnchor.MiddleCenter, Palette.Warn);
            _title = Ui.Title("Title", box, 0.05f, 0.76f, 0.95f, 0.89f, "", 32, TextAnchor.MiddleCenter, Palette.Sorn);
            RectTransform iconBox = Ui.Rect("IconBox", box, 0.05f, 0.33f, 0.27f, 0.72f);
            _icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Caravan");
            _body = Ui.Label("Body", box, 0.3f, 0.24f, 0.95f, 0.74f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Button("Ok", box, 0.3f, 0.04f, 0.7f, 0.19f, "GOT IT", 26, Palette.Safe, Close, out _);
            _canvas.SetActive(false);
            string shot = Arg("-tip");
            if (shot != null && System.Enum.TryParse(shot, out Tip tip)) Show(tip);
        }

        /// <summary>Shows a tip the first time this hero meets it; <paramref name="closed"/> runs when it closes (at once
        /// if it was seen before).</summary>
        public void Offer(Tip tip, System.Action closed = null)
        {
            string key = "orsuun.tip." + tip + "." + HeroKey();
            bool seen;
            try { seen = PlayerPrefs.GetInt(key, 0) == 1; } catch { seen = true; }
            if (seen || Showing) { closed?.Invoke(); return; }
            try { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); } catch { }
            _closed = closed;
            Show(tip);
        }

        private void Show(Tip tip)
        {
            _title.text = Titles[(int)tip];
            _body.text = Bodies[(int)tip];
            Ui.SetIcon(_icon, Icons[(int)tip]);
            _canvas.SetActive(true);
        }

        private void Close()
        {
            _canvas.SetActive(false);
            System.Action closed = _closed;
            _closed = null;
            closed?.Invoke();
        }

        /// <summary>The hero the tips are remembered for (like the goal line's key).</summary>
        private string HeroKey()
        {
            Net.ServerLink server = _root.Server;
            return server.Online && !string.IsNullOrEmpty(server.PlayerName) ? server.PlayerName : "local";
        }

        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
