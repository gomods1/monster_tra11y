using System;
using System.IO;
using System.Reflection;

namespace MonsterTrainAccessibility.Core.Translation
{
    /// <summary>Languages the mod can switch to (config Speech.Language, first item of the F9 list).</summary>
    public enum Language
    {
        English,
        Spanish
    }

    /// <summary>
    /// Applies the Language setting. Spanish turns on two independent layers:
    /// the game's own text (GameTextTranslation, needs the game set to English)
    /// and the mod's own speech (SpeechTranslator over ScreenReaderOutput).
    /// Data files live in BepInEx/plugins/Spanish: GameText_es.txt,
    /// ModStrings_es.txt, SceneDescriptions_es.txt.
    /// </summary>
    public static class ModLanguage
    {
        public const string SpanishFolder = "Spanish";

        public static Language Current { get; private set; } = Language.English;

        public static bool IsSpanish => Current == Language.Spanish;

        public static string PluginDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        public static string SpanishPath(string file) => Path.Combine(PluginDir, SpanishFolder, file);

        /// <summary>Called once at startup, before anything is spoken.</summary>
        public static void Initialize(AccessibilityConfig config)
        {
            Apply(config?.Language?.Value ?? Language.English, refreshGame: false);
        }

        /// <summary>Switch language. refreshGame re-localizes the labels already on screen.</summary>
        public static void Apply(Language language, bool refreshGame)
        {
            Current = language;
            try
            {
                if (language == Language.Spanish)
                {
                    if (GameTextTranslation.EntryCount == 0)
                        GameTextTranslation.Load(SpanishPath("GameText_es.txt"));
                    SpeechTranslator.Load(SpanishPath("ModStrings_es.txt"));
                    GameTextTranslation.SetActive(true);
                }
                else
                {
                    SpeechTranslator.Unload();
                    GameTextTranslation.SetActive(false);
                }
                SceneDescriptions.Load();
                KeywordManager.Reset();
                if (refreshGame) GameTextTranslation.RefreshGameText();
                MonsterTrainAccessibility.LogInfo($"Language: {language}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Could not apply language {language}: {ex.Message}");
            }
        }

        /// <summary>The mod's speech in the current language (identity in English).</summary>
        public static string TranslateSpeech(string text) =>
            Current == Language.English ? text : SpeechTranslator.Translate(text);
    }
}
