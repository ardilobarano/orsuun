using System;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The Carvers' rune lock on the Archive's third floor (Rules.Dungeons.RiddleFor; world bible: "puzzle-light"): a
    /// riddle carved in the vault door and three runes. The right one opens the vault (the Last Carver's chest then holds a
    /// Master's Needle); LEAVE IT SHUT walks on. The answer goes back to GameRoot, which asks the server and replays the
    /// rest of the run.
    /// </summary>
    public sealed class RuneLockPanel : MonoBehaviour
    {
        private GameObject _canvas;
        private Text _riddle;
        private readonly Button[] _runes = new Button[3];
        private readonly Text[] _runeLabels = new Text[3];
        private string[] _shown = new string[3];
        private Action<string> _answer;

        public bool IsOpen => _canvas.activeSelf;

        public void Init()
        {
            _canvas = Ui.Canvas("RuneLockCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.78f));
            Ui.Framed("Box", canvas, 0.04f, 0.22f, 0.96f, 0.78f, new Color(0.05f, 0.07f, 0.12f, 0.98f));
            Ui.Title("Title", canvas, 0.08f, 0.7f, 0.92f, 0.75f, "THE RUNE LOCK", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true, ribbon: false);
            Ui.Label("Flavour", canvas, 0.08f, 0.655f, 0.92f, 0.7f, "Carved in the vault door, under the Sky Banner's hawk:", 22, TextAnchor.MiddleCenter,
                Palette.Muted).fontStyle = FontStyle.Italic;
            _riddle = Ui.Title("Riddle", canvas, 0.1f, 0.52f, 0.9f, 0.65f, "", 30, TextAnchor.MiddleCenter, new Color(0.62f, 0.82f, 1f));
            _riddle.resizeTextForBestFit = true;
            for (int i = 0; i < _runes.Length; i++)
            {
                int index = i;
                float x0 = 0.08f + i * 0.285f;
                _runes[i] = Ui.Button("Rune" + i, canvas, x0, 0.39f, x0 + 0.265f, 0.48f, "", 30, Palette.Alloy, () => Answer(_shown[index]), out _runeLabels[i]);
            }
            Ui.Label("Note", canvas, 0.08f, 0.32f, 0.92f, 0.38f, "The right rune opens the vault: the Last Carver's chest then holds a Master's Needle.", 20,
                TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Shut", canvas, 0.25f, 0.24f, 0.75f, 0.31f, "LEAVE IT SHUT", 26, Palette.ButtonIdle, () => Answer(""), out _);
            _canvas.SetActive(false);
        }

        /// <summary>Opens the lock of run <paramref name="runId"/>; <paramref name="answer"/> gets the rune chosen, or "" to walk on.</summary>
        public void Open(long runId, Action<string> answer)
        {
            var riddle = Dungeons.RiddleFor(runId);
            _riddle.text = "“" + riddle.Text + "”";
            _shown = riddle.Runes;
            for (int i = 0; i < _runes.Length; i++) _runeLabels[i].text = _shown[i].ToUpperInvariant();
            _answer = answer;
            _canvas.SetActive(true);
        }

        private void Answer(string rune)
        {
            _canvas.SetActive(false);
            Action<string> answer = _answer;
            _answer = null;
            answer?.Invoke(rune);
        }
    }
}
