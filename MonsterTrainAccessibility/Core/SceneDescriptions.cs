using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Audio descriptions for static scenes a sighted player watches: the game
    /// launching, a run starting, arriving at a ring, a boss's entrance, a boss
    /// turning Relentless or falling, the outer-train boss acting between
    /// combats, and the run's end. Never spoken during automated combat:
    /// Relentless (which triggers mid-combat) waits for the player's next turn.
    ///
    /// Texts come from SceneDescriptions.txt, embedded in the DLL; a copy next to
    /// the DLL (BepInEx/plugins) overrides it, so players can edit them. Format:
    /// one "key: text" per line, '#' comments. Boss keys are matched by the
    /// longest key whose suffix is a prefix of the scenario's asset name
    /// ("boss_intro_Level3BattleTalos" covers every Talos variant).
    ///
    /// With the Spanish language, Spanish/SceneDescriptions_es.txt is used instead.
    ///
    /// Toggle: Announcements.SceneDescriptions (F9 settings).
    /// </summary>
    public static class SceneDescriptions
    {
        private const string FileName = "SceneDescriptions.txt";
        private static Dictionary<string, string> _entries;
        private static readonly HashSet<string> _spokenThisRun = new HashSet<string>();
        private static readonly HashSet<string> _spokenThisBattle = new HashSet<string>();
        private static bool _launchSpoken;
        private static string _pendingTurnStart;

        private static bool Enabled =>
            MonsterTrainAccessibility.AccessibilitySettings?.SceneDescriptions?.Value != false;

        private static Dictionary<string, string> Entries
        {
            get
            {
                if (_entries == null) Load();
                return _entries;
            }
        }

        public static void Load()
        {
            _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string text = null;
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string overridePath = Path.Combine(dir ?? "", FileName);
                string spanishPath = Translation.ModLanguage.SpanishPath("SceneDescriptions_es.txt");
                if (Translation.ModLanguage.IsSpanish && File.Exists(spanishPath))
                {
                    text = File.ReadAllText(spanishPath);
                    MonsterTrainAccessibility.LogInfo($"Scene descriptions loaded from {spanishPath}");
                }
                else if (File.Exists(overridePath))
                {
                    text = File.ReadAllText(overridePath);
                    MonsterTrainAccessibility.LogInfo($"Scene descriptions loaded from {overridePath}");
                }
                else
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MonsterTrainAccessibility.SceneDescriptions.txt"))
                    {
                        if (stream != null)
                            using (var reader = new StreamReader(stream))
                                text = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Could not load scene descriptions: {ex.Message}");
            }

            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (key.Length > 0 && value.Length > 0)
                    _entries[key] = value;
            }
            MonsterTrainAccessibility.LogInfo($"Scene descriptions: {_entries.Count} entries");
        }

        private static string Get(string key)
        {
            return key != null && Entries.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>Longest "kind_prefix" key whose prefix starts the scenario asset name.</summary>
        /// <summary>Debug bridge: the texts each boss scene would use for a scenario.</summary>
        internal static string Describe(ScenarioData scenario)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kind in new[] { "boss_intro", "relentless", "boss_defeated", "outer_boss_action" })
            {
                string key = FindForScenario(kind, scenario);
                sb.AppendLine($"  {kind}: {key ?? "none"} -> {Get(key) ?? ""}");
            }
            return sb.ToString();
        }

        private static string FindForScenario(string kind, ScenarioData scenario)
        {
            if (scenario == null) return null;
            string name = scenario.name ?? "";
            string bestKey = null;
            int best = -1;
            string head = kind + "_";
            foreach (var key in Entries.Keys)
            {
                if (!key.StartsWith(head, StringComparison.OrdinalIgnoreCase)) continue;
                string prefix = key.Substring(head.Length);
                if (prefix.Length > best && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    best = prefix.Length;
                    bestKey = key;
                }
            }
            return bestKey;
        }

        private static void Say(string text)
        {
            if (string.IsNullOrEmpty(text) || !Enabled) return;
            MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
        }

        private static ScenarioData CurrentScenario()
        {
            try
            {
                return (Utilities.ReflectionHelper.FindManager("SaveManager") as SaveManager)?.GetCurrentScenarioData();
            }
            catch
            {
                return null;
            }
        }

        public static void PlayLaunch()
        {
            if (_launchSpoken) return;
            _launchSpoken = true;
            Say(Get("launch"));
        }

        /// <summary>A new run begins (run opening screen).</summary>
        public static void PlayRunStart()
        {
            _spokenThisRun.Clear();
            Say(Get("run_start"));
        }

        /// <summary>First arrival at a ring this run (1-based ring number).</summary>
        public static void PlayRing(int ring)
        {
            string key = $"ring_{ring}";
            if (!_spokenThisRun.Add(key)) return;
            string text = Get(key);
            if (string.IsNullOrEmpty(text)) return;
            // At a run's start the map initializes behind the boss list (run
            // opening screen); describe the ring once that screen has closed
            var host = MonsterTrainAccessibility.MenuHandler;
            if (host != null)
                host.StartCoroutine(SayWhenMapFrontmost(text));
            else
                Say(text);
        }

        private static System.Collections.IEnumerator SayWhenMapFrontmost(string text)
        {
            yield return new UnityEngine.WaitForSecondsRealtime(0.5f);
            float deadline = UnityEngine.Time.realtimeSinceStartup + 600f;
            while (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.RunOpening &&
                   UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;
            Say(text);
        }

        public static void OnBattleStarted()
        {
            _spokenThisBattle.Clear();
            _pendingTurnStart = null;
        }

        public static void PlayBossIntro()
        {
            var scenario = CurrentScenario();
            if (scenario == null || scenario.GetDifficulty() != ScenarioDifficulty.Boss) return;
            Say(Get(FindForScenario("boss_intro", scenario)));
        }

        /// <summary>A boss turned Relentless; spoken when the player next gets control.</summary>
        public static void QueueRelentless()
        {
            var scenario = CurrentScenario();
            string key = FindForScenario("relentless", scenario);
            if (key == null || !_spokenThisBattle.Add(key)) return;
            _pendingTurnStart = Get(key);
        }

        /// <summary>Called at the start of the player's turn (before the turn line).</summary>
        public static void FlushTurnStart()
        {
            if (_pendingTurnStart == null) return;
            string text = _pendingTurnStart;
            _pendingTurnStart = null;
            Say(text);
        }

        public static void PlayBossDefeated()
        {
            var scenario = CurrentScenario();
            if (scenario == null || scenario.GetDifficulty() != ScenarioDifficulty.Boss) return;
            Say(Get(FindForScenario("boss_defeated", scenario)));
        }

        /// <summary>The outer-train boss acts between combats (once per battle).</summary>
        public static void PlayOuterBossAction()
        {
            string key = FindForScenario("outer_boss_action", CurrentScenario());
            if (key == null || !_spokenThisBattle.Add(key)) return;
            Say(Get(key));
        }

        public static void PlayRunEnd(bool victory, bool lastDivinity)
        {
            Say(Get(victory ? (lastDivinity ? "victory_last_divinity" : "victory") : "defeat")
                ?? (victory ? Get("victory") : null));
        }
    }
}
