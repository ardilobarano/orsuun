using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// The game's languages (owner, 27 Sep 2026: "Turkish language", then "More languages"): English as written, and
    /// the others from Resources/Loc/&lt;code&gt;.txt (tr, de, pl, pt, ro), English and the translation on each line with a
    /// tab between. Translation happens only where text is
    /// shown (LocText): a string is looked up whole, then cut at rich-text tags, line breaks and separators ("·", two
    /// spaces, "|") and each piece looked up alone. A piece is found as it is, in capitals, or through a template whose
    /// {0}, {1} stand for anything ("Sold: {0}"); what a hole caught is translated in turn (item names, numbers pass), so
    /// the server's messages, letters and item names need no work on the server. Unknown text shows as it came.
    /// </summary>
    public static class Loc
    {
        public enum Lang { English, Turkish, German, Polish, Portuguese, Romanian }

        /// <summary>Each language's file name (and saved choice), and its name in its own words for the language list.</summary>
        private static readonly string[] Codes = { "en", "tr", "de", "pl", "pt", "ro" };
        public static readonly string[] Names = { "ENGLISH", "TÜRKÇE", "DEUTSCH", "POLSKI", "PORTUGUÊS", "ROMÂNĂ" };
        private static readonly SystemLanguage[] Systems =
            { SystemLanguage.English, SystemLanguage.Turkish, SystemLanguage.German, SystemLanguage.Polish, SystemLanguage.Portuguese, SystemLanguage.Romanian };

        private const string PrefKey = "orsuun.lang";
        private const int MaxDepth = 6;
        private const int MaxCache = 6000;

        public static Lang Current { get; private set; }
        public static bool Turkish => Current == Lang.Turkish;
        /// <summary>Raised when the language changes: every LocText translates itself again.</summary>
        public static event Action Changed;

        private sealed class Template
        {
            public Regex Pattern;
            public string To;
            public int Literal;
            /// <summary>The template's first two letters (its bucket), or "" when it starts with a hole.</summary>
            public string Key;
        }

        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Upper = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<Template>> Buckets = new Dictionary<string, List<Template>>();
        private static readonly List<Template> Open = new List<Template>();
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private static readonly Regex Cut = new Regex(@"(<[^>]*>|\n|\s*·\s*|\s{2,}|\s*\|\s*)", RegexOptions.CultureInvariant);
        private static readonly Regex Hole = new Regex(@"\{(\d+)(#?)\}", RegexOptions.CultureInvariant);
        private static readonly Regex NoWords = new Regex(@"^v?[\d\s.,:;%+\-×/()\[\]#'""!?›‹◆●$<>]*$", RegexOptions.CultureInvariant);
        private static readonly Regex Sentence = new Regex(@"(?<=[.!?])\s+(?=[A-Z0-9""(])", RegexOptions.CultureInvariant);
        private static Lang _loaded = Lang.English;
        /// <summary>A hole touching letters ("{0}m", "T{0}") and a marked one ("{0#}": a number or a time, "06:49").</summary>
        private const string Digits = "(-?[\\d.,]+)", Number = "(-?\\d[\\d.,:]*)";

        /// <summary>Pieces with words that found no translation (-locmiss: written out for the translator).</summary>
        public static readonly HashSet<string> Misses = new HashSet<string>();
        private static bool _recordMisses;

        /// <summary>The saved choice, else the phone's language; -lang &lt;code&gt; forces one (screenshots, not saved).</summary>
        public static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            int forced = Array.IndexOf(args, "-lang");
            string saved = forced >= 0 && forced + 1 < args.Length ? args[forced + 1] : PlayerPrefs.GetString(PrefKey, "");
            int index = Array.IndexOf(Codes, saved);
            if (index < 0) index = Math.Max(0, Array.IndexOf(Systems, Application.systemLanguage));
            Current = (Lang)index;
            _recordMisses = Array.IndexOf(args, "-locmiss") >= 0;
            Load(Current);
        }

        public static void Set(Lang lang)
        {
            if (lang == Current) return;
            Current = lang;
            PlayerPrefs.SetString(PrefKey, Codes[(int)lang]);
            PlayerPrefs.Save();
            Load(lang);
            Cache.Clear();
            Changed?.Invoke();
        }

        /// <summary>The current language's name in its own words.</summary>
        public static string Name => Names[(int)Current];

        /// <summary>The text in the language chosen.</summary>
        public static string T(string text)
        {
            if (Current == Lang.English || string.IsNullOrEmpty(text)) return text;
            if (Cache.TryGetValue(text, out string done)) return done;
            string result = Whole(text);
            if (Cache.Count > MaxCache) Cache.Clear();
            Cache[text] = result;
            return result;
        }

        private static string Whole(string text)
        {
            string trimmed = text.Trim();
            if (Exact.TryGetValue(trimmed, out string exact)) return Keep(text, trimmed, exact);
            if (!Cut.IsMatch(text)) return Keep(text, trimmed, Piece(trimmed, 0));
            // The pieces between tags, breaks and separators, each alone.
            var sb = new StringBuilder(text.Length + 16);
            foreach (string part in Cut.Split(text))
            {
                if (part.Length == 0) continue;
                if (Cut.IsMatch(part) && Cut.Match(part).Length == part.Length) { sb.Append(part); continue; }
                string core = part.Trim();
                sb.Append(core.Length == 0 ? part : Keep(part, core, Piece(core, 0)));
            }
            return sb.ToString();
        }

        /// <summary>The whitespace around a piece stays where it was.</summary>
        private static string Keep(string original, string core, string translated)
        {
            if (core.Length == original.Length) return translated;
            int start = original.IndexOf(core, StringComparison.Ordinal);
            return original.Substring(0, start) + translated + original.Substring(start + core.Length);
        }

        private static string Piece(string piece, int depth)
        {
            if (Find(piece, depth, out string done)) return done;
            if (_recordMisses) Misses.Add(piece);
            return done;
        }

        /// <summary>A piece's translation; false (with the best it could do) when some of its words found none.</summary>
        private static bool Find(string piece, int depth, out string done, bool split = true)
        {
            done = piece;
            if (piece.Length == 0 || NoWords.IsMatch(piece) || IsAddress(piece)) return true;
            if (Letters(piece) < 2)
            {
                // A unit or a code ("19m", "T3", "X"): a template may know it ("{0}m"), else it stays as it is.
                string none = null;
                if (Exact.TryGetValue(piece, out done) || depth < MaxDepth && Templates(Open, piece, depth, ref none, out done)) return true;
                done = piece;
                return true;
            }
            if (Exact.TryGetValue(piece, out done)) return true;
            if (IsCapitals(piece) && Upper.TryGetValue(piece, out done)) { done = ToUpper(done); return true; }
            done = piece;
            if (char.IsLower(piece[0]) && Exact.TryGetValue(char.ToUpperInvariant(piece[0]) + piece.Substring(1), out string lifted))
            {
                done = LowerFirst(lifted);
                return true;
            }
            if (depth >= MaxDepth) return false;
            // A template that fills every hole wins; else the first that matched at all, sentence by sentence first.
            string partial = null;
            string key = piece.Length >= 2 ? piece.Substring(0, 2) : piece;
            if (Buckets.TryGetValue(key, out List<Template> list) && Templates(list, piece, depth, ref partial, out done)) return true;
            string joined = null;
            if (split && Sentences(piece, depth, out joined)) { done = joined; return true; }
            partial ??= joined;
            if (Templates(Open, piece, depth, ref partial, out done)) return true;
            done = partial ?? piece;
            return false;
        }

        private static bool Templates(List<Template> list, string piece, int depth, ref string partial, out string done)
        {
            foreach (Template t in list)
            {
                Match m = t.Pattern.Match(piece);
                if (!m.Success) continue;
                bool all = true;
                string filled = Hole.Replace(t.To, h =>
                {
                    int i = int.Parse(h.Groups[1].Value) + 1;
                    if (i >= m.Groups.Count) return "";
                    string caught = m.Groups[i].Value.Trim();
                    all &= Find(caught, depth + 1, out string part) || NameLike(caught);
                    return part;
                });
                if (all) { done = filled; return true; }
                partial ??= filled;
            }
            done = null;
            return false;
        }

        /// <summary>
        /// The code often joins sentences: each run of them is looked up, longest first, so a line written as two
        /// sentences ("... Wardens.") is still found whole. Null when the piece is one sentence.
        /// </summary>
        private static bool Sentences(string piece, int depth, out string joined)
        {
            joined = null;
            string[] sentences = Sentence.Split(piece);
            if (sentences.Length < 2) return false;
            var sb = new StringBuilder(piece.Length + 16);
            bool all = true;
            for (int i = 0; i < sentences.Length;)
            {
                int j = sentences.Length - (i == 0 ? 1 : 0);
                string part = null;
                for (; j > i; j--)
                    if (Find(string.Join(" ", sentences, i, j - i), depth + 1, out part, split: false)) break;
                if (j == i)
                {
                    all = false;
                    Find(sentences[i], depth + 1, out part, split: false);
                    j = i + 1;
                }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(part);
                i = j;
            }
            joined = sb.ToString();
            return all;
        }

        /// <summary>A hole that caught a name (a hero, a guild, "[SOR]") is filled even though no line knows it.</summary>
        private static bool NameLike(string s)
        {
            bool start = true;
            foreach (char c in s)
            {
                if (start && char.IsLower(c)) return false;
                start = c == ' ' || c == '-';
            }
            return true;
        }

        /// <summary>A web address or an email: shown as it is.</summary>
        private static bool IsAddress(string s) => (s.IndexOf("://", StringComparison.Ordinal) > 0 || s.IndexOf('@') > 0) && s.IndexOf(' ') < 0;

        private static int Letters(string s)
        {
            int n = 0;
            foreach (char c in s)
                if (char.IsLetter(c) && ++n >= 2) break;
            return n;
        }

        private static string LowerFirst(string s) => s.Length == 0 ? s
            : (Current == Lang.Turkish && s[0] == 'İ' ? "i" : Current == Lang.Turkish && s[0] == 'I' ? "ı" : char.ToLowerInvariant(s[0]).ToString()) + s.Substring(1);

        private static bool IsCapitals(string s)
        {
            bool letter = false;
            foreach (char c in s)
            {
                if (char.IsLower(c)) return false;
                letter |= char.IsLetter(c);
            }
            return letter;
        }

        /// <summary>Capitals in the current language (Turkish: i to İ, ı to I).</summary>
        public static string ToUpper(string s) =>
            Current == Lang.Turkish ? s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant() : s.ToUpperInvariant();

        private static void Load(Lang lang)
        {
            if (lang == _loaded) return;
            _loaded = lang;
            Exact.Clear();
            Upper.Clear();
            Buckets.Clear();
            Open.Clear();
            Cache.Clear();
            if (lang == Lang.English) return;
            var asset = Resources.Load<TextAsset>("Loc/" + Codes[(int)lang]);
            if (asset == null) return;
            var templates = new List<Template>();
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                string from = line.Substring(0, tab).Replace("\\n", "\n").Trim(), to = line.Substring(tab + 1).Replace("\\n", "\n");
                if (from.IndexOf('{') >= 0 && Hole.IsMatch(from))
                {
                    templates.Add(Compile(from, to));
                    continue;
                }
                Exact[from] = to;
                string caps = from.ToUpperInvariant();
                if (!Upper.ContainsKey(caps)) Upper[caps] = to;
            }
            // Longer fixed words first: "Sold: {0}" before "{0}: {1}".
            templates.Sort((a, b) => b.Literal.CompareTo(a.Literal));
            foreach (Template t in templates)
            {
                if (t.Key.Length >= 2)
                {
                    if (!Buckets.TryGetValue(t.Key, out List<Template> list)) Buckets[t.Key] = list = new List<Template>();
                    list.Add(t);
                }
                else Open.Add(t);
            }
        }

        private static Template Compile(string from, string to)
        {
            var pattern = new StringBuilder("^");
            int at = 0, literal = 0;
            foreach (Match h in Hole.Matches(from))
            {
                string text = from.Substring(at, h.Index - at);
                pattern.Append(Regex.Escape(text)).Append(h.Groups[2].Length > 0 ? Number : HolePattern(from, h));
                literal += text.Length;
                at = h.Index + h.Length;
            }
            string tail = from.Substring(at);
            pattern.Append(Regex.Escape(tail)).Append('$');
            literal += tail.Length;
            // Holes are numbered as in the English; a template's groups come in order of appearance.
            var order = new List<int>();
            foreach (Match h in Hole.Matches(from)) order.Add(int.Parse(h.Groups[1].Value));
            string remapped = Hole.Replace(to, h =>
            {
                int n = int.Parse(h.Groups[1].Value);
                int place = order.IndexOf(n);
                return place < 0 ? "" : "{" + place + "}";
            });
            string lead = from.IndexOf('{') >= 2 ? from.Substring(0, 2) : "";
            return new Template { Pattern = new Regex(pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline), To = remapped, Literal = literal, Key = lead };
        }

        /// <summary>
        /// What a hole may catch. Touching letters it is a number ("{0}m", "{0}d {1}h", "T{0}", "x{0}") or, after a whole
        /// word, a plural's "s" ("player{1}"); a plural after the hole ("the {0}s") and holes between spaces catch anything.
        /// </summary>
        private static string HolePattern(string from, Match h)
        {
            char before = h.Index > 0 ? from[h.Index - 1] : ' ';
            int end = h.Index + h.Length;
            char after = end < from.Length ? from[end] : ' ';
            char afterNext = end + 1 < from.Length ? from[end + 1] : ' ';
            if (char.IsLetter(after) && !(after == 's' && !char.IsLetter(afterNext))) return Digits;
            if (char.IsLetter(before))
            {
                int word = h.Index - 1;
                while (word > 0 && char.IsLetter(from[word - 1])) word--;
                return h.Index - word == 1 ? Digits : "(s?)";
            }
            return "(.+?)";
        }
    }
}
