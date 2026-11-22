using BepInEx.Configuration;
using UnityEngine;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Configuration options for the accessibility mod
    /// </summary>
    public class AccessibilityConfig
    {
        // Speech settings
        public ConfigEntry<VerbosityLevel> VerbosityLevel { get; private set; }
        public ConfigEntry<Translation.Language> ModLanguage { get; private set; }
        public ConfigEntry<bool> UseSAPIFallback { get; private set; }
        public ConfigEntry<bool> EnableBraille { get; private set; }
        public ConfigEntry<bool> DebugBridge { get; private set; }
        public ConfigEntry<KeyCode> FloorReviewKey { get; private set; }
        public ConfigEntry<KeyCode> SettingsKey { get; private set; }
        public ConfigEntry<bool> SceneDescriptions { get; private set; }
        public ConfigEntry<int> KeyLayoutVersion { get; private set; }
        public ConfigEntry<bool> DebugBridgeMute { get; private set; }

        // Navigation keys
        public ConfigEntry<KeyCode> NavigateUpKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateDownKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateLeftKey { get; private set; }
        public ConfigEntry<KeyCode> NavigateRightKey { get; private set; }
        public ConfigEntry<KeyCode> ActivateKey { get; private set; }
        public ConfigEntry<KeyCode> BackKey { get; private set; }
        public ConfigEntry<KeyCode> AlternateActivateKey { get; private set; }

        // Information hotkeys
        public ConfigEntry<KeyCode> ReadCurrentKey { get; private set; }
        public ConfigEntry<KeyCode> ReadTextKey { get; private set; }
        public ConfigEntry<KeyCode> ReadHandKey { get; private set; }
        public ConfigEntry<KeyCode> ReadFloorsKey { get; private set; }
        public ConfigEntry<KeyCode> ReadCurrentFloorKey { get; private set; }
        public ConfigEntry<KeyCode> ReadEnemiesKey { get; private set; }
        public ConfigEntry<KeyCode> ReadEmberKey { get; private set; }
        public ConfigEntry<KeyCode> ReadGoldKey { get; private set; }
        public ConfigEntry<KeyCode> ReadPyreHealthKey { get; private set; }
        public ConfigEntry<KeyCode> ReadPactShardsKey { get; private set; }
        public ConfigEntry<KeyCode> ToggleVerbosityKey { get; private set; }
        public ConfigEntry<KeyCode> HelpKey { get; private set; }

        // Map keys
        public ConfigEntry<KeyCode> ReadMapNodeKey { get; private set; }
        public ConfigEntry<KeyCode> ReadRunSummaryKey { get; private set; }

        // Announcement preferences
        public ConfigEntry<bool> AnnounceCardDraws { get; private set; }
        public ConfigEntry<bool> AnnounceStatusEffects { get; private set; }
        public ConfigEntry<bool> AnnounceDamage { get; private set; }
        public ConfigEntry<bool> CombatSummaryMode { get; private set; }
        public ConfigEntry<bool> AnnounceDeaths { get; private set; }
        public ConfigEntry<bool> AnnounceSpawns { get; private set; }
        public ConfigEntry<bool> AnnounceDialogue { get; private set; }
        public ConfigEntry<bool> AnnounceRelicTriggers { get; private set; }
        public ConfigEntry<bool> InterruptOnFocusChange { get; private set; }

        public AccessibilityConfig(ConfigFile config)
        {
            // ========== Speech Settings ==========
            VerbosityLevel = config.Bind(
                "Speech",
                "VerbosityLevel",
                Core.VerbosityLevel.Normal,
                "How much detail to include in announcements.\n" +
                "Minimal = Names and numbers only\n" +
                "Normal = Standard descriptions\n" +
                "Verbose = Full details including flavor text"
            );

            ModLanguage = config.Bind(
                "Speech",
                "ModLanguage",
                Translation.Language.Auto,
                "Language of the mod's own speech. Auto follows the game's language (Settings > Language, where the mod adds Spanish).\n" +
                "Idioma de lo que dice el mod. Auto sigue el idioma del juego (Ajustes > Idioma, donde el mod añade el español)."
            );

            UseSAPIFallback = config.Bind(
                "Speech",
                "UseSAPIFallback",
                true,
                "Use Windows SAPI (Microsoft Speech) if no screen reader is detected"
            );

            EnableBraille = config.Bind(
                "Speech",
                "EnableBraille",
                true,
                "Send text to braille display if available"
            );

            DebugBridge = config.Bind(
                "Debug",
                "Bridge",
                false,
                "Development only: keep the game running and accepting input in the background, read commands from BepInEx/accessibility_bridge_cmd.txt and log all speech to BepInEx/accessibility_speech_log.txt"
            );

            DebugBridgeMute = config.Bind(
                "Debug",
                "BridgeMute",
                false,
                "While the debug bridge is enabled, log speech without sending it to the screen reader, and mute the game's audio"
            );

            // ========== Navigation Keys ==========
            NavigateUpKey = config.Bind(
                "Keys.Navigation",
                "NavigateUp",
                KeyCode.UpArrow,
                "Key to navigate up"
            );

            NavigateDownKey = config.Bind(
                "Keys.Navigation",
                "NavigateDown",
                KeyCode.DownArrow,
                "Key to navigate down"
            );

            NavigateLeftKey = config.Bind(
                "Keys.Navigation",
                "NavigateLeft",
                KeyCode.LeftArrow,
                "Key to navigate left"
            );

            NavigateRightKey = config.Bind(
                "Keys.Navigation",
                "NavigateRight",
                KeyCode.RightArrow,
                "Key to navigate right"
            );

            ActivateKey = config.Bind(
                "Keys.Navigation",
                "Activate",
                KeyCode.Return,
                "Key to activate/select the current item"
            );

            AlternateActivateKey = config.Bind(
                "Keys.Navigation",
                "AlternateActivate",
                KeyCode.Space,
                "Alternate key to activate/select (useful for some users)"
            );

            BackKey = config.Bind(
                "Keys.Navigation",
                "Back",
                KeyCode.Escape,
                "Key to go back or cancel"
            );

            // ========== Information Hotkeys ==========
            // None of these share a key with the game's own controls
            // (UserKeyMappingHelper defaults: E/Enter, R, Q/Backspace, Escape,
            // WASD/arrows, PageUp/Down, Home/End, Z X V B C M F Tab Shift,
            // 1-0, Y U I O P, N, T, H, F8). Tab, N and PageUp/Down only add an
            // announcement after the game's own action.
            ReadCurrentKey = config.Bind(
                "Keys.Information",
                "ReadCurrent",
                KeyCode.F2,
                "Key to re-read the currently focused item (cards, shop items and artifacts read their full details)"
            );

            ReadTextKey = config.Bind(
                "Keys.Information",
                "ReadText",
                KeyCode.F3,
                "Key to read all text content on screen (patch notes, descriptions, run summary, etc.)"
            );

            ReadHandKey = config.Bind(
                "Keys.Information",
                "ReadHand",
                KeyCode.J,
                "Key to read all cards in hand (battle)"
            );

            ReadFloorsKey = config.Bind(
                "Keys.Information",
                "ReadFloors",
                KeyCode.L,
                "Key to read all floors (battle)"
            );

            ReadCurrentFloorKey = config.Bind(
                "Keys.Information",
                "ReadCurrentFloor",
                KeyCode.K,
                "Key to read the selected floor's capacity and units (battle)"
            );

            FloorReviewKey = config.Bind(
                "Keys.Information",
                "FloorReview",
                KeyCode.K,
                "Key to open the floor review cursor (battle), pressed together with Shift"
            );

            ReadEnemiesKey = config.Bind(
                "Keys.Information",
                "ReadEnemies",
                KeyCode.L,
                "Key to read all units with full detail (battle), pressed together with Shift"
            );

            ReadEmberKey = config.Bind(
                "Keys.Information",
                "ReadEmber",
                KeyCode.G,
                "Key to read current ember (battle)"
            );

            ReadGoldKey = config.Bind(
                "Keys.Information",
                "ReadGold",
                KeyCode.F5,
                "Key to read current gold"
            );

            ReadPyreHealthKey = config.Bind(
                "Keys.Information",
                "ReadPyreHealth",
                KeyCode.F6,
                "Key to read current Pyre health"
            );

            ReadPactShardsKey = config.Bind(
                "Keys.Information",
                "ReadPactShards",
                KeyCode.F7,
                "Key to read pact shards and threat level (The Last Divinity)"
            );

            ToggleVerbosityKey = config.Bind(
                "Keys.Information",
                "ToggleVerbosity",
                KeyCode.F4,
                "Key to cycle through verbosity levels"
            );

            HelpKey = config.Bind(
                "Keys.Information",
                "Help",
                KeyCode.F1,
                "Key to show context-sensitive help for current screen"
            );

            SettingsKey = config.Bind(
                "Keys.Information",
                "Settings",
                KeyCode.F9,
                "Key to open the accessibility settings list (works on any screen)"
            );

            // Retired: the read-current key covers map nodes and the read-text
            // key covers the run summary. Kept so custom bindings still work.
            ReadMapNodeKey = config.Bind(
                "Keys.Information",
                "ReadMapNode",
                KeyCode.None,
                "Optional extra key to re-read the focused map node (the ReadCurrent key does the same)"
            );

            ReadRunSummaryKey = config.Bind(
                "Keys.Information",
                "ReadRunSummary",
                KeyCode.None,
                "Optional extra key to read the run summary on the victory/defeat screen (the ReadText key does the same)"
            );

            MigrateKeyLayout(config);

            // ========== Announcement Preferences ==========
            AnnounceCardDraws = config.Bind(
                "Announcements",
                "CardDraws",
                true,
                "Announce when cards are drawn"
            );

            AnnounceStatusEffects = config.Bind(
                "Announcements",
                "StatusEffects",
                true,
                "Announce when status effects are applied"
            );

            AnnounceDamage = config.Bind(
                "Announcements",
                "Damage",
                true,
                "Announce damage dealt during combat"
            );

            CombatSummaryMode = config.Bind(
                "Announcements",
                "CombatSummaryMode",
                false,
                "During automated combat, stay silent and speak one summary right before your next turn instead of narrating each hit, death, move and arrival: per floor the damage your units dealt and took, kills, losses and the health left on every unit that was hit, then enemy moves, pyre damage and new arrivals. Everything is still logged to the Events buffer and combat log. Off = live blow-by-blow narration."
            );

            SceneDescriptions = config.Bind(
                "Announcements",
                "SceneDescriptions",
                true,
                "Describe static scenes a sighted player watches: the game launching, a run starting, arriving at a new ring, a boss's entrance, a boss turning Relentless or falling, the outer-train boss acting, and the run's end. Never during automated combat. Texts are in SceneDescriptions.txt (a copy next to the mod DLL overrides the built-in one)"
            );

            AnnounceDeaths = config.Bind(
                "Announcements",
                "Deaths",
                true,
                "Announce when units die"
            );

            AnnounceSpawns = config.Bind(
                "Announcements",
                "Spawns",
                true,
                "Announce when enemies and units enter the battlefield"
            );

            AnnounceDialogue = config.Bind(
                "Announcements",
                "Dialogue",
                true,
                "Announce enemy speech bubbles and dialogue"
            );

            AnnounceRelicTriggers = config.Bind(
                "Announcements",
                "RelicTriggers",
                true,
                "Announce when artifacts/relics trigger during combat"
            );

            InterruptOnFocusChange = config.Bind(
                "Announcements",
                "InterruptOnFocusChange",
                false,
                "Stop current speech when focus changes to a new item"
            );
        }

        /// <summary>
        /// Cycle to the next verbosity level
        /// </summary>
        /// <summary>
        /// Layout 2 moved every mod key off the game's own controls (C opened the
        /// discard pile, V the consumed pile, H/R/U/T/M/S were game keys too).
        /// Saved config files keep the old values, so keys still at an old default
        /// move to the new default; keys the player rebound are left alone.
        /// </summary>
        private void MigrateKeyLayout(ConfigFile config)
        {
            KeyLayoutVersion = config.Bind("Keys.Information", "KeyLayoutVersion", 0,
                "Internal: version of the default key layout this file was migrated to");
            if (KeyLayoutVersion.Value >= 2)
                return;

            void Move(ConfigEntry<KeyCode> entry, KeyCode oldDefault, KeyCode newDefault)
            {
                if (entry.Value == oldDefault)
                    entry.Value = newDefault;
            }

            Move(ReadCurrentKey, KeyCode.C, KeyCode.F2);
            Move(ReadTextKey, KeyCode.T, KeyCode.F3);
            Move(ReadHandKey, KeyCode.H, KeyCode.J);
            Move(ReadEnemiesKey, KeyCode.U, KeyCode.L);
            Move(ReadEmberKey, KeyCode.R, KeyCode.G);
            Move(ReadGoldKey, KeyCode.G, KeyCode.F5);
            Move(ReadPyreHealthKey, KeyCode.H, KeyCode.F6);
            Move(ReadPactShardsKey, KeyCode.R, KeyCode.F7);
            Move(ToggleVerbosityKey, KeyCode.V, KeyCode.F4);
            Move(ReadMapNodeKey, KeyCode.M, KeyCode.None);
            Move(ReadRunSummaryKey, KeyCode.S, KeyCode.None);

            KeyLayoutVersion.Value = 2;
            config.Save();
            MonsterTrainAccessibility.LogInfo("Migrated accessibility keys to layout 2 (no game key conflicts)");
        }

        public void CycleVerbosity()
        {
            var current = VerbosityLevel.Value;
            VerbosityLevel.Value = current switch
            {
                Core.VerbosityLevel.Minimal => Core.VerbosityLevel.Normal,
                Core.VerbosityLevel.Normal => Core.VerbosityLevel.Verbose,
                Core.VerbosityLevel.Verbose => Core.VerbosityLevel.Minimal,
                _ => Core.VerbosityLevel.Normal
            };

            MonsterTrainAccessibility.ScreenReader?.Speak($"Verbosity: {VerbosityLevel.Value}", false);
        }
    }

    /// <summary>
    /// How verbose the accessibility announcements should be
    /// </summary>
    public enum VerbosityLevel
    {
        /// <summary>Names and essential numbers only</summary>
        Minimal,

        /// <summary>Standard descriptions with key information</summary>
        Normal,

        /// <summary>Full details including flavor text and extended info</summary>
        Verbose
    }
}
