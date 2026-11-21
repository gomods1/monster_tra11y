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
