using System;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The character screen (owner, 25 Sep 2026: "character creation with name selection after signing up or logging in
    /// like metin2, total 4 char slots"). Shown after signing in, before the hunt: the chosen character stands large
    /// (HeroStage) over four slot cards; START plays it, DELETE asks for its name typed, an empty slot opens CREATE (a
    /// class and a name). Amber and the Banner belong to the account; the depot is shared by all four.
    /// </summary>
    public sealed class CharacterPanel : MonoBehaviour
    {
        private enum Mode { Select, Create, Delete }

        private static readonly HeroClass[] Classes = { HeroClass.Vanguard, HeroClass.Kestrel, HeroClass.Wraithsworn, HeroClass.Drumcaller };
        private static readonly string[] ClassBlurbs =
        {
            "Rider's armour and a glaive. Steady, hard to kill: the Banner's heavy horse, now on foot.",
            "Twin knives and speed. Quick blows and crits; her Heartseeker finds the seam in a Korstone.",
            "A void-cursed sword. The hardest-hitting skills and the fewest HP.",
            "Staff and storm drum. Lightning from the sky and a rhythm that never breaks.",
        };

        private sealed class SlotCard
        {
            public Image Back;
            public Text Name;
            public Text Line;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private HeroStage _stage;
        private Text _account;
        private Text _name;
        private Text _line;
        private Text _detail;
        private Text _message;
        private readonly SlotCard[] _slots = new SlotCard[Characters.MaxSlots];
        private GameObject _selectGroup;
        private GameObject _createGroup;
        private GameObject _deleteGroup;
        private Button _start;
        private Button _delete;
        private Button _create;
        private readonly Button[] _classButtons = new Button[4];
        private Text _classBlurb;
        private InputField _nameField;
        private InputField _deleteField;
        private Text _deletePrompt;
        private Mode _mode;
        private int _slot;
        private int _classIndex;
        private bool _busy;
        private int _lobbyGeneration = -1;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("CharacterCanvas", 30).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gate");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "CHOOSE YOUR HERO", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _account = Ui.Label("Account", canvas, 0.04f, 0.895f, 0.66f, 0.925f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Button("AccountButton", canvas, 0.68f, 0.893f, 0.96f, 0.927f, "ACCOUNT", 20, Palette.ButtonIdle, () => _root.Account.Open(), out _);

            RectTransform stage = Ui.Rect("Stage", canvas, 0.1f, 0.4f, 0.9f, 0.89f);
            _stage = new GameObject("HeroStage").AddComponent<HeroStage>();
            _stage.Init(stage);

            Ui.Framed("InfoBack", canvas, 0.04f, 0.315f, 0.96f, 0.405f, new Color(0.05f, 0.05f, 0.1f, 0.9f));
            _name = Ui.Title("Name", canvas, 0.07f, 0.365f, 0.93f, 0.4f, "", 34, TextAnchor.MiddleCenter, Palette.Sorn);
            _line = Ui.Label("Line", canvas, 0.07f, 0.34f, 0.93f, 0.366f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _detail = Ui.Label("Detail", canvas, 0.07f, 0.318f, 0.93f, 0.342f, "", 19, TextAnchor.MiddleCenter, Palette.Muted);

            // Select: START and DELETE, or CREATE on an empty slot.
            _selectGroup = Ui.Rect("SelectGroup", canvas, 0f, 0f, 1f, 1f).gameObject;
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                float x0 = 0.03f + i * 0.2375f;
                var c = new SlotCard();
                c.Back = Ui.Framed("Slot" + i, _selectGroup.transform, x0, 0.215f, x0 + 0.225f, 0.305f, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                c.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => Pick(index));
                c.Back.gameObject.AddComponent<Press>();
                c.Name = Ui.Title("Name", c.Back.transform, 0.05f, 0.45f, 0.95f, 0.9f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
                c.Line = Ui.Label("Line", c.Back.transform, 0.05f, 0.1f, 0.95f, 0.45f, "", 16, TextAnchor.MiddleCenter, Palette.Muted);
                _slots[i] = c;
            }

            _start = Ui.Button("Start", _selectGroup.transform, 0.1f, 0.115f, 0.66f, 0.195f, "START", 40, Palette.Danger, Play, out _);
            _delete = Ui.Button("Delete", _selectGroup.transform, 0.69f, 0.125f, 0.9f, 0.185f, "DELETE", 22, Palette.ButtonIdle, () => SetMode(Mode.Delete), out _);
            _create = Ui.Button("Create", _selectGroup.transform, 0.15f, 0.115f, 0.85f, 0.195f, "CREATE A HERO", 34, Palette.ButtonForge, () => SetMode(Mode.Create), out _);

            // Create: a class, a name.
            _createGroup = Ui.Rect("CreateGroup", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform cg = _createGroup.transform;
            for (int i = 0; i < Classes.Length; i++)
            {
                int index = i;
                float x0 = 0.03f + i * 0.2375f;
                _classButtons[i] = Ui.Button("Class" + i, cg, x0, 0.25f, x0 + 0.225f, 0.305f, Classes[i].ToString().ToUpperInvariant(), 17, Palette.ButtonIdle, () => _classIndex = index, out _);
            }
            _classBlurb = Ui.Label("Blurb", cg, 0.05f, 0.2f, 0.95f, 0.248f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
            _nameField = Ui.Input("NameField", cg, 0.1f, 0.14f, 0.9f, 0.195f, $"A name: {Characters.NameMin}-{Characters.NameMax} letters or digits", 28, Characters.NameMax);
            Ui.Button("DoCreate", cg, 0.1f, 0.075f, 0.62f, 0.132f, "CREATE", 32, Palette.ButtonForge, DoCreate, out _);
            Ui.Button("CancelCreate", cg, 0.65f, 0.075f, 0.9f, 0.132f, "BACK", 26, Palette.ButtonIdle, () => SetMode(Mode.Select), out _);

            // Delete: the name typed.
            _deleteGroup = Ui.Rect("DeleteGroup", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform dg = _deleteGroup.transform;
            _deletePrompt = Ui.Label("Prompt", dg, 0.05f, 0.2f, 0.95f, 0.245f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            _deleteField = Ui.Input("DeleteField", dg, 0.1f, 0.14f, 0.9f, 0.195f, "The hero's name", 28, 24);
            Ui.Button("DoDelete", dg, 0.1f, 0.075f, 0.62f, 0.132f, "DELETE FOREVER", 28, Palette.Danger, DoDelete, out _);
            Ui.Button("CancelDelete", dg, 0.65f, 0.075f, 0.9f, 0.132f, "BACK", 26, Palette.ButtonIdle, () => SetMode(Mode.Select), out _);

            _message = Ui.Label("Message", canvas, 0.05f, 0.03f, 0.95f, 0.07f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            SetMode(Mode.Select);
            _canvas.SetActive(false);
        }

        /// <summary>GameRoot calls this each frame: open while the device waits at the character screen.</summary>
        public void SetVisible(bool visible)
        {
            if (visible == _canvas.activeSelf) return;
            _canvas.SetActive(visible);
            _stage.gameObject.SetActive(visible);
            // -createhero opens the create view (screenshots).
            if (visible) { _message.text = ""; SetMode(Array.IndexOf(Environment.GetCommandLineArgs(), "-createhero") >= 0 ? Mode.Create : Mode.Select); }
        }

        private Net.ServerLink.CharacterSlotDto InSlot(int slot)
        {
            var lobby = _root.Server.Lobby;
            if (lobby?.characters == null) return null;
            foreach (var c in lobby.characters) if (c.slot == slot) return c;
            return null;
        }

        private void Pick(int slot)
        {
            if (_busy) return;
            _slot = slot;
            SetMode(Mode.Select);
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            _selectGroup.SetActive(mode == Mode.Select);
            _createGroup.SetActive(mode == Mode.Create);
            _deleteGroup.SetActive(mode == Mode.Delete);
            if (mode == Mode.Create) _nameField.text = "";
            if (mode == Mode.Delete) _deleteField.text = "";
        }

        private void Play()
        {
            var c = InSlot(_slot);
            if (_busy || c == null) return;
            _busy = true;
            _message.text = "";
            StartCoroutine(_root.Server.SelectCharacter(c.id, error =>
            {
                _busy = false;
                if (error != null) _message.text = error;
                else GameAudio.Instance?.Play("LaneLevelUp", 0.8f, 1f, 0f);
            }));
        }

        private void DoCreate()
        {
            if (_busy) return;
            string name = (_nameField.text ?? "").Trim();
            if (Characters.NameProblem(name) is string problem) { _message.text = problem; return; }
            _busy = true;
            StartCoroutine(_root.Server.CreateCharacter(name, Classes[_classIndex], _slot, (message, error) =>
            {
                _busy = false;
                _message.text = error ?? message ?? "";
                if (error == null) SetMode(Mode.Select);
            }));
        }

        private void DoDelete()
        {
            var c = InSlot(_slot);
            if (_busy || c == null) return;
            _busy = true;
            StartCoroutine(_root.Server.DeleteCharacter(c.id, _deleteField.text ?? "", (message, error) =>
            {
                _busy = false;
                _message.text = error ?? message ?? "";
                if (error == null) SetMode(Mode.Select);
            }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var server = _root.Server;
            var lobby = server.Lobby;
            // A new lobby (signed in again): start on the character played last.
            if (lobby != null && _lobbyGeneration != server.AccountGeneration)
            {
                _lobbyGeneration = server.AccountGeneration;
                _slot = 0;
                string latest = "";
                foreach (var ch in lobby.characters ?? new Net.ServerLink.CharacterSlotDto[0])
                    if (string.CompareOrdinal(ch.lastPlayedUtc, latest) > 0) { latest = ch.lastPlayedUtc; _slot = ch.slot; }
            }
            _account.text = lobby == null ? "Connecting..." : !string.IsNullOrEmpty(lobby.email) ? "Account: " + lobby.email
                : lobby.links > 0 ? "Account: saved with Google / Apple" : "Guest account: save it under ACCOUNT";

            for (int i = 0; i < _slots.Length; i++)
            {
                var c = InSlot(i);
                SlotCard card = _slots[i];
                card.Back.color = i == _slot ? new Color(0.32f, 0.2f, 0.08f, 0.97f) : new Color(0.07f, 0.07f, 0.13f, 0.95f);
                card.Name.text = c?.name ?? "EMPTY";
                card.Name.color = c == null ? Palette.Muted : Palette.Parchment;
                card.Line.text = c == null ? "tap to create" : $"Lv {c.level} {c.@class}";
            }

            var chosen = InSlot(_slot);
            if (_mode == Mode.Create)
            {
                HeroClass cls = Classes[_classIndex];
                _stage.Show(cls, 0, 0, null);
                _name.text = "A NEW HERO";
                _line.text = cls.ToString();
                _detail.text = "Sworn to the " + (server.Banner == Banner.None ? "Banner you choose next" : Banners.Def(server.Banner).Name);
                for (int i = 0; i < _classButtons.Length; i++) _classButtons[i].targetGraphic.color = i == _classIndex ? Palette.ButtonForge : Palette.ButtonIdle;
                _classBlurb.text = ClassBlurbs[_classIndex];
                return;
            }

            if (chosen == null)
            {
                _stage.Show(null, 0, 0, null);
                _name.text = lobby == null ? "" : "AN EMPTY SLOT";
                _line.text = lobby == null ? "" : $"{(lobby.characters?.Length ?? 0)} of {lobby.maxSlots} heroes";
                _detail.text = "Amber and the Banner are shared by the account's heroes; so is the depot.";
                _start.gameObject.SetActive(false);
                _delete.gameObject.SetActive(false);
                _create.gameObject.SetActive(lobby != null);
                if (_mode == Mode.Delete) SetMode(Mode.Select);
                return;
            }

            Enum.TryParse(chosen.@class, out HeroClass chosenClass);
            _stage.Show(chosenClass, chosen.armorBand, chosen.weaponBand, chosen.skin);
            _name.text = (string.IsNullOrEmpty(chosen.guildTag) ? "" : "[" + chosen.guildTag + "] ") + chosen.name;
            _line.text = $"Level {chosen.level} {chosen.@class}";
            _detail.text = chosen.highestStageCleared > 0 ? "Reached " + Content.StageName(Math.Min(Content.TotalStages, chosen.highestStageCleared + 1)) : "Fresh on the steppe";
            _start.gameObject.SetActive(true);
            _delete.gameObject.SetActive(true);
            _create.gameObject.SetActive(false);
            _start.interactable = !_busy && !chosen.banned;
            if (_mode == Mode.Delete) _deletePrompt.text = $"Type {chosen.name} to delete this hero and everything it holds.";
        }
    }
}
