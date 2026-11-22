using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using I2.Loc;

namespace MonsterTrainAccessibility.Core.Translation
{
    /// <summary>
    /// Shows the game's own text in a language the game doesn't ship (Spanish),
    /// at runtime, without touching the game's files. Monster Train localizes
    /// through I2 Localization: every lookup ends in
    /// LanguageSourceData.TryGetTranslation, which this patches. While the game
    /// language is English, a term found in the table (Spanish/GameText_es.txt,
    /// built from a fan translation) answers with its translation instead of the
    /// English column; plurals and parameters are applied by the game afterwards
    /// as usual.
    ///
    /// Safety: each entry stores the English it was translated from, and is only
    /// used while the live English text still equals it. A game update that
    /// changes a card's numbers therefore falls back to the new English text
    /// instead of reading stale Spanish.
    ///
    /// Story events: the game finds an Ink line's translation key by looking up
    /// its English text in a dictionary built from the English column
    /// (InkLocalizationHelper.Init). Translation is suspended while that runs so
    /// the dictionary stays English; the keys then resolve to Spanish.
    /// </summary>
    public static class GameTextTranslation
    {
        private sealed class Entry
        {
            public List<string> English = new List<string>(1);
            public string Translated;
            public bool Checked;
            public string Resolved;   // null = live English differs, keep the game's text
        }

        private static readonly Dictionary<string, Entry> _table = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static int _suspend;
        private static int _stale;
        private static bool _patched;

        public static bool Active { get; private set; }
        public static int EntryCount => _table.Count;

        public static void TryPatch(Harmony harmony)
        {
            if (_patched) return;
            try
            {
                var target = AccessTools.Method(typeof(LanguageSourceData), nameof(LanguageSourceData.TryGetTranslation), new[]
                {
                    typeof(string), typeof(string).MakeByRefType(), typeof(TermData).MakeByRefType(),
                    typeof(string), typeof(string), typeof(bool), typeof(bool)
                });
                if (target == null)
                {
                    MonsterTrainAccessibility.LogError("Game translation: LanguageSourceData.TryGetTranslation not found");
                    return;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(GameTextTranslation), nameof(TryGetTranslationPrefix)));

                var inkInit = AccessTools.Method(typeof(InkLocalizationHelper), nameof(InkLocalizationHelper.Init));
                if (inkInit != null)
                {
                    harmony.Patch(inkInit,
                        prefix: new HarmonyMethod(typeof(GameTextTranslation), nameof(SuspendPrefix)),
                        finalizer: new HarmonyMethod(typeof(GameTextTranslation), nameof(SuspendFinalizer)));
                }
                _patched = true;
                MonsterTrainAccessibility.LogInfo("Game translation patches applied");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Game translation patch failed: {ex.Message}");
            }
        }

        public static void Load(string path)
        {
            _table.Clear();
            _stale = 0;
            if (!File.Exists(path))
            {
                MonsterTrainAccessibility.LogWarning($"Game translation not found: {path}");
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
            MonsterTrainAccessibility.LogInfo($"Game translation loaded: {_table.Count} terms ({path})");
        }

        public static void SetActive(bool active)
        {
            Active = active && _table.Count > 0;
        }

        /// <summary>Re-localize the labels already on screen (after a live language change).</summary>
        public static void RefreshGameText()
        {
            try
            {
                LocalizationManager.LocalizeAll(true);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogWarning($"LocalizeAll failed: {ex.Message}");
            }
        }

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

        public static void SuspendPrefix() => _suspend++;

        public static Exception SuspendFinalizer(Exception __exception)
        {
            if (_suspend > 0) _suspend--;
            return __exception;
        }

        public static bool TryGetTranslationPrefix(LanguageSourceData __instance, string term, ref string Translation,
            ref TermData data, string overrideLanguage, ref bool __result)
        {
            if (!Active || _suspend > 0 || term == null) return true;
            try
            {
                if (!_table.TryGetValue(term, out var entry)) return true;
                string language = overrideLanguage ?? LocalizationManager.CurrentLanguage;
                if (language != LocalizationUtil.EnglishLanguage) return true;

                var termData = __instance.GetTermData(term);
                if (termData == null) return true;

                if (!entry.Checked)
                {
                    int idx = __instance.GetLanguageIndex(LocalizationUtil.EnglishLanguage, false, false);
                    if (idx < 0 || termData.Languages == null || idx >= termData.Languages.Length) return true;
                    string live = Normalize(termData.Languages[idx]);
                    entry.Checked = true;
                    foreach (var en in entry.English)
                    {
                        if (Normalize(en) == live) { entry.Resolved = entry.Translated; break; }
                    }
                    if (entry.Resolved == null)
                    {
                        _stale++;
                        if (_stale <= 20)
                            MonsterTrainAccessibility.LogInfo($"Game translation: English text changed for {term}, keeping the game's text");
                    }
                }
                if (entry.Resolved == null) return true;

                Translation = entry.Resolved;
                data = termData;
                __result = true;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
