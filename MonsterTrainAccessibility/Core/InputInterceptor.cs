using System;
using System.Collections.Generic;
using MonsterTrainAccessibility.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// MonoBehaviour that handles accessibility hotkeys.
    /// Navigation is handled by the game's EventSystem - we just provide info hotkeys.
    /// </summary>
    public class InputInterceptor : MonoBehaviour
    {
        /// <summary>
        /// Whether input handling is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Cooldown to prevent key repeat spam
        /// </summary>
        private float _inputCooldown = 0f;
        private const float INPUT_COOLDOWN_TIME = 0.15f;

        /// <summary>
        /// Currently browsed floor room index for Page Up/Down outside targeting mode
        /// </summary>
        private int _browsingFloor = 0; // Start at bottom floor (room index 0)

        private void LateUpdate()
        {
            MonsterTrainAccessibility.ScreenReader?.FlushBatch();
        }

        private void Update()
        {
            if (!IsEnabled)
                return;

            if (Input.anyKeyDown)
                KeyInput.MarkKeyPressed();
            if (KeyInput.GetKeyDown(KeyCode.UpArrow) || KeyInput.GetKeyDown(KeyCode.DownArrow) ||
                KeyInput.GetKeyDown(KeyCode.LeftArrow) || KeyInput.GetKeyDown(KeyCode.RightArrow) ||
                KeyInput.GetKeyDown(KeyCode.Tab))
                KeyInput.MarkNavKeyPressed();

            // Check if game has focus
            if (!KeyInput.AppFocused)
                return;

            // Update cooldown
            if (_inputCooldown > 0)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            var config = MonsterTrainAccessibility.AccessibilitySettings;

            if (config == null)
                return;

            // The settings list is modal: every key goes to it while it is open
            var settings = MonsterTrainAccessibility.Settings;
            if (settings != null && settings.IsOpen)
            {
                if (settings.HandleInput())
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }
            if (settings != null && KeyInput.GetKeyDown(config.SettingsKey.Value) &&
                MonsterTrainAccessibility.HelpSystem?.IsBrowsing != true)
            {
                settings.Open();
                _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }

            // While the help list is open it captures Up/Down/F1/Enter/Escape
            // until closed (InputSuppressionPatch keeps those keys from the game)
            var help = MonsterTrainAccessibility.HelpSystem;
            if (help != null && help.IsBrowsing)
            {
                HandleHelpBrowseInput(config, help);
                return;
            }

            // Ctrl combos: review buffers (map cursor on the map screen) on the
            // arrows. Handled before the targeting check
            // so they stay available while targeting. While Ctrl is held no
            // plain hotkeys fire (so Ctrl+C etc. don't trigger letter hotkeys).
            if (KeyInput.GetKey(KeyCode.LeftControl) || KeyInput.GetKey(KeyCode.RightControl))
            {
                HandleCtrlHotkeys(config);
                return;
            }

            // Skip most input handling if floor or unit targeting is active
            // (Targeting systems handle their own input)
            if (FloorTargetingSystem.Instance?.IsTargeting == true ||
                UnitTargetingSystem.Instance?.IsTargeting == true)
            {
                // Only allow help key during targeting
                if (KeyInput.GetKeyDown(config.HelpKey.Value))
                {
                    MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
                return;
            }

            bool shiftHeld = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);

            // Floor review: Shift+K in battle opens a virtual cursor over the
            // floors; while it is open the arrows, Enter, and Escape belong to it
            // (InputSuppressionPatch keeps them from the game). Unclaimed keys
            // fall through so F1 and the other hotkeys keep working.
            var review = MonsterTrainAccessibility.FloorReview;
            if (review != null && review.IsActive)
            {
                if (review.HandleInput())
                {
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                    return;
                }
            }
            else if (review != null && shiftHeld && KeyInput.GetKeyDown(config.FloorReviewKey.Value) &&
                     FloorReviewSystem.IsBattleScreenFrontmost())
            {
                review.Open();
                _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }

            // Submit on a toggle/flip button changes its text without moving the
            // selection; re-read it once the game has processed the press
            if ((KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter) ||
                 KeyInput.GetKeyDown(KeyCode.Space) || KeyInput.GetKeyDown(KeyCode.E)) &&
                MonsterTrainAccessibility.MenuHandler != null)
            {
                MonsterTrainAccessibility.MenuHandler.StartCoroutine(MonsterTrainAccessibility.MenuHandler.RecheckFocusAfterSubmit());
            }

            // Help key (F1) - always available
            if (KeyInput.GetKeyDown(config.HelpKey.Value))
            {
                MonsterTrainAccessibility.HelpSystem?.ShowHelp();
                _inputCooldown = INPUT_COOLDOWN_TIME;
                return;
            }

            // Information hotkeys. None of them is a game control (see
            // AccessibilityConfig); Shift variants are checked first.
            if (KeyInput.GetKeyDown(config.ReadCurrentKey.Value))
            {
                RereadCurrentSelection();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadTextKey.Value))
            {
                ReadAllScreenText();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ToggleVerbosityKey.Value))
            {
                config.CycleVerbosity();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadGoldKey.Value))
            {
                ReadGold();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadPyreHealthKey.Value))
            {
                ReadPyreHealth();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadPactShardsKey.Value))
            {
                ReadPactShards();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (shiftHeld && KeyInput.GetKeyDown(config.ReadEnemiesKey.Value))
            {
                ReadEnemies();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadHandKey.Value))
            {
                ReadHand();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (!shiftHeld && KeyInput.GetKeyDown(config.ReadFloorsKey.Value))
            {
                ReadFloors();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (!shiftHeld && KeyInput.GetKeyDown(config.ReadCurrentFloorKey.Value))
            {
                ReadCurrentFloor();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(config.ReadEmberKey.Value))
            {
                ReadEmber();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.Tab))
            {
                // Skip train stats read when in Settings screen - Tab is used for tab navigation there
                if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.Settings)
                {
                    MonsterTrainAccessibility.LogInfo("TAB key pressed - starting train stats read");
                    // When TAB is pressed, read the train stats panel after a short delay
                    // (to allow the panel to open first)
                    StartCoroutine(ReadTrainStatsDelayed());
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
            }
            else if (KeyInput.GetKeyDown(KeyCode.N))
            {
                // N is the game's native combat speed toggle
                // We don't block it - just announce the new speed after a short delay
                // (to allow the game to process the speed change first)
                StartCoroutine(AnnounceSpeedChangeDelayed());
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (config.ReadRunSummaryKey.Value != KeyCode.None && KeyInput.GetKeyDown(config.ReadRunSummaryKey.Value))
            {
                if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Victory ||
                    Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Defeat)
                {
                    MonsterTrainAccessibility.LogInfo("S key pressed - reading run summary");
                    ReadAllScreenText();
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
            }
            else if (config.ReadMapNodeKey.Value != KeyCode.None && KeyInput.GetKeyDown(config.ReadMapNodeKey.Value))
            {
                ReadMapNode();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.PageUp) || KeyInput.GetKeyDown(KeyCode.PageDown))
            {
                var battle = MonsterTrainAccessibility.BattleHandler;
                if (battle != null && battle.IsInBattle)
                {
                    BrowseFloor(KeyInput.GetKeyDown(KeyCode.PageUp));
                    _inputCooldown = INPUT_COOLDOWN_TIME;
                }
            }

        }

        private enum CtrlArrow { Up, Down, Left, Right }

        /// <summary>
        /// Handle hotkeys while Ctrl is held: arrows drive the review buffers
        /// (or the map cursor).
        /// </summary>
        private void HandleCtrlHotkeys(AccessibilityConfig config)
        {
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                HandleCtrlArrow(CtrlArrow.Up);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                HandleCtrlArrow(CtrlArrow.Down);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.LeftArrow))
            {
                HandleCtrlArrow(CtrlArrow.Left);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.RightArrow))
            {
                HandleCtrlArrow(CtrlArrow.Right);
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
        }

        /// <summary>
        /// Handle keys while the F1 help list is open:
        /// Up/Down browse entries, F1/Enter/Escape close.
        /// </summary>
        private void HandleHelpBrowseInput(AccessibilityConfig config, Help.HelpSystem help)
        {
            if (KeyInput.GetKeyDown(config.HelpKey.Value) ||
                KeyInput.GetKeyDown(KeyCode.Escape) ||
                KeyInput.GetKeyDown(KeyCode.Return) ||
                KeyInput.GetKeyDown(KeyCode.KeypadEnter) ||
                KeyInput.GetKeyDown(KeyCode.Space))
            {
                help.CloseHelp();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                help.PreviousEntry();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                help.NextEntry();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                help.FirstEntry();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
            else if (KeyInput.GetKeyDown(KeyCode.End))
            {
                help.LastEntry();
                _inputCooldown = INPUT_COOLDOWN_TIME;
            }
        }

        /// <summary>
        /// Route Ctrl+arrow presses: on the map screen they drive the virtual
        /// map cursor; everywhere else they drive the review buffers
        /// (Ctrl+Up reads deeper into the buffer starting from the current/top
        /// item, Ctrl+Down moves back toward the top, Ctrl+Left/Right switch
        /// buffers - the MT2 mod's conventions).
        /// </summary>
        private void HandleCtrlArrow(CtrlArrow arrow)
        {
            if ((Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Map ||
                 Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Minimap) &&
                MonsterTrainAccessibility.MapNav != null)
            {
                switch (arrow)
                {
                    case CtrlArrow.Up: MonsterTrainAccessibility.MapNav.NextRing(); break;
                    case CtrlArrow.Down: MonsterTrainAccessibility.MapNav.PreviousRing(); break;
                    case CtrlArrow.Right: MonsterTrainAccessibility.MapNav.NextNode(); break;
                    case CtrlArrow.Left: MonsterTrainAccessibility.MapNav.PreviousNode(); break;
                }
                return;
            }

            var buffers = MonsterTrainAccessibility.Buffers;
            if (buffers == null)
                return;

            switch (arrow)
            {
                case CtrlArrow.Up: buffers.PreviousItem(); break;
                case CtrlArrow.Down: buffers.NextItem(); break;
                case CtrlArrow.Right: buffers.NextBuffer(); break;
                case CtrlArrow.Left: buffers.PreviousBuffer(); break;
            }
        }

        /// <summary>
        /// Read train stats after a short delay (to allow panel to open)
        /// </summary>
        private System.Collections.IEnumerator ReadTrainStatsDelayed()
        {
            MonsterTrainAccessibility.LogInfo("ReadTrainStatsDelayed coroutine started");
            // Wait for the stats panel to open
            yield return new WaitForSecondsRealtime(0.3f);

            MonsterTrainAccessibility.LogInfo($"ReadTrainStatsDelayed: MenuHandler null: {MonsterTrainAccessibility.MenuHandler == null}");
            // Try to read the train stats panel
            MonsterTrainAccessibility.MenuHandler?.ReadTrainStatsPanel();
        }

        /// <summary>
        /// Announce the combat speed after N key is pressed (game toggles speed)
        /// </summary>
        private System.Collections.IEnumerator AnnounceSpeedChangeDelayed()
        {
            // Wait for the game to process the speed change
            yield return new WaitForSecondsRealtime(0.1f);

            string speedName = GetCurrentSpeedName();
            MonsterTrainAccessibility.ScreenReader?.Speak($"Speed: {speedName}", false);
        }

        private static System.Reflection.MethodInfo _getActiveGameSpeedMethod;
        private static bool _speedMethodSearched;

        /// <summary>
        /// Get the current game speed setting name
        /// </summary>
        private string GetCurrentSpeedName()
        {
            try
            {
                // Find SaveManager.GetActiveGameSpeed() via reflection (cached)
                if (!_speedMethodSearched)
                {
                    _speedMethodSearched = true;
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
                            continue;

                        var saveManagerType = assembly.GetType("SaveManager");
                        if (saveManagerType != null)
                        {
                            _getActiveGameSpeedMethod = saveManagerType.GetMethod("GetActiveGameSpeed",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            break;
                        }
                    }
                }

                if (_getActiveGameSpeedMethod != null)
                {
                    // Find the SaveManager instance in the scene
                    var saveManager = UnityEngine.Object.FindObjectOfType(_getActiveGameSpeedMethod.DeclaringType);
                    if (saveManager != null)
                    {
                        var speed = _getActiveGameSpeedMethod.Invoke(saveManager, null);
                        if (speed != null)
                        {
                            return FormatGameSpeed(speed);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting speed: {ex.Message}");
            }

            return "Changed";
        }

        /// <summary>
        /// Format a SaveManager.GameSpeed enum value into a readable name
        /// </summary>
        private string FormatGameSpeed(object value)
        {
            // The enum is: Normal=0, Fast=1, Ultra=2, SuperUltra=3, Instant=100
            string name = value.ToString();
            if (name == "SuperUltra") return "Super Ultra";
            return name;
        }

        /// <summary>
        /// Browse floors with Page Up/Down when not in targeting mode.
        /// The game natively handles PageUp/PageDown as ScrollUp/ScrollDown to change
        /// the selected room. We just read the game's floor and announce it.
        /// Uses a coroutine to wait one frame so the game processes the input first.
        /// </summary>
        private void BrowseFloor(bool up)
        {
            StartCoroutine(BrowseFloorDelayed());
        }

        private System.Collections.IEnumerator BrowseFloorDelayed()
        {
            // Wait one frame for the game to process the PageUp/PageDown input
            // and update its selected room before we read it.
            yield return null;

            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null) yield break;

            int gameFloor = battle.GetSelectedFloor();
            if (gameFloor < 0 || gameFloor > 3) yield break;

            // Only announce if the floor actually changed
            if (gameFloor != _browsingFloor)
            {
                _browsingFloor = gameFloor;
                string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(_browsingFloor);
                string summary = battle.GetFloorSummary(_browsingFloor) ?? "";
                string message = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
                MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
            }
        }

        /// <summary>
        /// Read the current map node with coordinates (M key on map screen)
        /// </summary>
        private void ReadMapNode()
        {
            if (Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Map)
            {
                // Re-read the current selection which will include map coordinates
                MonsterTrainAccessibility.MenuHandler?.RereadCurrentSelection();
            }
        }

        /// <summary>
        /// Re-read the currently selected UI element
        /// </summary>
        private void RereadCurrentSelection()
        {
            MonsterTrainAccessibility.MenuHandler?.RereadCurrentSelection();
        }

        /// <summary>
        /// Read all text on screen (patch notes, descriptions, etc.)
        /// </summary>
        private void ReadAllScreenText()
        {
            MonsterTrainAccessibility.MenuHandler?.ReadAllScreenText();
        }

        /// <summary>
        /// Read the player's current hand
        /// </summary>
        private void ReadHand()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceHand();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read all floor information
        /// </summary>
        private void ReadFloors()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceAllFloors();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read enemy information and intents
        /// </summary>
        private void ReadEnemies()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle != null && battle.IsInBattle)
            {
                battle.AnnounceEnemies();
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
            }
        }

        /// <summary>
        /// Read current ember (G, battle only)
        /// </summary>
        private void ReadEmber()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
                return;
            }

            int ember = battle.GetCurrentEnergy();
            MonsterTrainAccessibility.ScreenReader?.Speak(
                ember >= 0 ? $"{ember} ember" : "Ember not available", false);
        }

        /// <summary>
        /// Read current gold (F5, works on any screen)
        /// </summary>
        private void ReadGold()
        {
            int gold = RunInfoReader.GetGold();
            if (gold >= 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Speak($"{gold} gold", false);
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Gold not available");
            }
        }

        /// <summary>
        /// Read current pyre health (F6, works on any screen)
        /// </summary>
        private void ReadPyreHealth()
        {
            int hp = RunInfoReader.GetPyreHealth();
            if (hp < 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Pyre health not available");
                return;
            }

            int max = RunInfoReader.GetMaxPyreHealth();
            string message = max > 0 ? $"Pyre health {hp} of {max}" : $"Pyre health {hp}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Read pact shards and threat level (F7, The Last Divinity runs)
        /// </summary>
        private void ReadPactShards()
        {
            string info = RunInfoReader.GetPactShardInfo();
            MonsterTrainAccessibility.ScreenReader?.Speak(
                string.IsNullOrEmpty(info) ? "Pact shards not available" : info, false);
        }

        /// <summary>
        /// Read the currently selected floor's capacity and units (K, battle only)
        /// </summary>
        private void ReadCurrentFloor()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
                return;
            }

            int floor = battle.GetSelectedFloor();
            if (floor < 0 || floor > 3)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("No floor selected");
                return;
            }

            string floorName = Screens.BattleAccessibility.RoomIndexToFloorName(floor);
            string summary = battle.GetFloorSummary(floor);
            string message = string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
            MonsterTrainAccessibility.ScreenReader?.Speak(message, false);
        }

        /// <summary>
        /// Temporarily disable input handling
        /// </summary>
        public void Pause()
        {
            IsEnabled = false;
        }

        /// <summary>
        /// Re-enable input handling
        /// </summary>
        public void Resume()
        {
            IsEnabled = true;
        }
    }
}
