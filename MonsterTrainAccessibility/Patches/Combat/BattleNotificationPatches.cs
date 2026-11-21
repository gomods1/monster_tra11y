using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Battle events a sighted player sees as popups or on-board changes that
    /// had no announcement:
    /// - Unit trigger popups ("Summon", "Slay", "Revenge", "Harvest", "Immune"...)
    ///   via CharacterState.ShowNotification (Trigger and General sources only;
    ///   damage/heal popups are announced by the damage patches). The game
    ///   itself skips preview mode here.
    /// - Your own units being summoned: CardStatistics.OnCharacterAdded runs once
    ///   a monster's Setup coroutine has finished (the older Setup /
    ///   InstantiateCharacter hooks fire before the unit has a name).
    /// - Cards created mid-battle (CardManager.AddCard): Scourges added by
    ///   enemies, "add X to your hand" effects.
    /// - The "choose a card from your hand" prompt (HandSelectionUI.Open).
    /// </summary>
    public static class BattleNotificationPatches
    {
        private static readonly HashSet<int> _announcedMonsters = new HashSet<int>();

        public static void TryPatch(Harmony harmony)
        {
            TryPatchOne(harmony, typeof(CharacterState), "ShowNotification", prefix: nameof(ShowNotificationPrefix));
            TryPatchOne(harmony, typeof(CardStatistics), "OnCharacterAdded", postfix: nameof(OnCharacterAddedPostfix));
            TryPatchOne(harmony, typeof(CardManager), "AddCard", postfix: nameof(AddCardPostfix));
            TryPatchOne(harmony, typeof(HandSelectionUI), "Open", postfix: nameof(HandSelectionOpenPostfix));
            TryPatchOne(harmony, typeof(SaveManager), "AddCardToDeck", postfix: nameof(AddCardToDeckPostfix));
        }

        private static void TryPatchOne(Harmony harmony, Type type, string method, string prefix = null, string postfix = null)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null)
                {
                    MonsterTrainAccessibility.LogWarning($"BattleNotificationPatches: {type.Name}.{method} not found");
                    return;
                }
                harmony.Patch(target,
                    prefix: prefix != null ? new HarmonyMethod(typeof(BattleNotificationPatches).GetMethod(prefix)) : null,
                    postfix: postfix != null ? new HarmonyMethod(typeof(BattleNotificationPatches).GetMethod(postfix)) : null);
                MonsterTrainAccessibility.LogInfo($"Patched {type.Name}.{method}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch {type.Name}.{method}: {ex.Message}");
            }
        }

        private static bool IsPreview()
        {
            try
            {
                return (ReflectionHelper.FindManager("SaveManager") as SaveManager)?.PreviewMode == true;
            }
            catch
            {
                return false;
            }
        }

        public static void ShowNotificationPrefix(CharacterState __instance, string message, PopupNotificationUI.Source source)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(message) || __instance.PreviewMode)
                    return;
                if (source != PopupNotificationUI.Source.Trigger && source != PopupNotificationUI.Source.General)
                    return;
                string text = TextUtilities.StripRichTextTags(message).Trim().TrimEnd('!', '.');
                if (string.IsNullOrEmpty(text))
                    return;
                MonsterTrainAccessibility.BattleHandler?.OnUnitNotification(__instance.GetName(), text, __instance.GetTeamType() == Team.Type.Heroes);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in unit notification patch: {ex.Message}");
            }
        }

        public static void OnCharacterAddedPostfix(CharacterState addedChar)
        {
            try
            {
                if (addedChar == null || addedChar.PreviewMode || IsPreview())
                    return;
                if (addedChar.GetTeamType() != Team.Type.Monsters || addedChar.IsPyreHeart())
                    return;
                int id = RuntimeHelpers.GetHashCode(addedChar);
                if (!_announcedMonsters.Add(id))
                    return;
                if (_announcedMonsters.Count > 500)
                    _announcedMonsters.Clear();
                int roomIndex = CharacterStateHelper.GetRoomIndex(addedChar);
                MonsterTrainAccessibility.BattleHandler?.OnUnitSpawned(addedChar.GetName(), false, roomIndex);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in monster added patch: {ex.Message}");
            }
        }

        public static void AddCardPostfix(CardData cardData, CardPile targetPile, CardState __result)
        {
            try
            {
                if (__result == null || cardData == null || IsPreview())
                    return;
                if (MonsterTrainAccessibility.BattleHandler?.IsInBattle != true)
                    return;
                string pile;
                switch (targetPile)
                {
                    case CardPile.HandPile: pile = "your hand"; break;
                    case CardPile.DiscardPile: pile = "your discard pile"; break;
                    case CardPile.ExhaustedPile: pile = "the consumed pile"; break;
                    case CardPile.EatenPile: pile = "the eaten pile"; break;
                    case CardPile.DeckPile:
                    case CardPile.DeckPileTop:
                    case CardPile.DeckPileRandom: pile = "your draw pile"; break;
                    default: return;
                }
                MonsterTrainAccessibility.BattleHandler.OnCardAdded(cardData.GetName(), pile);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in add card patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Out of battle: a card joined the deck (draft pick, shop purchase, event).
        /// Skipped in battle (CardManager.AddCard reports it) and outside run
        /// screens (the starter deck is built through here too).
        /// </summary>
        public static void AddCardToDeckPostfix(CardData cardData, CardState __result)
        {
            try
            {
                if (__result == null || cardData == null || IsPreview())
                    return;
                if (MonsterTrainAccessibility.BattleHandler?.IsInBattle == true)
                    return;
                switch (Help.ScreenStateTracker.CurrentScreen)
                {
                    case Help.GameScreen.CardDraft:
                    case Help.GameScreen.Rewards:
                    case Help.GameScreen.Shop:
                    case Help.GameScreen.Event:
                    case Help.GameScreen.Map:
                    case Help.GameScreen.RelicDraft:
                        break;
                    default:
                        return;
                }
                MonsterTrainAccessibility.ScreenReader?.Queue($"{cardData.GetName()} added to your deck");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in add card to deck patch: {ex.Message}");
            }
        }

        public static void HandSelectionOpenPostfix(HandSelectionUI.Params parameters)
        {
            try
            {
                string text = LocalizationHelper.LocalizeOrNull(parameters.descriptionKey);
                if (string.IsNullOrEmpty(text))
                    return;
                text = TextUtilities.StripRichTextTags(text);
                MonsterTrainAccessibility.ScreenReader?.Queue(text);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in hand selection patch: {ex.Message}");
            }
        }
    }
}
