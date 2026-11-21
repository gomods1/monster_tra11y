using System;
using System.Linq;
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

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Minimap (M on the map): node markers carry their MapNodeData, battle
    /// nodes their ScenarioData; the section has the ring number and name.
    /// "Merchant of Steel, left path, ring 2, The Cliffs of Fallen Souls. Forge..."
    /// </summary>
    internal static class MinimapTextReader
    {
        private static readonly System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        internal static string GetText(UnityEngine.GameObject go)
        {
            try
            {
                var section = go.GetComponentInParent<MinimapSection>();
                string ring = null;
                if (section != null)
                {
                    string Label(string f) => Utilities.TextUtilities.StripRichTextTags(
                        Utilities.UITextHelper.GetRenderedTMPLabelText(typeof(MinimapSection).GetField(f, Flags)?.GetValue(section)) ?? "").Trim();
                    string number = Label("sectionNumberLabel");
                    string name = Label("sectionNameLabel");
                    ring = string.Join(" ", new[] { number, name }.Where(x => !string.IsNullOrEmpty(x)));
                }

                var marker = go.GetComponentInParent<MinimapNodeMarker>();
                if (marker != null)
                {
                    var data = typeof(MinimapNodeMarker).GetField("data", Flags)?.GetValue(marker) as MapNodeData;
                    if (data == null) return null;
                    string title = Utilities.TextUtilities.StripRichTextTags(data.GetTooltipTitle() ?? "").Trim();
                    string body = Utilities.TextUtilities.StripRichTextTags(data.GetTooltipBody() ?? "").Trim();
                    string path = null;
                    for (var t = marker.transform.parent; t != null && t != section?.transform; t = t.parent)
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n.Contains("left")) { path = "left path"; break; }
                        if (n.Contains("right")) { path = "right path"; break; }
                    }
                    var head = new System.Collections.Generic.List<string> { title };
                    if (path != null) head.Add(path);
                    if (!string.IsNullOrEmpty(ring)) head.Add(ring);
                    string text = string.Join(", ", head.Where(x => !string.IsNullOrEmpty(x)));
                    return string.IsNullOrEmpty(body) ? text : $"{text}. {body}";
                }

                var battle = go.GetComponentInParent<MinimapBattleNode>();
                if (battle != null)
                {
                    var scenario = typeof(MinimapBattleNode).GetField("data", Flags)?.GetValue(battle) as ScenarioData;
                    if (scenario == null) return null;
                    bool boss = scenario.GetDifficulty() == ScenarioDifficulty.Boss;
                    string text = $"{(boss ? "Boss battle" : "Battle")}: {Utilities.TextUtilities.StripRichTextTags(scenario.GetBattleName() ?? "")}";
                    return string.IsNullOrEmpty(ring) ? text : $"{text}, {ring}";
                }
            }
            catch (System.Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error reading minimap node: {ex.Message}");
            }
            return null;
        }
    }
}

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// The merchant character in shops (MerchantCharacterUI): selectable, and
    /// Enter makes them talk. Was read as "Merchant Screen".
    /// </summary>
    internal static class MerchantCharacterTextReader
    {
        internal static string GetText(UnityEngine.GameObject go)
        {
            try
            {
                if (go.GetComponentInParent<MerchantCharacterUI>() == null) return null;
                string name = null;
                var screen = UnityEngine.Object.FindObjectOfType<MerchantScreen>();
                var data = typeof(MerchantScreen).GetField("sourceMerchantData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(screen) as MerchantData;
                if (data != null)
                    name = Utilities.TextUtilities.StripRichTextTags(data.GetTooltipTitle() ?? "").Trim();
                if (string.IsNullOrEmpty(name)) name = "Merchant";
                return $"{name}. Press Enter to talk";
            }
            catch
            {
                return null;
            }
        }
    }
}
