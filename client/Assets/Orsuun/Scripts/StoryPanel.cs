using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Story beats (owner, 27 Sep 2026: "A short painted card with two or three lines from the world bible when a map opens
    /// and when its boss falls. Reuses the map backdrops, no credits"). Twenty-four cards: each map as it opens (the
    /// Oathfields once the first session's guide is done) and each map boss as it falls, over the map's own lane backdrop.
    /// Written for the world bible's steppe and tone ("dry, fatalistic steppe humor, no chosen-one story"). Each is shown
    /// once per hero (the count is kept on the device); a hero first seen part-way through starts from where it stands.
    /// </summary>
    public sealed class StoryPanel : MonoBehaviour
    {
        /// <summary>For each map: the lines when it opens, then the lines when its boss falls.</summary>
        private static readonly (string opens, string falls)[] Beats =
        {
            ("The stones came up through the barley this spring, black and warm as a hearth. The old women say the dead are marching again. You swore the oath anyway: somebody has to break them.",
             "Greyjaw guarded these fields for twenty winters before a stone's hum emptied him. The herders will sleep tonight. The markers under the pass will not."),
            ("The Gorak clans never swore to anyone. Now a Korstone speaks to them, and at last they have a khan. The pass smells of tar and burning felt.",
             "Tul-Gorak fought like a man who could not die, because he believed it. His raiders scatter south, toward the salt."),
            ("A dry seabed the caravans once crossed in a week. The Salt Peace still holds here, mostly: the scorpions never signed it.",
             "She showed every traveller the thing they wanted most, then let the salt keep them. Her images fade in the heat. What she wanted, nobody asked."),
            ("Above the snow line the Sky monks keep the old carvings. Below it, the ice wights keep everything else. Wrap your hands: the metal bites.",
             "Nine winters he stood on the ridge, counting the ones who climbed. He will not count you. The monks ring one bell for him, out of courtesy."),
            ("Here the markers went so deep they found fire. The ground cracks, the air shimmers, and the cultists sing to a wyrm that never learned gratitude.",
             "The Furnace Wyrm is cold at last. Forgemaster Dorun will want its heart. He will apologise to it first."),
            ("Lanterns hang from every branch, and none of them were lit by the living. Keep walking. Whatever calls your name learned it from a grave.",
             "She lit a lantern for every soul she lost, until the forest was brighter than the town. Her lights go out one by one. The woods are only dark now."),
            ("The birches bleed when they are cut, and lately they cut back. The Hollowed come here to rest, and the trees make sure they stay.",
             "The Rootfather drank from the markers for a hundred years. Pull him up, and the ground groans like something turning over in its sleep."),
            ("The river left its bed to cover a battlefield, and the serpents found the bones first. Tread on the tussocks, never between them.",
             "Her brood will hatch without her now. The bog riders lower their spears, unsure who gives the orders any more."),
            ("Giants lay buried here before the Khan was born. His hum woke them anyway: the dead do not ask whose war it is.",
             "Hurm lies down again, as he did an age ago. The bone pickers wait politely for you to leave."),
            ("Velimar's old market sank with its ledgers still open. The debts did not drown. They walk the arcades, collecting.",
             "The Merchant-Prince closes his last account. The Gold Banner will call it a tragedy for trade. Nobody else will."),
            ("The plain where the free clans buried the Khan's army: one carved stone for every rider. The carvings are failing. You can hear them breathing under the grass.",
             "Varkesh of the Left Wing held his line for nine hundred years under the earth. It breaks today, and the markers ahead lean toward the throne."),
            ("At the bottom of the grave stands a throne of cut markers, and something sits in it wearing the Khan's shape. Every oath you swore has led here.",
             "The Shadow breaks, and for a moment the steppe is quiet. Then, far below, something older stirs. The Khan himself has not woken yet. Keep your oath warm."),
        };

        /// <summary>The lane backdrop each map is painted with (LaneView's keys).</summary>
        private static readonly string[] Backdrops =
        {
            "HuntingGround", "CommanderGround", "SaltFlats", "FrostPasture", "CinderMarches", "Whisperwood",
            "Bloodbirch", "DrownedSteppe", "ColossusGraves", "SunkenBazaar", "ThousandMarkers", "HollowThrone",
        };

        private GameRoot _root;
        private GameObject _canvas;
        private RawImage _picture;
        private Text _kicker, _title, _text;
        private string _heroKey;
        private int _shown = -1;
        private float _checkIn;

        public bool Showing => _canvas != null && _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            // This component stays off its canvas: it watches for the next beat while the card is hidden.
            _canvas = Ui.Canvas("StoryCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            Ui.Panel("Scrim", canvas, 0f, 0f, 1f, 1f, new Color(0.01f, 0.01f, 0.03f, 0.78f));
            Image card = Ui.Framed("Card", canvas, 0.05f, 0.22f, 0.95f, 0.82f, new Color(0.06f, 0.05f, 0.05f, 0.97f));
            _picture = Ui.Picture("Picture", card.transform, 0.03f, 0.46f, 0.97f, 0.97f, null);
            _kicker = Ui.Label("Kicker", card.transform, 0.06f, 0.38f, 0.94f, 0.44f, "", 22, TextAnchor.MiddleCenter, Palette.Sorn);
            _title = Ui.Title("Title", card.transform, 0.06f, 0.3f, 0.94f, 0.39f, "", 40, TextAnchor.MiddleCenter, Palette.Parchment);
            _text = Ui.Label("Text", card.transform, 0.08f, 0.13f, 0.92f, 0.3f, "", 25, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Continue", card.transform, 0.3f, 0.025f, 0.7f, 0.11f, "CONTINUE", 28, Palette.ButtonForge, Close, out _);
            _canvas.SetActive(false);
        }

        /// <summary>Beat n: map n/2 + 1 opening (n even) or its boss falling (n odd).</summary>
        private static bool Reached(int beat, int cleared, bool guideDone) =>
            beat == 0 ? guideDone || cleared > 0 : cleared >= (beat + 1) / 2 * MapDef.StagesPerMap;

        private void Show(int beat)
        {
            int map = beat / 2;
            MapDef def = Content.Maps[map];
            var backdrop = Art.Load<Material>("Backdrops/Backdrop" + Backdrops[map]);
            Texture art = backdrop != null ? backdrop.mainTexture : null;
            _picture.texture = art;
            _picture.enabled = art != null;
            if (art != null) _picture.GetComponent<AspectRatioFitter>().aspectRatio = art.width / (float)art.height;
            bool opens = beat % 2 == 0;
            _kicker.text = opens ? $"Map {map + 1}  ·  levels {def.LevelMin}-{def.LevelMax}" : def.Name;
            _title.text = opens ? def.Name : $"{def.BossName} falls";
            _text.text = opens ? Beats[map].opens : Beats[map].falls;
            _canvas.SetActive(true);
            GameAudio.Instance?.Play(opens ? "LaneLevelUp" : "StingVictory", 0.8f, 0f, 0f);
        }

        private void Close()
        {
            _canvas.SetActive(false);
            _shown++;
            Save();
        }

        private void Save()
        {
            try { PlayerPrefs.SetInt("orsuun.story." + _heroKey, _shown); PlayerPrefs.Save(); } catch { }
        }

        private void Update()
        {
            if (_root == null || Showing) return;
            _checkIn -= Time.unscaledDeltaTime;
            if (_checkIn > 0f) return;
            _checkIn = 0.5f;
            Net.ServerLink server = _root.Server;
            string key = server.Online ? server.PlayerName : server.Status.StartsWith("LOCAL MODE") ? "local" : null;
            if (string.IsNullOrEmpty(key)) return;
            int cleared = _root.Session.HighestStageCleared;
            bool guideDone = Tutorial.Finished && !_root.Tutorial.Running;
            if (key != _heroKey)
            {
                _heroKey = key;
                int saved;
                try { saved = PlayerPrefs.GetInt("orsuun.story." + key, -1); } catch { saved = -1; }
                if (saved < 0)
                {
                    // A hero first seen here: the beats it has already passed are not told again (a new hero starts at 0).
                    saved = 0;
                    while (saved < Beats.Length * 2 && Reached(saved, cleared, guideDone) && cleared > 0) saved++;
                }
                _shown = saved;
                Save();
            }
            // Not over another screen, a replay (the win's banner comes first) or the guide.
            bool busy = _root.Replaying || _root.PushBusy || _root.Title.Showing || _root.Account.Showing || _root.Oath.Showing
                        || _root.Characters.IsOpen || _root.Tutorial.Running || server.WaitingForHero;
            if (busy || _shown >= Beats.Length * 2 || !Reached(_shown, cleared, guideDone)) return;
            Show(_shown);
        }

        /// <summary>Screenshots: -story n shows beat n (0 the Oathfields opening, 1 Greyjaw falling, ...).</summary>
        public void ShowForShot(int beat) => Show(Mathf.Clamp(beat, 0, Beats.Length * 2 - 1));
    }
}
