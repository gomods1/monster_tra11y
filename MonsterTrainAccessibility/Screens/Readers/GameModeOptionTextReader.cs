using System;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Reads the game mode option panels on the main menu's Standard Run /
    /// challenge dialogs (SingleplayerOptionsDialog): the Continue game option
    /// ("Active Run", with the saved run's clans, covenant and ring shown as
    /// icons) and the New game option. Their buttons only say "Resume" or
    /// "Start", so the panel's meaning and the saved run's details are added.
    /// </summary>
    internal static class GameModeOptionTextReader
    {
        internal static string GetGameModeOptionText(GameObject go)
        {
            try
            {
                var continueOption = UITextHelper.FindComponentInHierarchy(go, "ContinueGameOption");
                if (continueOption != null)
                {
                    // Stat icons inside the panel (distance, covenant, clan
                    // icons) carry tooltips; read them when focused
                    if (!IsPlayButton(go))
                    {
                        string tip = GetOwnTooltip(go);
                        if (!string.IsNullOrEmpty(tip))
                            return tip;
                    }

                    string summary = GetSavedRunSummary(continueOption);
                    string label = IsPlayButton(go) ? GetLabel(go) ?? "Resume" : GetLabel(go);
                    if (string.IsNullOrEmpty(summary))
                        return IsPlayButton(go) ? $"Active run: {label}" : null;
                    return IsPlayButton(go) ? $"Active run: {summary}. {label}" : $"Active run: {summary}";
                }

                var option = UITextHelper.FindComponentInHierarchy(go, "GameModeOption");
                if (option != null && IsPlayButton(go))
                {
                    string title = GetOptionTitle(option);
                    string label = GetLabel(go) ?? "Start";
                    return string.IsNullOrEmpty(title) ? label : $"{title}: {label}";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading game mode option: {ex.Message}");
            }
            return null;
        }

        private static bool IsPlayButton(GameObject go)
        {
            return go.name.IndexOf("Play button", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetLabel(GameObject go)
        {
            string text = UITextHelper.GetTMPText(go)?.Trim();
            return string.IsNullOrEmpty(text) || text.Length <= 2 ? null : text;
        }

        /// <summary>A tooltip on the focused object itself (not an ancestor).</summary>
        private static string GetOwnTooltip(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "TooltipProviderComponent")
                {
                    string text = TooltipTextReader.GetTooltipTextWithBody(go);
                    return string.IsNullOrEmpty(text) ? null : TextUtilities.StripRichTextTags(text);
                }
            }
            return null;
        }

        /// <summary>
        /// The option's heading ("New Run", "Active Run"): the first rendered
        /// label in the panel that is not on the play button.
        /// </summary>
        private static string GetOptionTitle(Component option)
        {
            foreach (var tmp in option.GetComponentsInChildren<Component>())
            {
                if (tmp == null || !tmp.GetType().Name.Contains("TextMeshPro")) continue;
                if (tmp.transform.GetComponentInParent<UnityEngine.UI.Selectable>() != null) continue;
                string text = UITextHelper.GetRenderedTMPLabelText(tmp)?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 2)
                    return TextUtilities.StripRichTextTags(text);
            }
            return null;
        }

        private static string GetSavedRunSummary(Component continueOption)
        {
            var saveManager = ReflectionHelper.FindManager("SaveManager");
            if (saveManager == null) return null;
            var smType = saveManager.GetType();
            var parts = new List<string>();

            string main = GetClassTitle(Invoke(saveManager, smType, "GetMainClass"));
            string sub = GetClassTitle(Invoke(saveManager, smType, "GetSubClass"));
            string champion = GetChampionName(Invoke(saveManager, smType, "GetMainChampionData"));
            if (!string.IsNullOrEmpty(main))
            {
                string clans = string.IsNullOrEmpty(sub) ? main : $"{main} and {sub}";
                parts.Add(string.IsNullOrEmpty(champion) ? clans : $"{clans}, champion {champion}");
            }

            if (Invoke(saveManager, smType, "GetAscensionLevel") is int covenant && covenant > 0)
                parts.Add($"covenant {covenant}");

            // The distance icon's tooltip title names the ring ("Ring 3: ...")
            foreach (var c in continueOption.GetComponentsInChildren<Component>())
            {
                if (c != null && c.GetType().Name == "RunDistanceDisplay")
                {
                    TooltipTextReader.GetTooltipTitleAndBody(c.gameObject, out string ring, out _);
                    if (!string.IsNullOrEmpty(ring))
                        parts.Add(TextUtilities.StripRichTextTags(ring));
                    break;
                }
            }

            if (Invoke(saveManager, smType, "GetTowerHP") is int hp && hp > 0)
            {
                parts.Add(Invoke(saveManager, smType, "GetMaxTowerHP") is int max && max > 0
                    ? $"pyre {hp} of {max}"
                    : $"pyre {hp}");
            }
            if (Invoke(saveManager, smType, "GetGold") is int gold && gold >= 0)
                parts.Add($"{gold} gold");

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static object Invoke(object target, Type type, string method)
        {
            try
            {
                return type.GetMethod(method, Type.EmptyTypes)?.Invoke(target, null);
            }
            catch
            {
                return null;
            }
        }

        private static string GetClassTitle(object classData)
        {
            if (classData == null) return null;
            return Invoke(classData, classData.GetType(), "GetTitle") as string;
        }

        private static string GetChampionName(object championData)
        {
            if (championData == null) return null;
            var cardData = championData.GetType().GetField("championCardData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(championData);
            if (cardData == null) return null;
            return Invoke(cardData, cardData.GetType(), "GetName") as string;
        }
    }
}
