using System;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Run-end score rows (BattleScoreUI on the game over / run summary screens):
    /// each row is selectable but its only text is the score ("+5000"). Reads the
    /// event name, the score, and the breakdown the row's tooltip shows.
    /// </summary>
    internal static class ScoreTextReader
    {
        private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        internal static string GetBattleScoreText(GameObject go)
        {
            try
            {
                var row = go.GetComponentInParent<BattleScoreUI>();
                if (row == null) return null;

                string Label(string field) => TextUtilities.StripRichTextTags(
                    UITextHelper.GetRenderedTMPLabelText(typeof(BattleScoreUI).GetField(field, Flags)?.GetValue(row)) ?? "").Trim();

                var parts = new List<string>();
                string eventName = Label("eventLabel");
                string score = Label("scoreLabel");
                if (!string.IsNullOrEmpty(eventName)) parts.Add(eventName);
                if (!string.IsNullOrEmpty(score)) parts.Add(score);

                foreach (var tip in row.tooltips)
                {
                    string body = TextUtilities.StripRichTextTags(tip.body ?? "").Replace("\r", "").Trim();
                    if (string.IsNullOrEmpty(body)) continue;
                    parts.Add(string.Join(", ", body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)));
                }
                return parts.Count > 0 ? string.Join(". ", parts) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading score row: {ex.Message}");
                return null;
            }
        }
    }
}

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Run History rows (RunAggregateDataRow): the row's only label reads as the
    /// prefab's placeholder date. Built from the run's own data instead: result,
    /// clans, run type, covenant, battles won, score and date.
    /// </summary>
    internal static class RunHistoryTextReader
    {
        private static readonly System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        internal static string GetRunRowText(UnityEngine.GameObject go)
        {
            try
            {
                var row = go.GetComponentInParent<RunAggregateDataRow>();
                if (row == null) return null;
                var t = typeof(RunAggregateDataRow);
                if (!(t.GetField("runData", Flags)?.GetValue(row) is RunAggregateData data)) return null;
                var allGameData = t.GetField("allGameData", Flags)?.GetValue(row) as AllGameData;

                var parts = new System.Collections.Generic.List<string>();
                parts.Add(data.GetVictory() ? "Victory" : "Defeat");
                string main = allGameData?.FindClassData(data.GetMainClassID())?.GetTitle();
                string sub = allGameData?.FindClassData(data.GetSubClassID())?.GetTitle();
                if (!string.IsNullOrEmpty(main))
                    parts.Add(string.IsNullOrEmpty(sub) ? main : $"{main} and {sub}");
                string runType = Utilities.TextUtilities.StripRichTextTags(
                    Utilities.UITextHelper.GetRenderedTMPLabelText(t.GetField("runTypeLabel", Flags)?.GetValue(row)) ?? "").Trim();
                if (!string.IsNullOrEmpty(runType)) parts.Add(runType);
                if (data.GetAscensionLevel() > 0) parts.Add($"covenant {data.GetAscensionLevel()}");
                int won = data.GetNumBattlesWon();
                parts.Add(won == 1 ? "1 battle won" : $"{won} battles won");
                parts.Add($"score {data.GetScore():N0}");
                var date = LocalizationUtil.GetDateTimeFromCultureCalendar(data.GetSaveDate()).ToLocalTime();
                parts.Add(date.ToString("g", LocalizationUtil.CurrentCulture));
                return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading run history row: {ex.Message}");
                return null;
            }
        }
    }
}

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Small run-summary widgets that have no readable label of their own:
    /// currency score rows (gold, pact crystals) and covenant level icons.
    /// </summary>
    internal static class RunWidgetTextReader
    {
        private static readonly System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        internal static string GetText(UnityEngine.GameObject go)
        {
            try
            {
                var currency = go.GetComponentInParent<CurrencyScoreRow>();
                if (currency != null)
                {
                    string label = Utilities.TextUtilities.StripRichTextTags(
                        Utilities.UITextHelper.GetRenderedTMPLabelText(
                            typeof(CurrencyScoreRow).GetField("label", Flags)?.GetValue(currency)) ?? "").Trim();
                    TooltipTextReader.GetTooltipTitleAndBody(go, out string title, out string body);
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrEmpty(title)) parts.Add(Utilities.TextUtilities.StripRichTextTags(title));
                    if (!string.IsNullOrEmpty(label)) parts.Add(label);
                    if (!string.IsNullOrEmpty(body)) parts.Add(Utilities.TextUtilities.StripRichTextTags(body));
                    return parts.Count > 0 ? string.Join(". ", parts) : null;
                }

                var covenant = go.GetComponentInParent<ChallengeCovenantUI>();
                if (covenant != null)
                {
                    var rank = typeof(ChallengeCovenantUI).GetField("rankLabel", Flags)?.GetValue(covenant) as UnityEngine.Behaviour;
                    string level = rank != null && rank.enabled
                        ? Utilities.UITextHelper.GetRenderedTMPLabelText(rank)?.Trim()
                        : null;
                    TooltipTextReader.GetTooltipTitleAndBody(go, out string title, out string body);
                    string text = !string.IsNullOrEmpty(level) ? $"Covenant {level}" : "Covenant";
                    if (!string.IsNullOrEmpty(title)) text = Utilities.TextUtilities.StripRichTextTags(title);
                    if (!string.IsNullOrEmpty(body)) text += ". " + Utilities.TextUtilities.StripRichTextTags(body);
                    return text;
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading run widget: {ex.Message}");
            }
            return null;
        }
    }
}
