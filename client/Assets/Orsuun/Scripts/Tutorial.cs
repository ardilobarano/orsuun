using System;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The first-session guide: the screen dims around one part of it, a gold frame and a pointer show it, and a short
    /// note explains it, step by step (the hunt, the Korstone, skills, the Forge and a first attempt, Gear, Push, the
    /// bottom bar, the goal line). Some steps wait for the player to do the thing; the rest have NEXT. SKIP ends it;
    /// MENU → HOW TO PLAY runs it again. It never blocks the screen: only the note box takes taps.
    /// </summary>
    public sealed class Tutorial : MonoBehaviour
    {
        private const string DoneKey = "orsuun.tutorialDone";
        /// <summary>Over the lane: the note sits on the skills while the frame shows the hunt above.</summary>
        private const float BoxLow = 0.27f;
        private const float BoxOverLane = 0.50f;
        private const float BoxHigh = 0.62f;
        private const float BoxHeight = 0.17f;
        /// <summary>The pointer's size in canvas units (1080 x 1920 reference).</summary>
        private const float PointerSize = 84f;

        private sealed class Step
        {
            public string Title;
            public string Text;
            public Func<Rect> Frame;
            public float BoxY;
            /// <summary>Set for steps the player finishes by doing it; they have no NEXT.</summary>
            public Func<bool> Done;
            public bool AllowNext = true;
            /// <summary>Shown over the Forge screen (other steps hide while a screen covers the hunt).</summary>
            public bool OnForge;
            /// <summary>The step about the goal line: the HUD shows it even while the guide runs.</summary>
            public bool Goal;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Image[] _shade;
        private RectTransform _frame;
        private Image _frameImage;
        private Image _glow;
        private RectTransform _pointer;
        private Image _pointerImage;
        private RectTransform _box;
        private Text _title;
        private Text _text;
        private Text _count;
        private GameObject _next;
        private Text _nextLabel;
        private Step[] _steps;
        private int _index = -1;
        private bool _sawForgeBusy;
        private float _shownAt;

        public bool Running => _index >= 0;

        /// <summary>True while the guide is on the goal-line step (the HUD keeps the line up for it).</summary>
        public bool ShowsGoal => Running && _steps[_index].Goal;

        public static bool Finished
        {
            get { try { return PlayerPrefs.GetInt(DoneKey, 0) == 1; } catch { return false; } }
        }

        public static void Reset()
        {
            try { PlayerPrefs.DeleteKey(DoneKey); } catch { }
        }

        public void Init(GameRoot root)
        {
            _root = root;
            // This component stays off the canvas: its Update must run while the canvas is hidden.
            _canvas = Ui.Canvas("TutorialCanvas", 13).gameObject;
            Transform canvas = _canvas.transform;

            // The spotlight: four shades around the framed area. They take no taps, so the whole screen stays live.
            _shade = new Image[4];
            for (int i = 0; i < _shade.Length; i++)
            {
                _shade[i] = Ui.Panel("Shade" + i, canvas, 0f, 0f, 1f, 1f, new Color(0.01f, 0.01f, 0.03f, 0.55f));
                _shade[i].raycastTarget = false;
            }

            _glow = Ui.Sliced("Glow", canvas, 0f, 0f, 1f, 1f, "Glow", Palette.Sorn);
            _glow.raycastTarget = false;
            _frameImage = Ui.Sliced("Frame", canvas, 0f, 0f, 1f, 1f, "SlotRim", Color.white);
            _frameImage.raycastTarget = false;
            _frame = _frameImage.rectTransform;

            _pointerImage = Ui.Sliced("Pointer", canvas, 0.5f, 0.5f, 0.5f, 0.5f, "Pointer", Color.white);
            _pointerImage.raycastTarget = false;
            _pointer = _pointerImage.rectTransform;
            _pointer.sizeDelta = new Vector2(PointerSize, PointerSize);

            Image box = Ui.Framed("Box", canvas, 0.04f, 0f, 0.96f, BoxHeight, new Color(0.07f, 0.07f, 0.12f, 0.97f));
            _box = box.rectTransform;
            _title = Ui.Title("Title", _box, 0.05f, 0.75f, 0.95f, 0.95f, "", 30, TextAnchor.MiddleLeft, Palette.Sorn);
            Ui.Sliced("TitleRule", _box, 0.04f, 0.71f, 0.96f, 0.76f, "Rule", Color.white).raycastTarget = false;
            _text = Ui.Label("Text", _box, 0.05f, 0.31f, 0.95f, 0.71f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
            _count = Ui.Title("Count", _box, 0.36f, 0.05f, 0.64f, 0.28f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Skip", _box, 0.03f, 0.05f, 0.30f, 0.29f, "SKIP", 26, Palette.ButtonIdle, () => Finish(skipped: true), out _);
            _next = Ui.Button("Next", _box, 0.70f, 0.05f, 0.97f, 0.29f, "NEXT", 28, Palette.ButtonForge, Next, out _nextLabel).gameObject;

            Rect Fixed(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);
            _steps = new[]
            {
                new Step { Title = "THE HUNT", Text = "Your hero hunts on their own: packs fall and loot drops, even while you are away.",
                           Frame = () => Fixed(0.01f, GameRoot.LaneViewportBottom + 0.01f, 0.99f, 0.935f), BoxY = BoxLow },
                new Step { Title = "KORSTONES", Text = "After a few packs a Korstone rises: the top line counts them. Break it for the best drops. The deeper you hunt, the darker and deadlier the stones.",
                           Frame = () => _root.Hud.Area("Stage", "Link"), BoxY = BoxHigh },
                new Step { Title = "SKILLS", Text = "Skills cast themselves while AUTO is on. Tap a skill to fire it the moment you want: aimed, it hits harder.",
                           Frame = () => _root.Hud.Area("Skill0", "Skill4", "Auto0", "Auto4"), BoxY = BoxOverLane },
                new Step { Title = "THE FORGE", Text = "The Forge raises your gear's level and its power. Tap FORGE.",
                           Frame = () => _root.Hud.Area("Forge"), BoxY = BoxOverLane, Done = () => _root.Forge.IsOpen, AllowNext = false },
                new Step { Title = "FIRST STRIKE", Text = "Every try shows its chance and cost and asks first. Up to +3 a failure costs a level; from +4 a failed FORGE ALONE destroys the piece, and a Scroll of Mercy keeps it safe. Try once.",
                           Frame = () => _root.Forge.Area("AttemptBack", "Method0", "Method2"), BoxY = 0.56f, OnForge = true,
                           // Done after one attempt, or when the Forge is closed without one (the next step then passes too).
                           Done = () => { _sawForgeBusy |= _root.Forge.Busy; return (_sawForgeBusy && !_root.Forge.Busy) || !_root.Forge.IsOpen; } },
                new Step { Title = "WELL STRUCK", Text = "Win or lose, the Forge is always there. Tap BACK TO THE HUNT.",
                           Frame = () => _root.Forge.Area("Close"), BoxY = 0.56f, OnForge = true,
                           Done = () => !_root.Forge.IsOpen, AllowNext = false },
                new Step { Title = "GEAR", Text = "New pieces drop into your bag: wear them from GEAR. Forge or turn any piece there, worn or in the bag.",
                           Frame = () => _root.Hud.Area("Gear"), BoxY = BoxOverLane },
                new Step { Title = "PUSH", Text = "PUSH takes the next stage when you are strong enough, and your hunt moves on with it. Each clear opens new hunting grounds in ZONES.",
                           Frame = () => _root.Hud.Area("Push"), BoxY = BoxOverLane },
                new Step { Title = "THE STEPPE", Text = "These open as you level: WAR for your Banner, BOUNTIES for Hunt Marks, GUILD, TRADE on the Salt Exchange. MENU holds sound, your account and this guide.",
                           Frame = () => _root.Hud.Area("Zones", "Menu"), BoxY = 0.125f },
                new Step { Title = "NEXT GOAL", Text = "This line always shows your next goal. Tap it to go there. Good hunting!",
                           Frame = () => _root.Hud.Area("Goal"), BoxY = BoxOverLane, Goal = true },
            };
            _canvas.SetActive(false);
        }

        public void Begin(int step = 0)
        {
            _index = Mathf.Clamp(step, 0, _steps.Length - 1);
            Show();
        }

        private void Next()
        {
            if (!Running) return;
            _index++;
            if (_index >= _steps.Length) Finish(skipped: false);
            else Show();
        }

        private void Finish(bool skipped)
        {
            _root.Server.Milestone(skipped ? "tutorial-skipped" : "tutorial-done");
            _index = -1;
            _canvas.SetActive(false);
            try { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); } catch { }
        }

        private void Show()
        {
            Step step = _steps[_index];
            _sawForgeBusy = false;
            _shownAt = Time.unscaledTime;
            _root.Server.Milestone("tutorial-" + (_index + 1));
            _title.text = step.Title;
            _text.text = step.Text;
            _count.text = $"{_index + 1} / {_steps.Length}";
            _next.SetActive(step.AllowNext);
            _nextLabel.text = _index == _steps.Length - 1 ? "DONE" : "NEXT";
            _box.anchorMin = new Vector2(0.04f, step.BoxY);
            _box.anchorMax = new Vector2(0.96f, step.BoxY + BoxHeight);
        }

        private void Update()
        {
            if (!Running) return;
            Step step = _steps[_index];
            if (step.Done != null && step.Done())
            {
                Next();
                if (!Running) return;
                step = _steps[_index];
            }

            // Hidden while another screen covers the hunt, unless the step is about that screen.
            bool covered = _root.Title.Showing || _root.Gear.IsOpen || _root.Zones.IsOpen || _root.Sockets.IsOpen || _root.Menu.IsOpen || _root.TurnHelper.IsOpen
                           || _root.War.IsOpen || _root.Bounties.IsOpen || _root.Oath.Showing || _root.Guild.IsOpen || _root.GuildWar.IsOpen || _root.Pits.IsOpen || _root.Caravan.IsOpen || _root.Trail.IsOpen || _root.Trade.IsOpen || _root.Wardrobe.IsOpen || _root.Characters.IsOpen || _root.Depot.IsOpen || _root.Smith.IsOpen || _root.RuneLock.IsOpen || _root.Market.IsOpen
                           || _root.Chat.IsOpen || _root.Account.Showing
                           || (_root.Forge.IsOpen && !step.OnForge) || (!_root.Forge.IsOpen && step.OnForge);
            if (_canvas.activeSelf == covered) _canvas.SetActive(!covered);
            if (covered) return;

            Rect area = step.Frame();
            Place(area);
        }

        /// <summary>Lays the shades, the frame, its glow and the pointer around one area (canvas anchors).</summary>
        private void Place(Rect area)
        {
            float t = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            // The shades fade in over the first moment of a step so the change of focus reads.
            float fade = Mathf.Clamp01((t - _shownAt) / 0.25f);

            SetAnchors(_shade[0].rectTransform, 0f, area.yMax, 1f, 1f);
            SetAnchors(_shade[1].rectTransform, 0f, 0f, 1f, area.yMin);
            SetAnchors(_shade[2].rectTransform, 0f, area.yMin, area.xMin, area.yMax);
            SetAnchors(_shade[3].rectTransform, area.xMax, area.yMin, 1f, area.yMax);
            foreach (Image shade in _shade) shade.color = new Color(0.01f, 0.01f, 0.03f, 0.55f * fade);

            SetAnchors(_frame, area.xMin, area.yMin, area.xMax, area.yMax);
            float grow = 6f + 4f * pulse;
            _frame.offsetMin = new Vector2(-grow, -grow);
            _frame.offsetMax = new Vector2(grow, grow);
            _frameImage.color = Color.Lerp(new Color(1f, 0.9f, 0.65f), Color.white, pulse);

            SetAnchors(_glow.rectTransform, area.xMin, area.yMin, area.xMax, area.yMax);
            _glow.rectTransform.offsetMin = new Vector2(-46f, -46f);
            _glow.rectTransform.offsetMax = new Vector2(46f, 46f);
            Color glow = Palette.Sorn;
            glow.a = 0.18f + 0.3f * pulse;
            _glow.color = glow;

            // The pointer stands between the note and the frame, its tip on the frame's edge, bobbing toward it.
            float boxMid = (_box.anchorMin.y + _box.anchorMax.y) * 0.5f;
            bool below = boxMid < area.center.y;
            float bob = 10f * Mathf.Abs(Mathf.Sin(t * 3.2f));
            _pointer.anchorMin = _pointer.anchorMax = new Vector2(area.center.x, below ? area.yMin : area.yMax);
            _pointer.pivot = new Vector2(0.5f, 0f);
            // The sprite points down; below the frame it turns to point up. The pivot sits at the tip after the turn.
            _pointer.localRotation = Quaternion.Euler(0f, 0f, below ? 180f : 0f);
            _pointer.anchoredPosition = new Vector2(0f, below ? -(12f + bob) : 12f + bob);
            // A frame that fills the lane has no room above or below for the pointer: it hides.
            _pointerImage.enabled = area.height < 0.3f;
        }

        private static void SetAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
