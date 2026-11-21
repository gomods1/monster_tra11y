using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Keeps ScreenStateTracker in step with the screen that actually has focus.
    ///
    /// The screen patches only see screens opening, so closing one (leaving a
    /// shop or event, closing a pile view, collecting a reward) left the tracker
    /// on the closed screen: wrong F1 help, wrong map/battle readers. The game
    /// recomputes which screens take input in ScreenManager.UpdateInteractable
    /// whenever the screen stack changes; after it runs, the first active screen
    /// from the top that isn't an always-on layer (Loading, Hud) is frontmost.
    ///
    /// Only screens with an unambiguous tracker state are synced. Game maps to
    /// Battle only while combat runs (the post-battle VictoryUI lives on the game
    /// screen and is tracked as Rewards by VictoryUIPatch); GameOver is left to
    /// its own patch, which decides Victory vs Defeat.
    /// </summary>
    public static class ScreenFocusSyncPatch
    {
        private static FieldInfo _activeScreensField;
        private static FieldInfo _nameField;
        private static FieldInfo _activeField;

        private static readonly Dictionary<ScreenName, Help.GameScreen> Map = new Dictionary<ScreenName, Help.GameScreen>
        {
            { ScreenName.Map, Help.GameScreen.Map },
            { ScreenName.Merchant, Help.GameScreen.Shop },
            { ScreenName.Reward, Help.GameScreen.Rewards },
            { ScreenName.Draft, Help.GameScreen.CardDraft },
            { ScreenName.StoryEvent, Help.GameScreen.Event },
            { ScreenName.MainMenu, Help.GameScreen.MainMenu },
            { ScreenName.ClassSelection, Help.GameScreen.ClanSelection },
            { ScreenName.Deck, Help.GameScreen.DeckView },
            { ScreenName.RelicChoice, Help.GameScreen.RelicDraft },
            { ScreenName.ChampionUpgrade, Help.GameScreen.ChampionUpgrade },
            { ScreenName.Settings, Help.GameScreen.Settings },
            { ScreenName.Dialog, Help.GameScreen.Dialog },
            { ScreenName.Compendium, Help.GameScreen.Compendium },
            { ScreenName.Minimap, Help.GameScreen.Minimap },
            { ScreenName.BattleIntro, Help.GameScreen.BattleIntro },
            { ScreenName.RunSummary, Help.GameScreen.RunSummary },
            { ScreenName.UnitSynthesis, Help.GameScreen.Synthesis },
            { ScreenName.ChallengeDetails, Help.GameScreen.ChallengeDetails },
            { ScreenName.ChallengeOverview, Help.GameScreen.ChallengeOverview },
            { ScreenName.Credits, Help.GameScreen.Credits },
            { ScreenName.KeyMapping, Help.GameScreen.KeyMapping },
            { ScreenName.RunHistory, Help.GameScreen.RunHistory },
            { ScreenName.RunOpening, Help.GameScreen.RunOpening },
        };

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(typeof(ScreenManager), "UpdateInteractable");
                _activeScreensField = AccessTools.Field(typeof(ScreenManager), "activeScreensSorted");
                if (method == null || _activeScreensField == null)
                {
                    MonsterTrainAccessibility.LogWarning("ScreenFocusSyncPatch: UpdateInteractable not found");
                    return;
                }
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(ScreenFocusSyncPatch).GetMethod(nameof(Postfix))));
                MonsterTrainAccessibility.LogInfo("Patched ScreenManager.UpdateInteractable for screen tracking");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UpdateInteractable: {ex.Message}");
            }
        }

        /// <summary>Re-derive the tracker from the current screen stack (for patches that set it out of order).</summary>
        public static void SyncNow()
        {
            if (_activeScreensField == null) return;
            var sm = Utilities.ReflectionHelper.FindManager("ScreenManager") as ScreenManager;
            if (sm != null) Postfix(sm);
        }

        public static void Postfix(ScreenManager __instance)
        {
            try
            {
                if (!(_activeScreensField.GetValue(__instance) is IList screens))
                    return;

                foreach (var item in screens)
                {
                    if (item == null) continue;
                    if (_nameField == null)
                    {
                        _nameField = item.GetType().GetField("name");
                        _activeField = item.GetType().GetField("active");
                    }
                    if (!(_nameField?.GetValue(item) is ScreenName name)) continue;
                    if (_activeField?.GetValue(item) is bool active && !active) continue;

                    // Always-on layers never count as the frontmost screen
                    if (name == ScreenName.Loading || name == ScreenName.Hud || name == ScreenName.NONE ||
                        name == ScreenName.Cheat || name == ScreenName.IntroLogos || name == ScreenName.IntroFmv ||
                        name == ScreenName.FeedbackEntry)
                        continue;

                    Help.GameScreen target;
                    if (name == ScreenName.Game)
                    {
                        if (MonsterTrainAccessibility.BattleHandler?.IsInBattle != true)
                            return;
                        target = Help.GameScreen.Battle;
                    }
                    else if (!Map.TryGetValue(name, out target))
                    {
                        return; // GameOver, ... keep their own patch's state
                    }

                    if (ScreenStateTracker.CurrentScreen != target)
                        ScreenStateTracker.SetScreen(target);
                    return;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Screen focus sync failed: {ex.Message}");
            }
        }
    }
}
