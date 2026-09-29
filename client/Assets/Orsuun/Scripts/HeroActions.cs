using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// What can be done with another hero met in the world (owner, 29 Sep 2026: "Tap heroes on the map"): INSPECT, WHISPER,
    /// TRADE, ADD FRIEND and INVITE TO PARTY on a small card, the same wherever a hero is tapped (the town square, a big map's field, the full
    /// map). It stands on its own canvas over those screens and under INSPECT, which it opens.
    /// </summary>
    public sealed class HeroActions : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private Text _title;
        private string _id, _name;
        private System.Action<string> _report;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("HeroActionsCanvas", 34).gameObject;
            Transform canvas = _canvas.transform;
            Image dim = Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.55f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Transform box = Ui.Framed("Box", canvas, 0.12f, 0.33f, 0.88f, 0.67f, Palette.PanelDark).transform;
            _title = Ui.Raw(Ui.Title("Title", box, 0.05f, 0.83f, 0.95f, 0.97f, "", 28, TextAnchor.MiddleCenter, Palette.Sorn));
            Ui.Button("Inspect", box, 0.06f, 0.63f, 0.48f, 0.8f, "INSPECT", 22, Palette.Alloy, () => Act((id, _) => _root.Inspect.Open(id)), out _);
            Ui.Button("Whisper", box, 0.52f, 0.63f, 0.94f, 0.8f, "WHISPER", 22, Palette.Safe, () => Act((id, name) => _root.Messages.OpenWith(id, name)), out _);
            Ui.Button("Trade", box, 0.06f, 0.43f, 0.48f, 0.6f, "TRADE", 22, Palette.Alloy, () => Act((id, _) =>
                StartCoroutine(_root.Server.TradeInvite(null, error =>
                {
                    if (error != null) Report(error);
                    else _root.Trade.Open();
                }, id))), out _);
            Ui.Button("Friend", box, 0.52f, 0.43f, 0.94f, 0.6f, "ADD FRIEND", 20, Palette.Safe, () => Act((id, _) =>
                StartCoroutine(_root.Server.AddFriend(id, null, (message, error) => Report(error ?? message)))), out _);
            // A hunting party (Rules.Parties): friends and guildmates only; the server says so otherwise.
            Ui.Button("Party", box, 0.06f, 0.23f, 0.94f, 0.4f, "INVITE TO PARTY", 22, Palette.ButtonForge, () => Act((id, _) => _root.Party.Invite(id, Report)), out _);
            Ui.Button("Close", box, 0.3f, 0.04f, 0.7f, 0.19f, "CLOSE", 22, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        /// <summary>The card for a hero; answers (a friend asked, a trade refused) go to <paramref name="report"/>, else the lane's log.</summary>
        public void Show(string heroId, string heroName, System.Action<string> report = null)
        {
            if (string.IsNullOrEmpty(heroId)) return;
            if (!_root.Server.Online) { _root.Hud.Log("Meeting other heroes needs the server."); return; }
            _id = heroId;
            _name = heroName;
            _report = report;
            _title.text = heroName;
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        private void Act(System.Action<string, string> act)
        {
            Close();
            if (_id != null) act(_id, _name);
        }

        private void Report(string message)
        {
            if (_report != null) _report(message);
            else _root.Hud.Log(message);
        }
    }
}
