using System;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The first-session guide: a pulsing frame around one part of the screen and a short note, step by step (the
    /// hunt, the Korstone, skills, then the Forge, a first attempt, Gear and Push). Some steps wait for the player to
    /// do the thing; the rest have NEXT. SKIP ends it; MENU → HOW TO PLAY runs it again. It never blocks the screen:
    /// only the note box takes taps.
    /// </summary>
    public sealed class Tutorial : MonoBehaviour
    {
        private const string DoneKey = "orsuun.tutorialDone";
        /// <summary>Over the lane: the note sits on the skills while the frame shows the hunt above.</summary>
        private const float BoxLow = 0.285f;
        private const float BoxOverLane = 0.50f;
        private const float BoxHigh = 0.64f;

        private sealed class Step
        {
            public string Text;
            public Func<Rect> Frame;
            public float BoxY;
            /// <summary>Set for steps the player finishes by doing it; they have no NEXT.</summary>
            public Func<bool> Done;
            public bool AllowNext = true;
            /// <summary>Shown over the Forge screen (other steps hide while a screen covers the hunt).</summary>
            public bool OnForge;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private RectTransform _frame;
        private Image[] _bars;
        private RectTransform _box;
        private Text _text;
        private Text _count;
        private GameObject _next;
        private Text _nextLabel;
        private Step[] _steps;
        private int _index = -1;
        private bool _sawForgeBusy;

        public bool Running => _index >= 0;

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

            _frame = Ui.Rect("Frame", canvas, 0f, 0f, 1f, 1f);
            _bars = new[]
            {
                Bar("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(6f, 6f)),
                Bar("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-6f, -6f), new Vector2(6f, 0f)),
                Bar("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-6f, -6f), new Vector2(0f, 6f)),
                Bar("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(6f, 6f)),
            };

            Image box = Ui.Framed("Box", canvas, 0.04f, 0f, 0.96f, 0.15f, new Color(0.07f, 0.07f, 0.12f, 0.96f));
            _box = box.rectTransform;
            Ui.Trim("TopRule", _box, 0f, 0.98f, 1f, 1f);
            _text = Ui.Label("Text", _box, 0.04f, 0.36f, 0.96f, 0.95f, "", 32, TextAnchor.MiddleLeft, Palette.Parchment);
            _count = Ui.Label("Count", _box, 0.36f, 0.05f, 0.64f, 0.32f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Skip", _box, 0.03f, 0.06f, 0.30f, 0.32f, "SKIP", 26, Palette.ButtonIdle, Finish, out _);
            _next = Ui.Button("Next", _box, 0.70f, 0.06f, 0.97f, 0.32f, "NEXT", 28, Palette.ButtonForge, Next, out _nextLabel).gameObject;

            Rect Fixed(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);
            _steps = new[]
            {
                new Step { Text = "Your hero hunts on their own: packs fall and loot drops, even while you are away.",
                           Frame = () => Fixed(0.01f, GameRoot.LaneViewportBottom + 0.01f, 0.99f, 0.935f), BoxY = BoxLow },
                new Step { Text = "Every wave ends at a Korstone. Break it for the best drops. The deeper you hunt, the darker and deadlier the stones.",
                           Frame = () => _root.Hud.Area("Stage", "Link"), BoxY = BoxHigh },
                new Step { Text = "Skills cast themselves while AUTO is on. Tap a skill to fire it the moment you want.",
                           Frame = () => _root.Hud.Area("Skill0", "Skill2", "Auto0", "Auto2"), BoxY = BoxOverLane },
                new Step { Text = "The Forge raises your gear's level and its power. Tap FORGE.",
                           Frame = () => _root.Hud.Area("Forge"), BoxY = BoxOverLane, Done = () => _root.Forge.IsOpen, AllowNext = false },
                new Step { Text = "Every try shows its chance and cost and asks first. Up to +3 a failure costs a level; from +4 a failed FORGE ALONE destroys the piece, and a Scroll of Mercy keeps it safe. Try once.",
                           Frame = () => Fixed(0.04f, 0.25f, 0.97f, 0.375f), BoxY = 0.58f, OnForge = true,
                           // Done after one attempt, or when the Forge is closed without one (the next step then passes too).
                           Done = () => { _sawForgeBusy |= _root.Forge.Busy; return (_sawForgeBusy && !_root.Forge.Busy) || !_root.Forge.IsOpen; } },
                new Step { Text = "Well struck. Tap BACK TO THE HUNT.",
                           Frame = () => Fixed(0.25f, 0.015f, 0.75f, 0.075f), BoxY = 0.58f, OnForge = true,
                           Done = () => !_root.Forge.IsOpen, AllowNext = false },
                new Step { Text = "GEAR holds everything you own. Forge or turn any piece from there, worn or in the bag.",
                           Frame = () => _root.Hud.Area("Gear"), BoxY = BoxOverLane },
                new Step { Text = "PUSH takes the next stage when you are strong enough; ZONES moves your hunt anywhere you have opened. Good hunting!",
                           Frame = () => _root.Hud.Area("Push"), BoxY = BoxOverLane },
            };
            _canvas.SetActive(false);
        }

        private Image Bar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Image bar = Ui.Panel(name, _frame, 0f, 0f, 1f, 1f, Palette.Sorn);
            RectTransform r = bar.rectTransform;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
            bar.raycastTarget = false;
            return bar;
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
            if (_index >= _steps.Length) Finish();
            else Show();
        }

        private void Finish()
        {
            _index = -1;
            _canvas.SetActive(false);
            try { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); } catch { }
        }

        private void Show()
        {
            Step step = _steps[_index];
            _sawForgeBusy = false;
            _text.text = step.Text;
            _count.text = $"{_index + 1} / {_steps.Length}";
            _next.SetActive(step.AllowNext);
            _nextLabel.text = _index == _steps.Length - 1 ? "DONE" : "NEXT";
            _box.anchorMin = new Vector2(0.04f, step.BoxY);
            _box.anchorMax = new Vector2(0.96f, step.BoxY + 0.15f);
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
                           || (_root.Forge.IsOpen && !step.OnForge) || (!_root.Forge.IsOpen && step.OnForge);
            if (_canvas.activeSelf == covered) _canvas.SetActive(!covered);
            if (covered) return;

            Rect area = step.Frame();
            _frame.anchorMin = area.min;
            _frame.anchorMax = area.max;
            Color c = Palette.Sorn;
            c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            foreach (Image bar in _bars) bar.color = c;
        }
    }
}
