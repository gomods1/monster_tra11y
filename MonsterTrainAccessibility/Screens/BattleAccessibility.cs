using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the battle/combat screen.
    /// Coordinates between reader classes that access actual game state.
    /// </summary>
    public class BattleAccessibility
    {
        public bool IsInBattle { get; private set; }

        // Shared manager cache used by all reader classes
        private readonly BattleManagerCache _cache = new BattleManagerCache();

        // Accumulates automated-combat damage for the end-of-turn summary (CombatSummaryMode).
        private readonly CombatTurnSummary _turnSummary = new CombatTurnSummary();

        /// <summary>
        /// Shared manager cache, exposed for the review buffer providers.
        /// </summary>
        internal BattleManagerCache Cache => _cache;

        public BattleAccessibility()
        {
        }

        #region Battle Lifecycle

        /// <summary>
        /// Called when combat begins
        /// </summary>
        public void OnBattleEntered()
        {
            IsInBattle = true;
            _cache.FindManagers();
            Patches.PreviewModeDetector.Reset();
            _turnSummary.Reset();
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;
            _awaitingTurnStart = false;
            _pendingDraws = 0;
            Core.SceneDescriptions.OnBattleStarted();

            MonsterTrainAccessibility.LogInfo("Battle entered");
            // Short opener; the full resource list is on the Resources buffer and
            // the info keys (at battle start ember and hand are still 0)
            var opener = new List<string> { "Battle started" };
            int pyre = ResourceReader.GetPyreHealth(_cache);
            int maxPyre = ResourceReader.GetMaxPyreHealth(_cache);
            if (pyre > 0)
                opener.Add(maxPyre > 0 ? $"pyre {pyre} of {maxPyre}" : $"pyre {pyre}");
            string waves = ResourceReader.GetWaveText(_cache);
            if (!string.IsNullOrEmpty(waves))
                opener.Add(waves);
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen(string.Join(", ", opener));
        }

        /// <summary>
        /// Called when combat ends
        /// </summary>
        public void OnBattleExited()
        {
            IsInBattle = false;
            _turnSummary.Reset();
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;
            Core.Buffers.FocusBuffers.ClearCreature();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
            MonsterTrainAccessibility.LogInfo("Battle exited");
        }

        // Set when the MonsterTurn phase begins and cleared when the game hands
        // control to the player; the turn's opening draw is folded into the
        // single "Your turn" line instead of being spoken separately.
        private bool _awaitingTurnStart;
        private int _pendingDraws;

        /// <summary>True between the MonsterTurn phase starting and the player getting control.</summary>
        internal bool IsAwaitingTurnStart => _awaitingTurnStart;

        /// <summary>Called when the MonsterTurn phase begins (before the turn's draw).</summary>
        internal void OnMonsterTurnPhaseBegan()
        {
            _awaitingTurnStart = true;
            _pendingDraws = 0;
        }

        /// <summary>
        /// Called when the game hands control to the player (after the draw and
        /// start-of-turn effects): "Your turn, 3 ember, drew 5 cards, 2 waves remaining."
        /// </summary>
        public void OnPlayerTurnStarted()
        {
            if (!IsInBattle)
                return;

            bool hadPending = _awaitingTurnStart;
            _awaitingTurnStart = false;
            int drawn = _pendingDraws;
            _pendingDraws = 0;

            // The tracker only sees screens opening, so after an overlay (rewards,
            // pile view) it can be left on the overlay while the battle continues
            if (Help.ScreenStateTracker.CurrentScreen != Help.GameScreen.Battle &&
                Battle.FloorReviewSystem.IsBattleScreenFrontmost())
            {
                Help.ScreenStateTracker.SetScreen(Help.GameScreen.Battle);
            }

            if (MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value)
                AnnounceCombatSummary();

            // Scene description held back from combat (a boss turning Relentless)
            Core.SceneDescriptions.FlushTurnStart();

            var parts = new List<string> { "Your turn" };
            int ember = ResourceReader.GetCurrentEnergy(_cache);
            if (ember >= 0)
                parts.Add($"{ember} ember");
            // Hand size, not the draw count: part of the opening hand is drawn
            // before the MonsterTurn phase, and relics add extra draws
            int handSize = HandReader.GetHandCount(_cache);
            if (handSize >= 0)
                parts.Add(handSize == 1 ? "1 card in hand" : $"{handSize} cards in hand");
            else if (hadPending && drawn > 0 && MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                parts.Add(drawn == 1 ? "drew 1 card" : $"drew {drawn} cards");
            string wave = ResourceReader.GetWaveText(_cache);
            if (!string.IsNullOrEmpty(wave))
                parts.Add(wave);

            string message = string.Join(", ", parts) + ".";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Called when battle is won
        /// </summary>
        public void OnBattleWon()
        {
            IsInBattle = false;
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;
            _turnSummary.Reset();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
            MonsterTrainAccessibility.ScreenReader?.Speak("Victory! Battle won.", false);
            Core.SceneDescriptions.PlayBossDefeated();
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            Patches.CombatPhaseChangePatch.CurrentPhase = -1;
            _turnSummary.Reset();
            MonsterTrainAccessibility.FloorReview?.Close(announce: false);
            MonsterTrainAccessibility.ScreenReader?.Speak("Defeat. The pyre has been destroyed.", false);
        }

        #endregion

        #region Delegating Methods

        // Hand Reading
        public void AnnounceHand() => HandReader.AnnounceHand(_cache);

        /// <summary>
        /// Announce cards drawn (with card names)
        /// </summary>
        public void OnCardsDrawn(List<string> cardNames)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            string message;
            if (cardNames.Count == 1)
            {
                message = $"Drew {cardNames[0]}";
            }
            else
            {
                message = $"Drew: {string.Join(", ", cardNames)}";
            }
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce cards drawn (count only, used when card names aren't available)
        /// </summary>
        public void OnCardsDrawn(int count)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            // The turn's opening draw is reported in the "Your turn" line
            if (_awaitingTurnStart)
            {
                _pendingDraws += count;
                return;
            }

            string message;
            if (count == 1)
            {
                message = "Drew 1 card";
            }
            else if (count > 1)
            {
                message = $"Drew {count} cards";
            }
            else return;

            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Called when a card is played by index
        /// </summary>
        public void OnCardPlayed(int cardIndex)
        {
            // The card was played successfully
            MonsterTrainAccessibility.ScreenReader?.QueueBatched("Card played");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Card played");
        }

        /// <summary>
        /// Called when a card is discarded
        /// </summary>
        public void OnCardDiscarded(string cardName)
        {
            if (!string.IsNullOrEmpty(cardName) && cardName != "Card")
            {
                MonsterTrainAccessibility.ScreenReader?.QueueBatched($"Discarded {cardName}");
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent($"Discarded {cardName}");
            }
        }

        public void RefreshHand()
        {
            // Called when hand changes - could trigger re-announcement if desired
        }

        // Floor Reading
        public static string RoomIndexToFloorName(int roomIndex) => FloorReader.RoomIndexToFloorName(roomIndex);
        public void AnnounceAllFloors() => FloorReader.AnnounceAllFloors(_cache);
        public string GetFloorSummary(int roomIndex, bool includeKeywords = false) => FloorReader.GetFloorSummary(_cache, roomIndex, includeKeywords);
        public int GetSelectedFloor() => FloorReader.GetSelectedFloor(_cache);
        public bool SetSelectedFloor(int roomIndex) => FloorReader.SetSelectedFloor(_cache, roomIndex);
        public List<string> GetAllEnemies() => FloorReader.GetAllEnemies(_cache);
        public List<string> GetAllFriendlyUnits() => FloorReader.GetAllFriendlyUnits(_cache);
        public List<string> GetAllUnits() => FloorReader.GetAllUnits(_cache);
        public string GetTargetUnitDescription(object characterState) => FloorReader.GetTargetUnitDescription(_cache, characterState);

        // Resource Reading
        public void AnnounceResources() => ResourceReader.AnnounceResources(_cache);
        public int GetCurrentEnergy() => ResourceReader.GetCurrentEnergy(_cache);
        public int GetPyreHealth() => ResourceReader.GetPyreHealth(_cache);
        public int GetMaxPyreHealth() => ResourceReader.GetMaxPyreHealth(_cache);
        public int GetDeckSize() => ResourceReader.GetDeckSize(_cache);

        // Enemy Reading
        public void AnnounceEnemies() => EnemyReader.AnnounceEnemies(_cache);
        public string GetDetailedUnitDescription(object unit, bool includeKeywords = false) => EnemyReader.GetDetailedUnitDescription(_cache, unit, includeKeywords);

        /// <summary>
        /// Strip rich text tags from text for screen reader output.
        /// Converts game-specific tags to readable text and removes Unity rich text tags.
        /// </summary>
        public static string StripRichTextTags(string text) => TextUtilities.StripRichTextTags(text);

        #endregion

        #region Combat Events

        /// <summary>
        /// Announce damage dealt
        /// </summary>
        public void OnDamageDealt(string sourceName, string targetName, int damage)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value)
                return;

            string message = $"{sourceName} deals {damage} to {targetName}";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce unit death with floor info. roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnUnitDied(string unitName, bool isEnemy, int roomIndex = -1)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDeaths.Value)
                return;

            string prefix = isEnemy ? "Enemy" : "Your";
            string floorInfo = roomIndex >= 0 ? $" on {RoomIndexToFloorName(roomIndex).ToLower()}" : "";
            string message = $"{prefix} {unitName} died{floorInfo}";

            // During automated combat the death is folded into the end-of-turn summary
            // (per floor, and logged silently); your own spell-kills during MonsterTurn stay live.
            if (IsAutoEventSuppressed())
                _turnSummary.AddDeath(unitName, isEnemy, roomIndex);

            SpeakOrLogAutoEvent(message);
        }

        /// <summary>
        /// True when CombatSummaryMode is on and we are NOT in the player's card-play
        /// phase — i.e. an automated-combat event that should be deferred to the summary.
        /// </summary>
        private static bool IsAutoEventSuppressed()
            => MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value
               && !Patches.CombatPhaseChangePatch.IsMonsterTurn;

        /// <summary>Armor churns constantly as it absorbs hits; it's reported via the hp+armor briefs.</summary>
        private static bool IsArmorEffect(string effectName)
            => !string.IsNullOrEmpty(effectName)
               && effectName.Trim().Equals("armor", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Speak a combat event live, OR — when <see cref="IsAutoEventSuppressed"/> — log it
        /// silently so the blow-by-blow stays available in the Events buffer / combat log
        /// while only the end-of-turn summary is spoken.
        /// </summary>
        private void SpeakOrLogAutoEvent(string message)
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            if (IsAutoEventSuppressed())
            {
                output?.LogCombatEvent(message);
            }
            else
            {
                output?.QueueBatched(message);
                output?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce status effect applied
        /// </summary>
        public void OnStatusEffectApplied(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Keyword explanations are reviewable via the buffers, so the live
            // announcement stays short
            string message = $"{unitName} gains {effectName} {stacks}";
            // Armor changes are suppressed during automated combat (reflected in the
            // hp+armor briefs); other "powers" still announce live.
            if (IsArmorEffect(effectName))
                SpeakOrLogAutoEvent(message);
            else
            {
                MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce unit spawned (entering the battlefield). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnUnitSpawned(string unitName, bool isEnemy, int roomIndex)
        {
            MonsterTrainAccessibility.LogInfo($"OnUnitSpawned called: {unitName}, isEnemy={isEnemy}, roomIndex={roomIndex}, IsInBattle={IsInBattle}");

            if (!IsInBattle)
            {
                MonsterTrainAccessibility.LogInfo("OnUnitSpawned: skipping - not in battle");
                return;
            }

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceSpawns.Value)
            {
                MonsterTrainAccessibility.LogInfo("OnUnitSpawned: skipping - AnnounceSpawns disabled");
                return;
            }

            // Skip invalid unit names
            if (string.IsNullOrEmpty(unitName) || unitName == "Unit")
            {
                MonsterTrainAccessibility.LogInfo($"OnUnitSpawned: skipping - invalid name '{unitName}'");
                return;
            }

            string floorName = roomIndex >= 0 ? RoomIndexToFloorName(roomIndex).ToLower() : "the battlefield";

            string message;
            if (isEnemy)
            {
                message = $"Enemy {unitName} enters on {floorName}";
            }
            else
            {
                message = $"{unitName} summoned on {floorName}";
            }
            // Enemy wave spawns (PreCombat) and auto-spawns are folded into the
            // summary; your own summons during MonsterTurn stay live.
            if (IsAutoEventSuppressed())
                _turnSummary.AddArrival(unitName, isEnemy, roomIndex);
            SpeakOrLogAutoEvent(message);
        }

        public void OnEmberGained(int amount, int total)
        {
            if (!IsInBattle) return;
            string message = $"Gained {amount} ember, {total} total";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        public void OnAttackChanged(string unitName, int delta, int newAttack)
        {
            if (!IsInBattle || delta == 0) return;
            string message = delta > 0
                ? $"{unitName} gains {delta} attack, {newAttack} total"
                : $"{unitName} loses {-delta} attack, {newAttack} left";
            SpeakOrLogAutoEvent(message);
        }

        public void OnFriendlyUnitMoved(string unitName, int toRoomIndex, bool up)
        {
            if (!IsInBattle) return;
            string where = toRoomIndex == 3 ? "the pyre room" : RoomIndexToFloorName(toRoomIndex).ToLower();
            string message = $"Your {unitName} moves {(up ? "up" : "down")} to {where}";
            SpeakOrLogAutoEvent(message);
        }

        /// <summary>A unit regained health (actual HP change).</summary>
        public void OnUnitHealed(string unitName, int amount, int newHP)
        {
            if (!IsInBattle || amount <= 0)
                return;
            SpeakOrLogAutoEvent($"{unitName} healed for {amount}, {newHP} HP");
        }

        /// <summary>
        /// A unit's trigger/ability popup ("Summon", "Slay", "Revenge", "Immune").
        /// </summary>
        public void OnUnitNotification(string unitName, string text, bool isEnemy)
        {
            if (!IsInBattle || string.IsNullOrEmpty(text))
                return;
            string message = string.IsNullOrEmpty(unitName) ? text : $"{unitName}: {text}";
            SpeakOrLogAutoEvent(message);
        }

        /// <summary>A card created mid-battle (Scourge from an enemy, "add to hand" effects).</summary>
        public void OnCardAdded(string cardName, string pileName)
        {
            if (!IsInBattle || string.IsNullOrEmpty(cardName))
                return;
            string message = $"{cardName} added to {pileName}";
            SpeakOrLogAutoEvent(message);
        }

        /// <summary>
        /// Announce enemies ascending floors (generic)
        /// </summary>
        public void OnEnemiesAscended()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.QueueBatched("Enemies ascend");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Enemies ascend");
        }

        /// <summary>
        /// Announce a specific enemy ascending to a floor. roomIndex: 0=bottom, 1=middle, 2=top, 3=pyre.
        /// </summary>
        public void OnEnemyAscended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            string message = $"{enemyName} ascends to {RoomIndexToFloorName(roomIndex).ToLower()}";
            if (IsAutoEventSuppressed())
                _turnSummary.AddMove(enemyName, roomIndex, up: true);
            SpeakOrLogAutoEvent(message);
        }

        /// <summary>
        /// Announce pyre damage
        /// </summary>
        public void OnPyreDamaged(int damage, int remainingHP)
        {
            if (!IsInBattle && !IsRunEventScreen())
                return;

            string message = $"Pyre takes {damage} damage, {remainingHP} left";
            // In battle the pyre heart's own damage line (with the attacker) is spoken
            if (IsInBattle)
                return;
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
        }

        /// <summary>
        /// Out-of-battle screens where pyre health changes are real run events
        /// (story event outcomes, merchant repairs, rewards) - not save loading.
        /// </summary>
        private static bool IsRunEventScreen()
        {
            switch (Help.ScreenStateTracker.CurrentScreen)
            {
                case Help.GameScreen.Event:
                case Help.GameScreen.Shop:
                case Help.GameScreen.Rewards:
                case Help.GameScreen.Map:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Announce unit chatter (speech bubbles above units)
        /// </summary>
        public void OnUnitChatter(string speakerName, string text)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                string speaker = string.IsNullOrEmpty(speakerName) ? "Unit" : speakerName;
                string message = $"{speaker} says: {text}";
                MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce when combat resolution phase starts (units attacking each other)
        /// </summary>
        public void OnCombatResolutionStarted()
        {
            if (!IsInBattle)
                return;

            // In summary mode the "Combat!" framing cue is suppressed (user wants minimal
            // cues; the end-of-turn summary is the only spoken combat output).
            if (MonsterTrainAccessibility.AccessibilitySettings.CombatSummaryMode.Value)
                return;

            // Only announce if there are units to fight
            MonsterTrainAccessibility.ScreenReader?.QueueBatched("Combat!");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("Combat!");
        }

        /// <summary>
        /// Announce when an artifact/relic triggers during combat
        /// </summary>
        public void OnRelicTriggered(string relicName)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceRelicTriggers.Value)
                return;

            string message = $"{relicName} triggered";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce when a unit eats a morsel (Umbra feeding mechanic)
        /// </summary>
        public void OnMorselEaten(string feederName, string morselName)
        {
            if (!IsInBattle)
                return;

            string message = $"{feederName} eats {morselName}";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce when a card is exhausted/consumed (removed from deck)
        /// </summary>
        public void OnCardExhausted(string cardName)
        {
            if (!IsInBattle)
                return;

            string message = $"{cardName} consumed";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce pyre healing
        /// </summary>
        public void OnPyreHealed(int amount, int currentHP)
        {
            if (!IsInBattle && !IsRunEventScreen())
                return;

            string message = $"Pyre healed for {amount}. {currentHP} health";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        /// <summary>
        /// Announce status effect removed from a unit
        /// </summary>
        public void OnStatusEffectRemoved(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Format: "Hornbreaker Prince loses 5 Rage" or "Hornbreaker Prince loses Rage" if stacks is 1
            string message = stacks > 1
                ? $"{unitName} loses {stacks} {effectName}"
                : $"{unitName} loses {effectName}";
            // Armor loss spams during combat (it absorbs every hit); suppress it during
            // automated combat - the surviving units' briefs report current armor instead.
            if (IsArmorEffect(effectName))
                SpeakOrLogAutoEvent(message);
            else
            {
                MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
            }
        }

        /// <summary>
        /// Announce enemy descending to a lower floor (bumped down). roomIndex: 0=bottom, 1=middle, 2=top.
        /// </summary>
        public void OnEnemyDescended(string enemyName, int roomIndex)
        {
            if (!IsInBattle)
                return;

            string message = $"{enemyName} descends to {RoomIndexToFloorName(roomIndex).ToLower()}";
            if (IsAutoEventSuppressed())
                _turnSummary.AddMove(enemyName, roomIndex, up: false);
            SpeakOrLogAutoEvent(message);
        }

        /// <summary>
        /// Announce when all enemies in the current wave have been defeated
        /// </summary>
        public void OnAllEnemiesDefeated()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.QueueBatched("All enemies defeated");
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent("All enemies defeated");
        }

        /// <summary>
        /// Announce combat phase transitions (MonsterTurn, HeroTurn, BossAction, etc.)
        /// </summary>
        public void OnCombatPhaseChanged(string phaseName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.QueueBatched(phaseName);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(phaseName);
        }

        /// <summary>
        /// Announce when a unit's max HP is increased
        /// </summary>
        public void OnMaxHPBuffed(string unitName, int amount)
        {
            if (!IsInBattle)
                return;

            string message = $"{unitName} gains {amount} max health";
            MonsterTrainAccessibility.ScreenReader?.QueueBatched(message);
            MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(message);
        }

        #endregion

        #region Combat Summary (CombatSummaryMode)

        /// <summary>Record pyre damage taken during automated combat for the summary.</summary>
        internal void AccumulatePyre(int damage, int remaining)
            => _turnSummary.AddPyre(damage, remaining);

        /// <summary>Record a hit during automated combat for the summary.</summary>
        internal void AccumulateDamage(object target, string targetName, bool targetIsEnemy, int roomIndex, int amount)
            => _turnSummary.AddDamage(target, targetName, targetIsEnemy, roomIndex, amount);

        /// <summary>
        /// Speak what happened since the player's last turn as one block, then
        /// reset. Called from <see cref="OnPlayerTurnStarted"/> just before the
        /// "Your turn" line, so the turn's draw and start-of-turn effects don't
        /// interleave with it. Speaks nothing when nothing happened.
        /// </summary>
        internal void AnnounceCombatSummary()
        {
            try
            {
                if (!IsInBattle || _turnSummary.IsEmpty)
                    return;

                var parts = _turnSummary.Build(MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value);
                if (parts.Count > 0)
                {
                    string text = (_turnSummary.OnlyArrivals ? "" : "Combat summary. ") + string.Join(". ", parts) + ".";
                    MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                    MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(text);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in AnnounceCombatSummary: {ex.Message}");
            }
            finally
            {
                _turnSummary.Reset();
            }
        }

        #endregion
    }
}
