using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using I2.Loc;

namespace MonsterTrainAccessibility.Core.Translation
{
    /// <summary>Language of the mod's own speech (config Speech.ModLanguage).</summary>
    public enum Language
    {
        /// <summary>Follow the game's language: Spanish when the game is in Spanish, English otherwise.</summary>
        Auto,
        English,
        Spanish
    }

    /// <summary>
    /// Spanish support has two layers. The game's text: GameTextTranslation adds
    /// Spanish to the game's own language list (Settings > Language). The mod's
    /// speech: SpeechTranslator turns the mod's English sentences into Spanish at
    /// the speech output. The mod speaks the game's language (config
    /// Speech.ModLanguage can force one). Data files live in
    /// BepInEx/plugins/Spanish: GameText_es.txt, ModStrings_es.txt,
    /// SceneDescriptions_es.txt.
    /// </summary>
    public static class ModLanguage
    {
        public const string SpanishFolder = "Spanish";

        private static readonly FieldInfo LanguageCodeField =
            AccessTools.Field(typeof(LocalizationManager), "mLanguageCode");

        private static Language _applied = Language.English;
        private static bool _rulesLoaded;

        public static string PluginDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        public static string SpanishPath(string file) => Path.Combine(PluginDir, SpanishFolder, file);

        /// <summary>The game's current language code, read without initializing I2 early.</summary>
        public static string GameLanguageCode
        {
            get
            {
                try { return LanguageCodeField?.GetValue(null) as string ?? ""; }
                catch { return ""; }
            }
        }

        /// <summary>The language the mod speaks right now.</summary>
        public static Language Current
        {
            get
            {
                var setting = MonsterTrainAccessibility.AccessibilitySettings?.ModLanguage?.Value ?? Language.Auto;
                Language effective = setting != Language.Auto
                    ? setting
                    : (GameLanguageCode == GameTextTranslation.LanguageCode ? Language.Spanish : Language.English);
                if (effective != _applied) OnLanguageChanged(effective);
                return effective;
            }
        }

        public static bool IsSpanish => Current == Language.Spanish;

        private static void OnLanguageChanged(Language language)
        {
            _applied = language;
            try
            {
                if (language == Language.Spanish && !_rulesLoaded)
                {
                    SpeechTranslator.Load(SpanishPath("ModStrings_es.txt"));
                    _rulesLoaded = true;
                }
                SceneDescriptions.Load();
                KeywordManager.Reset();
                MonsterTrainAccessibility.LogInfo($"Mod speech language: {language}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Could not switch to {language}: {ex.Message}");
            }
        }

        /// <summary>The mod's speech in the current language (unchanged in English).</summary>
        public static string TranslateSpeech(string text) =>
            Current == Language.Spanish ? SpeechTranslator.Translate(text) : text;
    }
}
