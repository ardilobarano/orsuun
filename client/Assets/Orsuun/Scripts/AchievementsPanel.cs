using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// ACHIEVEMENTS (owner, 27 Sep 2026: "Titles & achievements"; Rules.Achievements): every feat with its progress, the
    /// done ones first. Tapping a done one claims its Honor and sorn (CLAIM ALL claims them all); the greatest give a title,
    /// and TITLE cycles through those claimed (and none): the title shows before the hero's name in chat and on the Pits'
    /// boards. Opened from MENU, whose tile carries a badge while something waits to be claimed.
    /// </summary>
    public sealed class AchievementsPanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private RectTransform _content;
        private Text _titleLabel, _summary, _message;
        private bool _busy;
        private string _listKey = "";

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("AchievementsCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Gate");
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "ACHIEVEMENTS", 46, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _summary = Ui.Label("Summary", canvas, 0.05f, 0.885f, 0.95f, 0.915f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Title", canvas, 0.1f, 0.825f, 0.9f, 0.875f, "", 24, Palette.Alloy, NextTitle, out _titleLabel);
            Ui.Scroll("Feats", canvas, 0.03f, 0.19f, 0.97f, 0.815f, out _content);
            Ui.Button("ClaimAll", canvas, 0.2f, 0.1f, 0.8f, 0.165f, "CLAIM ALL", 30, Palette.ButtonForge, ClaimAll, out _);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.1f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Offline: achievements need the server.";
            _listKey = "";
            if (_root.Server.Online && !_busy)
            {
                _busy = true;
                StartCoroutine(_root.Server.FetchAchievements(error =>
                {
                    _busy = false;
                    if (error != null) _message.text = error;
                }));
            }
        }

        public void Close() => _canvas.SetActive(false);

        private void Claim(int id)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.ClaimAchievement(id, error =>
            {
                _busy = false;
                _message.text = error ?? _root.Server.Achievements?.message ?? "";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.8f, 0.1f, 0f);
            }));
        }

        /// <summary>Claims every done achievement, one after another.</summary>
        private void ClaimAll()
        {
            if (_busy || !_root.Server.Online || _root.Server.Achievements?.list == null) return;
            var ready = new List<int>();
            foreach (AchievementDto a in _root.Server.Achievements.list)
                if (a.done && !a.claimed) ready.Add(a.id);
            if (ready.Count == 0)
            {
                _message.text = "Nothing to claim yet.";
                return;
            }
            StartCoroutine(ClaimEach(ready));
        }

        private System.Collections.IEnumerator ClaimEach(List<int> ids)
        {
            _busy = true;
            string failure = null;
            foreach (int id in ids)
            {
                yield return _root.Server.ClaimAchievement(id, error => failure = error);
                if (failure != null) break;
            }
            _busy = false;
            _message.text = failure ?? (ids.Count == 1 ? _root.Server.Achievements?.message ?? "" : $"Claimed {ids.Count} achievements.");
            if (failure == null) GameAudio.Instance?.Play("LaneLoot", 0.8f, 0.1f, 0f);
        }

        /// <summary>The next claimed title, then none, then round again.</summary>
        private void NextTitle()
        {
            AchievementsDto view = _root.Server.Achievements;
            if (_busy || !_root.Server.Online || view?.list == null) return;
            var titles = new List<int> { 0 };
            foreach (AchievementDto a in view.list)
                if (a.claimed && !string.IsNullOrEmpty(a.title)) titles.Add(a.id);
            if (titles.Count == 1)
            {
                _message.text = "Claim an achievement that gives a title first.";
                return;
            }
            int next = titles[(titles.IndexOf(view.titleId) + 1) % titles.Count];
            _busy = true;
            StartCoroutine(_root.Server.WearTitle(next, error =>
            {
                _busy = false;
                _message.text = error ?? _root.Server.Achievements?.message ?? "";
            }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            AchievementsDto view = _root.Server.Achievements;
            AchievementDto[] list = view?.list ?? new AchievementDto[0];
            int claimed = 0;
            foreach (AchievementDto a in list) if (a.claimed) claimed++;
            _summary.text = list.Length == 0 ? "" : $"{claimed} of {list.Length} claimed";
            _titleLabel.text = view == null ? "TITLE" : string.IsNullOrEmpty(view.title) ? "TITLE: NONE" : $"TITLE: {Loc.T(view.title).ToUpperInvariant()}";

            string key = view == null ? "" : view.titleId + ":";
            foreach (AchievementDto a in list) key += a.id + (a.claimed ? "c" : a.done ? "d" : a.progress.ToString(CultureInfo.InvariantCulture)) + ";";
            if (key == _listKey) return;
            _listKey = key;
            foreach (Transform child in _content) Destroy(child.gameObject);
            // Ready to claim first, then what is under way, then what is claimed.
            foreach (int pass in new[] { 0, 1, 2 })
                foreach (AchievementDto achievement in list)
                {
                    AchievementDto a = achievement;
                    int group = a.done && !a.claimed ? 0 : a.claimed ? 2 : 1;
                    if (group != pass) continue;
                    Ui.ListRow("Feat", _content, 24, () => { if (a.done && !a.claimed) Claim(a.id); }).text = Row(a);
                }
        }

        private static string Row(AchievementDto a)
        {
            string state = a.claimed ? ConfirmDialog.Tint("CLAIMED", Palette.Muted)
                : a.done ? ConfirmDialog.Tint("DONE: TAP TO CLAIM", Palette.Good)
                : ConfirmDialog.Tint($"{Count(a.progress)} / {Count(a.target)}", Palette.Parchment);
            string reward = $"+{a.honor} Honor  ·  +{a.sorn.ToString("N0", CultureInfo.InvariantCulture)} sorn"
                            + (string.IsNullOrEmpty(a.title) ? "" : "  ·  " + ConfirmDialog.Tint("title: " + a.title, Palette.Sorn));
            string name = a.claimed ? $"<color=#B9B3A8>{a.name}</color>" : $"<color=#FFD66B>{a.name}</color>";
            return $"<b>{name}</b>   <size=20>{state}</size>\n<size=20><color=#D8CFBF>{a.text}</color></size>   <size=18><color=#B9B3A8>{reward}</color></size>";
        }

        private static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
    }
}
