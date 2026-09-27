using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// A Text that shows what it is given in the chosen language (Loc.T) and translates itself again when the language
    /// changes. Source keeps what the code set; Raw leaves players' own words (chat, messages, typed input) as written.
    /// </summary>
    public sealed class LocText : Text
    {
        private string _source;
        private Loc.Lang _shownIn;

        /// <summary>Never translated: players' words.</summary>
        public bool Raw;

        /// <summary>The text as the code set it, before translation.</summary>
        public string Source => _source ?? base.text;

        public override string text
        {
            get => base.text;
            set
            {
                _source = value;
                _shownIn = Loc.Current;
                base.text = Raw ? value : Loc.T(value);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Loc.Changed += Retranslate;
            if (_shownIn != Loc.Current) Retranslate();
        }

        protected override void OnDisable()
        {
            Loc.Changed -= Retranslate;
            base.OnDisable();
        }

        private void Retranslate()
        {
            _shownIn = Loc.Current;
            if (_source != null && !Raw) base.text = Loc.T(_source);
        }
    }
}
