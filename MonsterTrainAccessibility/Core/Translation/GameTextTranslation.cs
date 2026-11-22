using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using I2.Loc;

namespace MonsterTrainAccessibility.Core.Translation
{
    /// <summary>
    /// Adds Spanish to the game as a real language, at runtime, without touching
    /// the game's files. Monster Train localizes with I2 Localization: each
    /// LanguageSourceData (I2Languages, I2LanguagesInk) holds a language list and,
    /// per term, one text column per language. This adds a "Spanish" (es) column
    /// to every source and fills it from Spanish/GameText_es.txt, so Spanish shows
    /// up in the game's own Settings > Language list (LanguageManager.Init builds
    /// that list from the main source's languages) and everything the game draws
    /// comes from I2 as for any official language: plurals, parameters, fonts.
    ///
    /// Story events work unchanged: the game maps an Ink line's English text to its
    /// InkLoc key through the English column, which stays English, and the key then
    /// resolves to the Spanish column.
    ///
    /// Each entry stores the English it was translated from. If a game update
    /// changes a term's English, that term shows the new English (never Spanish with
    /// outdated numbers) and the log lists it, so the table can be updated.
    /// Non-text terms (fonts, sprites) copy the English column.
    /// </summary>
    public static class GameTextTranslation
    {
        public const string LanguageName = "Spanish";
        public const string LanguageCode = "es";

        private sealed class Entry
        {
            public List<string> English = new List<string>(1);
            public string Translated;
        }

        private static readonly Dictionary<string, Entry> _table = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly HashSet<LanguageSourceData> _injected = new HashSet<LanguageSourceData>();
        private static bool _loaded;
        private static bool _patched;

        public static int EntryCount => _table.Count;

        public static void TryPatch(Harmony harmony)
        {
            if (_patched) return;
            _patched = true;
            try
            {
                var updateSources = AccessTools.Method(typeof(LocalizationManager), nameof(LocalizationManager.UpdateSources));
                if (updateSources != null)
                    harmony.Patch(updateSources, postfix: new HarmonyMethod(typeof(GameTextTranslation), nameof(InjectAll)));

                // The game's language list is built here: Spanish must already be in the sources
                var languageInit = AccessTools.Method(typeof(LanguageManager), "Init");
                if (languageInit != null)
                    harmony.Patch(languageInit, prefix: new HarmonyMethod(typeof(GameTextTranslation), nameof(BeforeLanguageList)));

                MonsterTrainAccessibility.LogInfo("Spanish language patches applied");
                InjectAll();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Spanish language patch failed: {ex.Message}");
            }
        }

        public static void BeforeLanguageList()
        {
            try { LocalizationManager.InitializeIfNeeded(); } catch { }
            InjectAll();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load(ModLanguage.SpanishPath("GameText_es.txt"));
        }

        private static void Load(string path)
        {
            _table.Clear();
            if (!File.Exists(path))
            {
                MonsterTrainAccessibility.LogWarning($"Spanish game text not found: {path}");
                return;
            }
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int a = line.IndexOf('\t');
                int b = a < 0 ? -1 : line.IndexOf('\t', a + 1);
                if (b < 0) continue;
                string term = line.Substring(0, a);
                string en = Unescape(line.Substring(a + 1, b - a - 1));
                string tr = Unescape(line.Substring(b + 1));
                if (!_table.TryGetValue(term, out var entry))
                {
                    entry = new Entry { Translated = tr };
                    _table[term] = entry;
                }
                entry.English.Add(en);
            }
            MonsterTrainAccessibility.LogInfo($"Spanish game text loaded: {_table.Count} terms ({path})");
        }

        /// <summary>Add and fill the Spanish column in every loaded source (once per source).</summary>
        public static void InjectAll()
        {
            try
            {
                var sources = LocalizationManager.Sources;
                if (sources == null || sources.Count == 0) return;
                EnsureLoaded();
                if (_table.Count == 0) return;
                foreach (var source in sources)
                {
                    if (source == null || _injected.Contains(source)) continue;
                    _injected.Add(source);
                    Inject(source);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Spanish language injection failed: {ex}");
            }
        }

        private static void Inject(LanguageSourceData source)
        {
            int en = source.GetLanguageIndex(LocalizationUtil.EnglishLanguage, false, false);
            if (en < 0) return;
            if (source.GetLanguageIndex(LanguageName, false, false) < 0)
                source.AddLanguage(LanguageName, LanguageCode);
            int es = source.GetLanguageIndex(LanguageName, false, false);
            if (es < 0) return;

            int translated = 0, changed = 0, missing = 0;
            foreach (var term in source.mTerms)
            {
                if (term?.Languages == null) continue;
                if (term.Languages.Length <= es) Array.Resize(ref term.Languages, source.mLanguages.Count);
                if (term.Flags == null || term.Flags.Length <= es) Array.Resize(ref term.Flags, source.mLanguages.Count);
                string english = term.Languages[en];
                string text = null;
                if (term.TermType == eTermType.Text && _table.TryGetValue(term.Term, out var entry))
                {
                    string live = Normalize(english);
                    foreach (var src in entry.English)
                        if (Normalize(src) == live) { text = entry.Translated; break; }
                    if (text == null)
                    {
                        changed++;
                        if (changed <= 30)
                            MonsterTrainAccessibility.LogInfo($"Spanish: English text changed for {term.Term}, showing English");
                    }
                }
                else if (term.TermType == eTermType.Text && !string.IsNullOrEmpty(english))
                {
                    missing++;
                }
                if (text != null) translated++;
                term.Languages[es] = text ?? english;
                term.Flags[es] = term.Flags[en];
            }

            // The language list shows LanguageName_<code> in each language
            if (source == LocalizationUtil.GetMainLanguageSource())
            {
                string key = LocalizationUtil.GetLanguageNameKey(LanguageCode);
                var nameTerm = source.GetTermData(key) ?? source.AddTerm(key);
                for (int i = 0; i < nameTerm.Languages.Length; i++)
                    nameTerm.Languages[i] = "Español";
            }
            source.UpdateDictionary(true);
            MonsterTrainAccessibility.LogInfo($"Spanish added to {source.ownerObject?.name ?? "source"}: {translated} translated, {changed} changed since translation, {missing} without translation");
        }

        /// <summary>Debug bridge: every text term with its live English, for updating the table.</summary>
        public static string DumpTerms(string path)
        {
            int n = 0;
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                foreach (var source in LocalizationManager.Sources)
                {
                    int en = source.GetLanguageIndex(LocalizationUtil.EnglishLanguage, false, false);
                    if (en < 0) continue;
                    string name = source.ownerObject?.name ?? "source";
                    foreach (var term in source.mTerms)
                    {
                        if (term?.Languages == null || term.TermType != eTermType.Text) continue;
                        w.Write(name); w.Write('\t');
                        w.Write(term.Term); w.Write('\t');
                        w.Write(Escape(term.Languages[en] ?? "")); w.Write('\n');
                        n++;
                    }
                }
            }
            return $"{n} terms written to {path}";
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append('\\').Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Normalize(string s) => s?.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }
}
