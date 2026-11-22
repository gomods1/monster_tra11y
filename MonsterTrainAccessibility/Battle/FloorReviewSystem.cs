using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MonsterTrainAccessibility.Core.Buffers;
using MonsterTrainAccessibility.Screens;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Battle navigation with the plain arrows. Up and Down walk the screen
    /// bottom to top: your hand, then the bottom, middle and top floors,
    /// then the pyre room (Up climbs, Down comes back toward the hand, no
    /// wrap). On the hand, Left and
    /// Right stay the game's (they cycle the cards); on a floor they step
    /// through what is on it: your units back-to-front, then enemies
    /// front-to-back (the pyre itself is the first stop in the pyre room),
    /// repeating at the ends. Enter on a floor reads its full details.
    ///
    /// Moving onto a floor also selects it in the game, the same as Page
    /// Up/Down (during the player's action phase, when the game allows
    /// scrolling); outside it the cursor only reviews. Focus announcements
    /// are the Page Up/Down floor line or a concise unit line; full details
    /// (status effects with keyword explanations, abilities, intents, floor
    /// corruption and enchantments) feed the UI buffer - and the Creature
    /// buffer for units and the pyre - for Ctrl+arrow review.
    ///
    /// IsActive = a floor (not the hand) is focused. The navigation only
    /// applies while the battle screen is frontmost and the player is not
    /// targeting, holding a picked-up card or choosing cards from the hand
    /// (IsNavigable). InputSuppressionPatch then keeps Up/Down from the game
    /// (its own Up-from-hand tower mode is replaced by this), and while a
    /// floor is focused also Left/Right and Submit (Enter/E would otherwise
    /// pick up the hand's still-focused card). Escape, number keys and Page
    /// Up/Down stay the game's.
    /// </summary>
    public class FloorReviewSystem
    {
        /// <summary>True while a floor (not the hand) is focused.</summary>
        public bool IsActive { get; private set; }

        private int _closedFrame = -1;

        /// <summary>
        /// True for the rest of the frame in which focus left the floors. Keys
        /// pressed that frame still read as down and must stay claimed.
        /// </summary>
        public bool ClosedThisFrame => _closedFrame == Time.frameCount;

        private int _roomIndex;

        /// <summary>-1 focuses the floor itself; 0.. index into the lineup.</summary>
        private int _unitIndex = -1;

        /// <summary>The game's selected room as last seen (to follow Page Up/Down).</summary>
        private int _lastGameRoom = -1;

        /// <summary>Up walks this list after the hand: bottom, middle, top, pyre room.</summary>
        private static readonly int[] FloorOrder = { 0, 1, 2, 3 };

        private struct LineupEntry
        {
            public object Unit;
            public bool IsEnemy;
            public bool IsPyre;
            public int Rank;      // 1 = frontmost of its team
            public int TeamCount;
        }

        private static BattleManagerCache Cache => MonsterTrainAccessibility.BattleHandler?.Cache;

        /// <summary>
        /// Return focus to the hand without speaking (targeting starts, the
        /// turn starts, battle ends, an overlay covers the battle).
        /// </summary>
        public void Close(bool announce)
        {
            if (!IsActive)
                return;

            IsActive = false;
            _closedFrame = Time.frameCount;
            _unitIndex = -1;
            if (announce)
                AnnounceHand();
        }

        /// <summary>
        /// Handle a keypress. Returns true when the key was claimed
        /// (InputInterceptor then applies its cooldown); unclaimed keys fall
        /// through to the normal hotkeys.
        /// </summary>
        public bool HandleInput()
        {
            if (!IsNavigable())
            {
                Close(announce: false);
                return false;
            }

            if (IsActive)
                FollowGameRoom();

            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(+1);
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(-1);
                return true;
            }

            if (!IsActive)
                return false;

            if (KeyInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveUnit(+1);
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveUnit(-1);
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                ReadFocusDetails();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Step through hand, bottom, middle, top, pyre room. At the ends the
        /// current stop is read again.
        /// </summary>
        private void Move(int direction)
        {
            // Position 0 = hand, 1.. = FloorOrder
            int position = IsActive ? Array.IndexOf(FloorOrder, _roomIndex) + 1 : 0;
            position = Mathf.Clamp(position + direction, 0, FloorOrder.Length);

            if (position == 0)
            {
                if (IsActive)
                {
                    IsActive = false;
                    _closedFrame = Time.frameCount;
                }
                _unitIndex = -1;
                AnnounceHand();
                return;
            }

            IsActive = true;
            _roomIndex = FloorOrder[position - 1];
            _unitIndex = -1;
            if (SelectGameRoom(_roomIndex))
                _lastGameRoom = _roomIndex;
            AnnounceFocus();
        }

        /// <summary>
        /// Left/Right through the floor's lineup. From the floor overview
        /// Right enters at the first stop and Left at the last; at the ends
        /// the focused unit is read again.
        /// </summary>
        private void MoveUnit(int direction)
        {
            var lineup = BuildLineup();
            if (lineup.Count == 0)
            {
                _unitIndex = -1;
                MonsterTrainAccessibility.ScreenReader?.Speak("No units on this floor", false);
                return;
            }

            if (_unitIndex < 0 || _unitIndex >= lineup.Count)
                _unitIndex = direction > 0 ? 0 : lineup.Count - 1;
            else
                _unitIndex = Mathf.Clamp(_unitIndex + direction, 0, lineup.Count - 1);
            AnnounceFocus();
        }

        /// <summary>
        /// Back on the hand: say so, and let the menu poll re-read the focused
        /// card (it refills the Card buffer the floors replaced).
        /// </summary>
        private void AnnounceHand()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("Hand", false);
            MonsterTrainAccessibility.MenuHandler?.ForgetFocus();
        }

        /// <summary>
        /// Announce the focused floor (the Page Up/Down line) or unit
        /// (concise) and feed its full details to the review buffers.
        /// </summary>
        private void AnnounceFocus()
        {
            try
            {
                var lineup = BuildLineup();
                if (_unitIndex >= lineup.Count)
                    _unitIndex = lineup.Count - 1; // units may have died since the last keypress

                string announcement;
                string details;
                bool isCreature = _unitIndex >= 0;
                if (isCreature)
                {
                    var entry = lineup[_unitIndex];
                    announcement = DescribeEntry(entry, brief: true);
                    details = DescribeEntry(entry, brief: false);
                }
                else
                {
                    announcement = GetFloorLine(includeKeywords: false);
                    details = GetFloorLine(includeKeywords: true);
                }

                FocusBuffers.SetReviewFocus(details, isCreature);
                MonsterTrainAccessibility.ScreenReader?.Speak(announcement, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation announce failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enter (and F2): read the focused floor or unit with full details live.
        /// </summary>
        public void ReadFocusDetails()
        {
            try
            {
                var lineup = BuildLineup();
                if (_unitIndex >= lineup.Count)
                    _unitIndex = lineup.Count - 1;

                string details = _unitIndex >= 0
                    ? DescribeEntry(lineup[_unitIndex], brief: false)
                    : GetFloorLine(includeKeywords: true);
                MonsterTrainAccessibility.ScreenReader?.Speak(details, false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation detail read failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Build the floor's lineup left to right: the pyre (pyre room only),
        /// your units back-to-front, then enemies front-to-back (the readers
        /// return each team front-to-back).
        /// </summary>
        private List<LineupEntry> BuildLineup()
        {
            var lineup = new List<LineupEntry>();
            try
            {
                var cache = Cache;
                if (cache == null)
                    return lineup;
                var room = FloorReader.GetRoom(cache, _roomIndex);
                if (room == null)
                    return lineup;

                var pyreHeart = (room as PyreRoomState)?.GetPyreHeart();
                if (pyreHeart != null)
                    lineup.Add(new LineupEntry { Unit = pyreHeart, IsPyre = true, Rank = 1, TeamCount = 1 });

                var friendly = new List<object>();
                var enemies = new List<object>();
                foreach (var unit in FloorReader.GetUnitsInRoom(room))
                {
                    if (unit == null || ReferenceEquals(unit, pyreHeart))
                        continue;
                    if (FloorReader.IsEnemyUnit(cache, unit))
                        enemies.Add(unit);
                    else
                        friendly.Add(unit);
                }

                for (int i = friendly.Count - 1; i >= 0; i--)
                    lineup.Add(new LineupEntry { Unit = friendly[i], IsEnemy = false, Rank = i + 1, TeamCount = friendly.Count });
                for (int i = 0; i < enemies.Count; i++)
                    lineup.Add(new LineupEntry { Unit = enemies[i], IsEnemy = true, Rank = i + 1, TeamCount = enemies.Count });
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation lineup failed: {ex.Message}");
            }
            return lineup;
        }

        private string DescribeEntry(LineupEntry entry, bool brief)
        {
            if (entry.IsPyre)
                return DescribePyre(entry.Unit, brief);

            var cache = Cache;
            string side = entry.IsEnemy ? "Enemy" : "Your unit";
            string position = "";
            if (entry.TeamCount > 1)
            {
                if (entry.Rank == 1)
                    position = ", front";
                else if (entry.Rank == entry.TeamCount)
                    position = ", back";
            }
            string body = brief
                ? FloorReader.GetUnitBriefDescription(cache, entry.Unit)
                : EnemyReader.GetDetailedUnitDescription(cache, entry.Unit, includeKeywords: true);
            // Position info always trails the content (user preference)
            return $"{body}. {side} {entry.Rank} of {entry.TeamCount}{position}";
        }

        /// <summary>
        /// The pyre as a stop of its own: health, attack, how many times it
        /// attacks, and its status effects (keyword explanations in details).
        /// </summary>
        private static string DescribePyre(object pyreHeart, bool brief)
        {
            var cache = Cache;
            var parts = new List<string>();

            // The game's own name for it: "Last Shard of the Pyre"
            string name = FloorReader.GetUnitName(cache, pyreHeart);
            if (string.IsNullOrEmpty(name))
                name = "Pyre";
            int attack = FloorReader.GetUnitAttack(cache, pyreHeart);
            int hp = ResourceReader.GetPyreHealth(cache);
            int maxHp = ResourceReader.GetMaxPyreHealth(cache);
            string head = $"{name} {attack} attack";
            if (hp >= 0)
                head += maxHp > 0 ? $", {hp} of {maxHp} health" : $", {hp} health";
            parts.Add(head);

            int attacks = GetPyreAttackCount();
            if (attacks > 1)
                parts.Add($"Attacks {attacks} times");

            string statuses = EnemyReader.GetUnitStatusEffects(pyreHeart);
            if (!string.IsNullOrEmpty(statuses))
                parts.Add(statuses);

            if (!brief)
            {
                string keywords = EnemyReader.GetUnitKeywordExplanations(statuses, null);
                if (!string.IsNullOrEmpty(keywords))
                    parts.Add($"Keywords: {keywords}");
                parts.Add("The pyre attacks enemies that reach the pyre room. If its health runs out, the run is lost");
            }
            return string.Join(". ", parts);
        }

        private static int GetPyreAttackCount()
        {
            try
            {
                return (Cache?.SaveManager as SaveManager)?.GetDisplayedPyreNumAttacks() ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>The Page Up/Down floor line: name, then the floor summary.</summary>
        private string GetFloorLine(bool includeKeywords)
        {
            var cache = Cache;
            string floorName = FloorReader.RoomIndexToFloorName(_roomIndex);
            string summary = cache != null
                ? FloorReader.GetFloorSummary(cache, _roomIndex, includeKeywords)
                : null;
            return string.IsNullOrEmpty(summary) ? floorName : $"{floorName}. {summary}";
        }

        #region Game room selection

        private static MethodInfo _innerSetSelectedRoom;
        private static FieldInfo _scrollEnabledField;
        private static bool _roomReflectionDone;

        private static RoomManager GetRoomManager()
        {
            var cache = Cache;
            if (cache == null)
                return null;
            if (cache.RoomManager == null || cache.RoomManager.Equals(null))
                cache.FindManagers();
            return cache.RoomManager as RoomManager;
        }

        /// <summary>
        /// The game only lets the player change floors in the action phase
        /// with scrolling enabled; then select the room the way Page Up/Down
        /// does (RoomUI.InnerSetSelectedRoom with fromPlayerInput: camera,
        /// sound, the game's floor highlights, and with a card picked up its
        /// targets and preview). True when the game's room is now roomIndex.
        /// Also used by FloorTargetingSystem for Up/Down.
        /// </summary>
        internal static bool SelectGameRoom(int roomIndex)
        {
            try
            {
                var roomManager = GetRoomManager();
                var combat = Cache?.CombatManager as CombatManager;
                if (roomManager == null || combat == null || !combat.IsPlayerActionPhase())
                    return false;
                if (roomManager.GetSelectedRoom() == roomIndex)
                    return true;

                var roomUI = roomManager.GetRoomUI();
                if (roomUI == null)
                    return false;
                if (!_roomReflectionDone)
                {
                    _roomReflectionDone = true;
                    _innerSetSelectedRoom = AccessTools.Method(typeof(RoomUI), "InnerSetSelectedRoom", new[] { typeof(int), typeof(bool) });
                    _scrollEnabledField = AccessTools.Field(typeof(RoomUI), "scrollEnabled");
                    if (_innerSetSelectedRoom == null)
                        MonsterTrainAccessibility.LogWarning("Floor navigation: RoomUI.InnerSetSelectedRoom not found");
                }
                if (_innerSetSelectedRoom == null)
                    return false;
                if (_scrollEnabledField?.GetValue(roomUI) is bool scrollEnabled && !scrollEnabled)
                    return false;

                _innerSetSelectedRoom.Invoke(roomUI, new object[] { roomIndex, true });
                return roomManager.GetSelectedRoom() == roomIndex;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation room select failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Page Up/Down still change the game's floor (and announce it); while
        /// a floor is focused the cursor follows. Only in the action phase:
        /// during combat the camera moves the selection by itself.
        /// </summary>
        private void FollowGameRoom()
        {
            try
            {
                var roomManager = GetRoomManager();
                var combat = Cache?.CombatManager as CombatManager;
                if (roomManager == null || combat == null || !combat.IsPlayerActionPhase())
                {
                    _lastGameRoom = -1;
                    return;
                }

                int gameRoom = roomManager.GetSelectedRoom();
                if (gameRoom == _lastGameRoom)
                    return;
                bool follow = _lastGameRoom >= 0 && gameRoom != _roomIndex && gameRoom >= 0 && gameRoom <= 3;
                _lastGameRoom = gameRoom;
                if (follow)
                {
                    _roomIndex = gameRoom;
                    _unitIndex = -1;
                    FocusBuffers.SetReviewFocus(GetFloorLine(includeKeywords: true), false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation room follow failed: {ex.Message}");
            }
        }

        #endregion

        #region When the arrows belong to the navigation

        private static int _navigableFrame = -1;
        private static bool _navigableResult;
        private static HandUI _handUI;

        /// <summary>
        /// True while the arrows drive this navigation: the battle screen is
        /// frontmost and the player is not targeting, holding a picked-up
        /// card, or choosing cards from the hand (discard/exhaust prompts).
        /// Cached per frame - the input suppression patches call this.
        /// </summary>
        internal static bool IsNavigable()
        {
            if (Time.frameCount == _navigableFrame)
                return _navigableResult;
            _navigableFrame = Time.frameCount;
            _navigableResult = ComputeNavigable();
            return _navigableResult;
        }

        private static bool ComputeNavigable()
        {
            if (!IsBattleScreenFrontmost())
                return false;
            if (FloorTargetingSystem.Instance?.IsTargeting == true ||
                UnitTargetingSystem.Instance?.IsTargeting == true)
                return false;

            try
            {
                if (_handUI == null)
                    _handUI = UnityEngine.Object.FindObjectOfType<HandUI>();
                if (_handUI == null)
                    return true;
                if (_handUI.IsHandSelectionActive() && !_handUI.IsHandSelectionHidden())
                    return false;
                var selection = _handUI.GetSelectionBehaviour();
                if (selection != null && (selection.IsCardSelected() || selection.IsPlayingCard()))
                    return false;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation state check failed: {ex.Message}");
            }
            return true;
        }

        /// <summary>
        /// Up/Down belong to the mod: the floor navigation, or choosing a
        /// floor for a picked-up card (FloorTargetingSystem).
        /// </summary>
        internal static bool ClaimsVerticalArrows()
        {
            if (IsNavigable())
                return true;
            return FloorTargetingSystem.Instance?.IsTargeting == true && IsBattleScreenFrontmost();
        }

        #endregion

        #region Battle screen frontmost check

        private static object _screenManager;
        private static MethodInfo _getScreenActiveMethod;
        private static MethodInfo _getScreenMethod;
        private static object _gameScreenNameBoxed;
        private static MethodInfo _isInteractableMethod;
        private static int _frontmostFrame = -1;
        private static bool _frontmostResult;

        // ScreenName.Game from the decompiled game source - the battle screen
        private const int ScreenNameGame = 1;

        /// <summary>
        /// True when the battle screen itself has input focus: combat is
        /// active, ScreenName.Game is active, and the game reports it
        /// interactable. The plain arrows are only claimed then.
        /// IsInteractable is the game's own focus notion:
        /// ScreenManager.UpdateInteractable walks the active screen stack
        /// top-down and only screens above the first DoesBlockInteractions
        /// screen stay interactable - so ANY covering screen (pile views,
        /// dialogs, pause, rewards, drafts, even Tab-focusing the HUD)
        /// releases the arrows without being known by name, while the
        /// permanently-active-but-hidden screens (the loading screen never
        /// deactivates, it only hides its visuals) don't block.
        /// ScreenStateTracker can't do this: it only sees screens opening,
        /// not closing. Cached per frame - this runs from the input
        /// suppression patches.
        /// </summary>
        internal static bool IsBattleScreenFrontmost()
        {
            if (Time.frameCount == _frontmostFrame)
                return _frontmostResult;
            _frontmostFrame = Time.frameCount;
            _frontmostResult = ComputeBattleScreenFrontmost();
            return _frontmostResult;
        }

        private static bool ComputeBattleScreenFrontmost()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return false;

            try
            {
                // UnityEngine.Object's Equals reports destroyed instances as null
                if (_screenManager == null || _screenManager.Equals(null))
                {
                    _screenManager = ReflectionHelper.FindManager("ScreenManager");
                    var smType = _screenManager?.GetType();
                    _getScreenActiveMethod = smType?.GetMethod("GetScreenActive");
                    _getScreenMethod = smType?.GetMethod("GetScreen");
                    var screenNameType = ReflectionHelper.FindType("ScreenName");
                    _gameScreenNameBoxed = screenNameType != null
                        ? Enum.ToObject(screenNameType, ScreenNameGame) : null;
                    _isInteractableMethod = null;
                }

                if (_screenManager == null || _getScreenActiveMethod == null ||
                    _getScreenMethod == null || _gameScreenNameBoxed == null)
                {
                    // Fall back to the tracker (DeckScreenPatch.ClosePostfix
                    // restores it to Battle when a pile view closes mid-battle)
                    return Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle;
                }

                var args = new object[] { _gameScreenNameBoxed };
                if (!(_getScreenActiveMethod.Invoke(_screenManager, args) is bool active && active))
                    return false;

                // An inactive screen keeps its stale interactable flag, so
                // only trust IsInteractable while the screen is active
                var gameScreen = _getScreenMethod.Invoke(_screenManager, args);
                if (gameScreen == null)
                    return false;
                if (_isInteractableMethod == null)
                    _isInteractableMethod = gameScreen.GetType().GetMethod("IsInteractable", Type.EmptyTypes);

                return _isInteractableMethod?.Invoke(gameScreen, null) is bool interactable && interactable;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Floor navigation screen check failed: {ex.Message}");
                return Help.ScreenStateTracker.CurrentScreen == Help.GameScreen.Battle;
            }
        }

        #endregion
    }
}
