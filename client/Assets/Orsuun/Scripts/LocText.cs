using System.Text.RegularExpressions;
using UnityEngine;
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
                // The same words again (screens set their labels every frame): nothing to translate or draw anew.
                if (_source != null && _shownIn == Loc.Current && string.Equals(value, _source, System.StringComparison.Ordinal)) return;
                _source = value;
                _shownIn = Loc.Current;
                base.text = ScaleTags(Raw ? value : Loc.T(value));
            }
        }

        /// <summary>The size Ui gave it (0: none, the size is left alone), and the text scale it was last drawn at.</summary>
        private int _baseSize, _baseMax, _setSize, _setMax;
        private float _scaledFor = 1f;

        private static readonly Regex SizeTag = new Regex(@"<size=(\d+)>", RegexOptions.CultureInvariant);

        /// <summary>A line's own smaller or larger parts (&lt;size=n&gt;) grow with the text size too.</summary>
        private static string ScaleTags(string s)
        {
            float k = GameSettings.TextScale;
            if (Mathf.Approximately(k, 1f) || string.IsNullOrEmpty(s) || s.IndexOf("<size=", System.StringComparison.Ordinal) < 0) return s;
            return SizeTag.Replace(s, m => "<size=" + Mathf.RoundToInt(int.Parse(m.Groups[1].Value) * k) + ">");
        }

        /// <summary>Remembers the size set by Ui and draws it at the player's text size (SETTINGS: LARGE, LARGER).</summary>
        public void SetBaseSize(int size, int max)
        {
            _baseSize = size;
            _baseMax = max;
            _scaledFor = -1f;
            Rescale();
        }

        private void Rescale()
        {
            float scale = GameSettings.TextScale;
            if (_baseSize <= 0 || Mathf.Approximately(scale, _scaledFor)) return;
            // A size a screen set since (a tab's highlight, a bigger number) becomes the base.
            if (_scaledFor > 0f && fontSize != _setSize) _baseSize = Mathf.RoundToInt(fontSize / _scaledFor);
            if (_scaledFor > 0f && resizeTextMaxSize != _setMax) _baseMax = Mathf.RoundToInt(resizeTextMaxSize / _scaledFor);
            _scaledFor = scale;
            fontSize = _setSize = Mathf.RoundToInt(_baseSize * scale);
            resizeTextMaxSize = _setMax = Mathf.RoundToInt(_baseMax * scale);
            if (_source != null) Retranslate();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Loc.Changed += Retranslate;
            GameSettings.Changed += Rescale;
            if (_shownIn != Loc.Current) Retranslate();
            Rescale();
        }

        protected override void OnDisable()
        {
            Loc.Changed -= Retranslate;
            GameSettings.Changed -= Rescale;
            base.OnDisable();
        }

        private void Retranslate()
        {
            _shownIn = Loc.Current;
            if (_source != null) base.text = ScaleTags(Raw ? _source : Loc.T(_source));
        }
    }
}
