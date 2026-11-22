using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Core.Translation
{
    /// <summary>
    /// Translates the mod's own spoken English into another language at the
    /// single speech output point (ScreenReaderOutput), so the mod's logic keeps
    /// working on English strings and no call site changes. Rules come from a
    /// text file (Spanish/ModStrings_es.txt):
    ///
    ///   English template => Spanish template
    ///   ~ english phrase => frase
    ///
    /// Holes: {name} = any text (shortest that fits), {#name} = a number. A rule
    /// must cover whole segments: it starts at the text start or after a
    /// separator (", " ". " ": " "; " "! " "? " " - " newline "("), and ends at the
    /// text end or before , . : ; ! ? ) " (" " - " or a newline. At each segment
    /// start the longest matching rule wins (on a tie, the one with more literal
    /// text); uncovered text is kept, then "~" phrase rules replace
    /// whole words in it (icon words like "gold" inside game text). Hole contents
    /// are translated recursively; a hole a rule starts with never crosses a separator. Only the first letter is matched without case,
    /// and the output's first letter takes the input's case. A single trailing
    /// period is ignored on both sides (help items arrive with periods stripped).
    /// Game text is already in the target language and matches no rule.
    /// </summary>
    public static class SpeechTranslator
    {
        private sealed class Rule
        {
            public Regex Regex;
            public List<string> HoleNames = new List<string>();
            public List<bool> HoleNumeric = new List<bool>();
            public string Output;
            public string FirstLiteral;      // literal the rule starts with, or null when it starts with a hole
            public List<string> Literals = new List<string>();
            public int LiteralLength;        // tie-break: the more specific rule wins
            public string English;
        }

        private sealed class Phrase
        {
            public Regex Regex;
            public string Output;
        }

        private static List<Rule> _rules;
        private static List<Phrase> _phrases;
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private const int CacheLimit = 4000;
        private const int MaxDepth = 3;

        private static readonly Regex SeparatorRe = new Regex(@"[,.:;!?]\s+|\n|\(|\s-\s", RegexOptions.Compiled);
        private static readonly Regex HoleRe = new Regex(@"\{(#?)([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
        private const string LeadingHole = @"((?:(?![,.:;!?]\s|\n|\(|\s-\s).)+?)";
        private const string EndBoundary = @"(?=$|[,.:;!?)\r\n]|(?<=[,.:;!?])\s|\s\(|\s-\s)";

        public static bool IsLoaded => _rules != null && _rules.Count > 0;
        public static int RuleCount => _rules?.Count ?? 0;

        public static void Load(string path)
        {
            var rules = new List<Rule>();
            var phrases = new List<Phrase>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int skipped = 0, lineNo = 0;
            _cache.Clear();
            if (!File.Exists(path))
            {
                MonsterTrainAccessibility.LogWarning($"Speech rules not found: {path}");
                _rules = rules; _phrases = phrases;
                return;
            }

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                bool phrase = line.StartsWith("~ ");
                if (phrase) line = line.Substring(2).Trim();
                int sep = line.IndexOf(" => ", StringComparison.Ordinal);
                if (sep <= 0) { skipped++; continue; }
                string en = line.Substring(0, sep).Trim();
                string es = line.Substring(sep + 4).Trim();
                if (en.Length == 0) { skipped++; continue; }
                if (!seen.Add((phrase ? "~" : "") + StripPeriod(en))) continue; // first rule wins

                try
                {
                    if (phrase)
                    {
                        phrases.Add(new Phrase
                        {
                            Regex = new Regex(@"(?<![\p{L}\p{N}])" + FirstCharInsensitive(Regex.Escape(en), en) + @"(?![\p{L}\p{N}])"),
                            Output = es
                        });
                    }
                    else
                    {
                        var rule = Compile(en, es);
                        if (rule == null) { skipped++; continue; }
                        rules.Add(rule);
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    MonsterTrainAccessibility.LogWarning($"Speech rule line {lineNo} skipped: {ex.Message}");
                }
            }

            // Longer phrases first so "pact shards" wins over "shards"
            phrases.Sort((a, b) => b.Regex.ToString().Length.CompareTo(a.Regex.ToString().Length));
            _rules = rules;
            _phrases = phrases;
            MonsterTrainAccessibility.LogInfo($"Speech rules loaded: {rules.Count} rules, {phrases.Count} phrases, {skipped} skipped ({path})");
        }

        public static void Unload()
        {
            _rules = null;
            _phrases = null;
            _cache.Clear();
        }

        private static string StripPeriod(string s)
        {
            if (s.EndsWith(".") && !s.EndsWith("..")) return s.Substring(0, s.Length - 1).TrimEnd();
            return s;
        }

        private static Rule Compile(string en, string es)
        {
            en = StripPeriod(en);
            es = StripPeriod(es);
            var rule = new Rule { English = en, Output = es };
            var sb = new StringBuilder(@"\G");
            int pos = 0;
            bool anyLetters = false;
            foreach (Match m in HoleRe.Matches(en))
            {
                if (m.Index > pos) AddLiteral(rule, sb, en.Substring(pos, m.Index - pos), ref anyLetters);
                bool numeric = m.Groups[1].Value == "#";
                bool leading = rule.Literals.Count == 0 && rule.HoleNames.Count == 0;
                if (leading) rule.FirstLiteral = null;
                rule.HoleNames.Add(m.Groups[2].Value);
                rule.HoleNumeric.Add(numeric);
                // A leading hole stays inside its own segment: otherwise "{who} at {#hp} of {#max}"
                // swallows the list before it ("killed Imp, Guard, your Guard at 3 of 5" -> who =
                // "Guard, your Guard"). Text it can't reach is kept as is, so names with commas survive.
                sb.Append(numeric ? @"([-+]?\d+(?:[.,]\d+)?)" : leading ? LeadingHole : "(.+?)");
                pos = m.Index + m.Length;
            }
            if (pos < en.Length) AddLiteral(rule, sb, en.Substring(pos), ref anyLetters);
            // A rule with no words of its own ("{a}: {b}") would rewrite any text
            if (!anyLetters) return null;
            sb.Append(EndBoundary);
            rule.Regex = new Regex(sb.ToString(), RegexOptions.CultureInvariant);
            return rule;
        }

        private static void AddLiteral(Rule rule, StringBuilder sb, string lit, ref bool anyLetters)
        {
            foreach (char c in lit) if (char.IsLetter(c)) { anyLetters = true; break; }
            bool first = rule.Literals.Count == 0 && rule.HoleNames.Count == 0;
            rule.Literals.Add(lit);
            rule.LiteralLength += lit.Length;
            if (first)
            {
                rule.FirstLiteral = lit;
                sb.Append(FirstCharInsensitive(Regex.Escape(lit), lit));
            }
            else
            {
                sb.Append(Regex.Escape(lit));
            }
        }

        /// <summary>Make the escaped pattern's first letter match either case.</summary>
        private static string FirstCharInsensitive(string escaped, string literal)
        {
            if (literal.Length == 0 || !char.IsLetter(literal[0]) || escaped.Length == 0 || escaped[0] != literal[0])
                return escaped;
            char lo = char.ToLowerInvariant(literal[0]), up = char.ToUpperInvariant(literal[0]);
            if (lo == up) return escaped;
            return "[" + lo + up + "]" + escaped.Substring(1);
        }

        /// <summary>Translate a finished speech string. Returns the input when nothing applies.</summary>
        public static string Translate(string text)
        {
            if (_rules == null || string.IsNullOrEmpty(text)) return text;
            if (_cache.TryGetValue(text, out var cached)) return cached;
            string result;
            try
            {
                result = TranslateCore(text, 0);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Speech translation failed: {ex.Message}");
                result = text;
            }
            if (_cache.Count >= CacheLimit) _cache.Clear();
            _cache[text] = result;
            return result;
        }

        private static string TranslateCore(string text, int depth)
        {
            if (string.IsNullOrEmpty(text) || depth > MaxDepth) return text;

            // Rules whose literals all occur somewhere in the text
            List<Rule> candidates = null;
            foreach (var rule in _rules)
            {
                bool ok = true;
                for (int i = 0; i < rule.Literals.Count; i++)
                {
                    string lit = rule.Literals[i];
                    int idx = (i == 0 && rule.FirstLiteral != null)
                        ? text.IndexOf(lit, StringComparison.OrdinalIgnoreCase)
                        : text.IndexOf(lit, StringComparison.Ordinal);
                    if (idx < 0) { ok = false; break; }
                }
                if (!ok) continue;
                (candidates ?? (candidates = new List<Rule>())).Add(rule);
            }
            if (candidates == null) return ApplyPhrases(text);

            var starts = new List<int> { 0 };
            foreach (Match m in SeparatorRe.Matches(text))
                starts.Add(m.Index + m.Length);

            var output = new StringBuilder(text.Length + 16);
            var pending = new StringBuilder();
            int cursor = 0;
            foreach (int rawStart in starts)
            {
                int s = rawStart;
                while (s < text.Length && text[s] == ' ') s++;
                if (s < cursor || s >= text.Length) continue;

                Rule best = null;
                Match bestMatch = null;
                foreach (var rule in candidates)
                {
                    if (rule.FirstLiteral != null)
                    {
                        string lit = rule.FirstLiteral;
                        if (s + lit.Length > text.Length) continue;
                        if (string.Compare(text, s + 1, lit, 1, lit.Length - 1, StringComparison.Ordinal) != 0) continue;
                    }
                    var m = rule.Regex.Match(text, s);
                    if (!m.Success || m.Index != s || m.Length == 0) continue;
                    if (bestMatch == null || m.Length > bestMatch.Length ||
                        (m.Length == bestMatch.Length && rule.LiteralLength > best.LiteralLength))
                    {
                        best = rule;
                        bestMatch = m;
                    }
                }
                if (best == null) continue;

                pending.Append(text, cursor, s - cursor);
                output.Append(ApplyPhrases(pending.ToString()));
                pending.Clear();
                output.Append(MatchCase(text[s], Render(best, bestMatch, depth)));
                cursor = s + bestMatch.Length;
            }
            if (cursor < text.Length) pending.Append(text, cursor, text.Length - cursor);
            if (pending.Length > 0) output.Append(ApplyPhrases(pending.ToString()));
            return output.ToString();
        }

        private static string Render(Rule rule, Match m, int depth)
        {
            string result = rule.Output;
            for (int i = 0; i < rule.HoleNames.Count; i++)
            {
                string value = m.Groups[i + 1].Value;
                if (!rule.HoleNumeric[i]) value = TranslateCore(value, depth + 1);
                string name = rule.HoleNames[i];
                result = result.Replace("{#" + name + "}", value).Replace("{" + name + "}", value);
            }
            return result;
        }

        private static string ApplyPhrases(string text)
        {
            if (_phrases == null || _phrases.Count == 0 || text.Length == 0) return text;
            foreach (var p in _phrases)
            {
                if (!p.Regex.IsMatch(text)) continue;
                text = p.Regex.Replace(text, m => MatchCase(m.Value[0], p.Output));
            }
            return text;
        }

        /// <summary>Give the output's first letter the case of the input's first letter.</summary>
        private static string MatchCase(char source, string output)
        {
            if (string.IsNullOrEmpty(output) || !char.IsLetter(source)) return output;
            int i = 0;
            while (i < output.Length && !char.IsLetter(output[i])) i++;   // skip "¿" "¡"
            if (i >= output.Length) return output;
            char c = output[i];
            char wanted = char.IsUpper(source) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
            if (wanted == c) return output;
            return output.Substring(0, i) + wanted + output.Substring(i + 1);
        }
    }
}
