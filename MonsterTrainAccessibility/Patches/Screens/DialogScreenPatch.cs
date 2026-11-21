using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;
using System.Text;
using MonsterTrainAccessibility.Utilities;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Detect dialog/popup screen
    /// </summary>
    public static class DialogScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Dialog.Show is the funnel every dialog goes through:
                // DialogScreen.ShowDialog, ShowConfirmationDialog and
                // ShowNotificationDialog (abandon run, deck/challenge
                // confirmations, game notices) all end up here.
                var dialogShow = AccessTools.Method(typeof(Dialog), "Show", new[] { typeof(Dialog.Data) });
                if (dialogShow != null)
                {
                    harmony.Patch(dialogShow, postfix: new HarmonyMethod(typeof(DialogScreenPatch).GetMethod(nameof(DialogShowPostfix))));
                    MonsterTrainAccessibility.LogInfo("Patched Dialog.Show");
                    return;
                }

                var targetType = AccessTools.TypeByName("DialogScreen");
                if (targetType == null)
                {
                    targetType = AccessTools.TypeByName("DialogPopup");
                }

                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "ShowDialog") ??
                                 AccessTools.Method(targetType, "Show") ??
                                 AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(DialogScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched {targetType.Name}.{method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DialogScreen: {ex.Message}");
            }
        }

        private static string _lastContent;
        private static float _lastContentTime;

        /// <summary>
        /// True when this dialog text was just spoken on open, so the focus
        /// reader for its buttons can say only the button label.
        /// </summary>
        internal static bool WasJustAnnounced(string dialogText)
        {
            if (string.IsNullOrEmpty(_lastContent) || string.IsNullOrEmpty(dialogText))
                return false;
            if (UnityEngine.Time.unscaledTime - _lastContentTime > 30f)
                return false;
            return Normalize(dialogText) == _lastContent;
        }

        private static string Normalize(string text)
        {
            text = TextUtilities.StripRichTextTags(text ?? "");
            return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        }

        public static void DialogShowPostfix(Dialog.Data data)
        {
            try
            {
                if (data == null || string.IsNullOrEmpty(data.content))
                    return;
                ScreenStateTracker.SetScreen(Help.GameScreen.Dialog);
                string content = Normalize(TextUtilities.CleanSpriteTagsForSpeech(data.content));
                _lastContent = Normalize(data.content);
                _lastContentTime = UnityEngine.Time.unscaledTime;
                MonsterTrainAccessibility.ScreenReader?.Speak($"Dialog: {content}", false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in Dialog.Show patch: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Dialog);
                AutoReadDialog(__instance);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DialogScreen patch: {ex.Message}");
            }
        }

        private static void AutoReadDialog(object screen)
        {
            try
            {
                var screenType = screen.GetType();
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var sb = new StringBuilder();
                sb.Append("Dialog. ");

                // Try to get dialog content text
                string[] textFieldNames = { "contentText", "messageText", "bodyText", "dialogText", "content", "_content", "message", "_message" };
                foreach (var fieldName in textFieldNames)
                {
                    var field = screenType.GetField(fieldName, bindingFlags);
                    if (field != null)
                    {
                        var value = field.GetValue(screen);
                        if (value != null)
                        {
                            // Could be a TMP_Text component or a string
                            string text = null;
                            if (value is string str)
                            {
                                text = str;
                            }
                            else
                            {
                                var textProp = value.GetType().GetProperty("text");
                                if (textProp != null)
                                {
                                    text = textProp.GetValue(value) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.Append(Screens.BattleAccessibility.StripRichTextTags(text));
                                sb.Append(" ");
                                break;
                            }
                        }
                    }
                }

                sb.Append("Press Enter to confirm or Escape to cancel.");
                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error auto-reading dialog: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Dialog. Press Enter to confirm or Escape to cancel.", false);
            }
        }
    }
}
