using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using MonsterTrainAccessibility.Utilities;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect reward screen (post-battle rewards)
    /// </summary>
    public static class RewardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RewardScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Show");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(RewardScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched RewardScreen.{method.Name}");
                    }

                    // Battle start: HeroManager.ShowOffEnemyRelics reuses the reward
                    // screen to show the enemy's artifacts - that is not a reward
                    var showDisplayable = AccessTools.Method(targetType, "ShowDisplayableRewards");
                    if (showDisplayable != null)
                    {
                        harmony.Patch(showDisplayable,
                            prefix: new HarmonyMethod(typeof(RewardScreenPatch).GetMethod(nameof(ShowDisplayablePrefix))));
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RewardScreen: {ex.Message}");
            }
        }

        private static int _enemyShowcaseFrame = -100;

        public static void ShowDisplayablePrefix(System.Collections.IEnumerable rewards)
        {
            try
            {
                if (rewards == null) return;
                var parts = new List<string>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var r in rewards)
                {
                    if (!(r is RelicDisplayedAsReward shown)) continue;
                    if (!(typeof(RelicDisplayedAsReward).GetField("relicRewardType", flags)?.GetValue(shown) is RewardType type) ||
                        type != RewardType.EnemyRelic)
                        continue;
                    if (!(typeof(RelicDisplayedAsReward).GetField("relicState", flags)?.GetValue(shown) is RelicState relic))
                        continue;
                    string name = relic.GetName();
                    string desc = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(relic.GetDescription() ?? "")).Trim().TrimEnd('.');
                    parts.Add(string.IsNullOrEmpty(desc) ? name : $"{name}: {desc}");
                }
                if (parts.Count == 0) return;
                _enemyShowcaseFrame = UnityEngine.Time.frameCount;
                string text = (parts.Count == 1 ? "Enemy artifact. " : "Enemy artifacts. ") +
                              string.Join(". ", parts) + ". Press Enter to continue.";
                MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
                MonsterTrainAccessibility.ScreenReader?.LogCombatEvent(text);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading enemy artifacts: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Rewards);
                // Defer a frame: ShowDisplayableRewards runs right after Initialize
                // and tells us whether this is the enemy artifact showcase
                MonsterTrainAccessibility.MenuHandler?.StartCoroutine(AutoReadRewardsDeferred(__instance));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RewardScreen patch: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator AutoReadRewardsDeferred(object screen)
        {
            yield return null;
            yield return null;
            if (UnityEngine.Time.frameCount - _enemyShowcaseFrame <= 3)
                yield break;
            // A map node's reward (banner, hoard, Hellvent) opens its draft or
            // deck screen right on top of the reward screen, which then
            // initializes underneath and claims the tracker; re-derive it from
            // the screen stack. The screen on top announces itself.
            ScreenFocusSyncPatch.SyncNow();
            if (ScreenStateTracker.CurrentScreen != Help.GameScreen.Rewards)
                yield break;
            AutoReadRewards(screen);
        }

        private static void AutoReadRewards(object screen)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("Rewards. ");

                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Try to get reward list - try multiple field names and properties
                var rewardsField = screenType.GetField("rewards", bindingFlags) ??
                                   screenType.GetField("_rewards", bindingFlags) ??
                                   screenType.GetField("rewardStates", bindingFlags) ??
                                   screenType.GetField("rewardDataList", bindingFlags) ??
                                   screenType.GetField("currentRewards", bindingFlags) ??
                                   screenType.GetField("_rewardStates", bindingFlags) ??
                                   screenType.GetField("rewardNodes", bindingFlags);

                System.Collections.IList rewards = null;
                if (rewardsField != null)
                {
                    rewards = rewardsField.GetValue(screen) as System.Collections.IList;
                }

                // Fallback: try properties if fields didn't work
                if (rewards == null)
                {
                    var rewardsProp = screenType.GetProperty("Rewards", bindingFlags) ??
                                     screenType.GetProperty("RewardStates", bindingFlags);
                    if (rewardsProp != null)
                    {
                        rewards = rewardsProp.GetValue(screen) as System.Collections.IList;
                    }
                }

                if (rewards != null && rewards.Count > 0)
                {
                    sb.Append(rewards.Count == 1 ? "1 reward: " : $"{rewards.Count} rewards: ");
                    foreach (var reward in rewards)
                    {
                        string rewardName = GetRewardDisplayName(reward);
                        if (!string.IsNullOrEmpty(rewardName))
                        {
                            sb.Append($"{rewardName}, ");
                        }
                    }
                }

                sb.Append("Navigate with arrows. Enter to collect.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading rewards: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Rewards. Navigate with arrows. Enter to collect.", false);
            }
        }

        /// <summary>
        /// "100 gold", "+5 pyre health" when the detail is a quantity; otherwise
        /// "Title, detail".
        /// </summary>
        internal static string FormatTitleAndDetail(string title, string detail)
        {
            // Stripped icon sprites leave "Gain 50 gold ." behind
            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+([.,!?])", "$1").Trim().TrimEnd('.');
            detail = (detail ?? "").Trim();
            if (string.IsNullOrEmpty(detail) || title.Contains(detail))
                return title;
            bool isQuantity = System.Text.RegularExpressions.Regex.IsMatch(detail, @"^[+\-x×]?[\d,.]+%?$");
            return isQuantity ? $"{detail} {title.ToLowerInvariant()}" : $"{title}, {detail}";
        }

        /// <summary>
        /// Focused reward icon (RewardItemUI on the post-battle victory panel and
        /// reward screens): "100 gold. Description".
        /// </summary>
        internal static string GetRewardItemText(UnityEngine.GameObject go)
        {
            try
            {
                var item = go.GetComponentInParent<RewardItemUIBase>();
                var data = item?.rewardState?.RewardData;
                if (data == null) return null;
                string name = GetRewardDisplayName(item.rewardState);
                if (string.IsNullOrEmpty(name)) return null;
                string desc = TextUtilities.StripRichTextTags(data.RewardDescription ?? "").Trim();
                return string.IsNullOrEmpty(desc) || desc == name ? name : $"{name}. {desc}";
            }
            catch
            {
                return null;
            }
        }

        internal static string GetRewardDisplayName(object reward)
        {
            if (reward == null) return null;

            try
            {
                // The game's own localized title and detail ("Gold", "75";
                // "Pyre Health", "+5") - what the reward icon shows
                var grantable = (reward as RewardState)?.RewardData ?? reward as GrantableRewardData;
                if (grantable != null)
                {
                    string title = TextUtilities.StripRichTextTags(grantable.RewardTitle ?? "").Trim();
                    string detail = TextUtilities.StripRichTextTags(grantable.RewardDetail ?? "").Trim();
                    if (!string.IsNullOrEmpty(title))
                        return FormatTitleAndDetail(title, detail);
                }

                var rewardType = reward.GetType();

                // Try to get reward data from RewardState
                var getDataMethod = rewardType.GetMethod("GetRewardData") ??
                                    rewardType.GetMethod("GetData");
                object rewardData = getDataMethod?.Invoke(reward, null) ?? reward;

                var dataType = rewardData.GetType();
                string typeName = dataType.Name;

                // Map type names to readable names
                if (typeName.Contains("Gold")) return "Gold";
                if (typeName.Contains("Health")) return "Pyre Health";
                if (typeName.Contains("Crystal")) return "Crystals";
                if (typeName.Contains("RelicDraft") || typeName.Contains("RelicPool")) return "Artifact Choice";
                if (typeName.Contains("Relic")) return "Artifact";
                if (typeName.Contains("CardPool") || typeName.Contains("Draft")) return "Card Draft";
                if (typeName.Contains("Card")) return "Card";
                if (typeName.Contains("Enhancer") || typeName.Contains("Upgrade")) return "Upgrade";
                if (typeName.Contains("Purge")) return "Card Purge";
                if (typeName.Contains("Synthesis")) return "Unit Synthesis";
                if (typeName.Contains("ChampionUpgrade")) return "Champion Upgrade";
                if (typeName.Contains("Merchant")) return "Shop";
                if (typeName.Contains("MapSkip")) return "Map Skip";

                // Try GetTitle for any other type
                var getTitleMethod = dataType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    string title = getTitleMethod.Invoke(rewardData, null) as string;
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_"))
                        return title;
                }

                // Try _rewardTitleKey field and localize it
                var titleKeyField = dataType.GetField("_rewardTitleKey",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleKeyField != null)
                {
                    string titleKey = titleKeyField.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        string localized = TryLocalizeStatic(titleKey);
                        if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("_"))
                            return localized;
                    }
                }

                // Fallback: clean up type name
                return typeName.Replace("RewardData", "").Replace("RewardState", "").Replace("Data", "");
            }
            catch
            {
                return "Reward";
            }
        }

        private static MethodInfo _cachedLocalizeMethod;
        private static bool _localizeSearched;

        private static string TryLocalizeStatic(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            try
            {
                if (!_localizeSearched)
                {
                    _localizeSearched = true;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
                            continue;
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsClass || !type.IsAbstract || !type.IsSealed)
                                continue;
                            var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                            if (method != null && method.ReturnType == typeof(string))
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _cachedLocalizeMethod = method;
                                    break;
                                }
                            }
                        }
                        if (_cachedLocalizeMethod != null) break;
                    }
                }

                if (_cachedLocalizeMethod != null)
                {
                    var parameters = _cachedLocalizeMethod.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = key;
                    for (int i = 1; i < args.Length; i++)
                    {
                        args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }
                    var result = _cachedLocalizeMethod.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(result) && result != key)
                        return result;
                }
            }
            catch { }
            return key;
        }
    }
}
