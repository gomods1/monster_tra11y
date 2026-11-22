using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Speak the tab name when the settings dialog switches tabs (Tab / Shift+Tab
    /// or a tab button). The game keeps focus on the same row slot, so without
    /// this the player only hears the first setting of the new tab.
    /// </summary>
    public static class SettingsTabPatch
    {
        private static FieldInfo _tabIndexField;
        private static FieldInfo _tabsField;
        private static FieldInfo _buttonField;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var dialogType = AccessTools.TypeByName("SettingsDialog");
                var method = dialogType != null ? AccessTools.Method(dialogType, "ActivateTab") : null;
                if (method == null)
                {
                    MonsterTrainAccessibility.LogInfo("SettingsDialog.ActivateTab not found");
                    return;
                }
                _tabIndexField = AccessTools.Field(dialogType, "tabIndex");
                _tabsField = AccessTools.Field(dialogType, "tabs");
                _buttonField = AccessTools.Field(AccessTools.TypeByName("SettingsTab"), "button");
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(Prefix))),
                    postfix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(Postfix))));
                MonsterTrainAccessibility.LogInfo("Patched SettingsDialog.ActivateTab");

                // Opening: SettingsDialog.SetActive (override) and the pause dialog
                // (ScreenTransition.SetActive, shared base, filtered by type)
                var settingsSetActive = AccessTools.DeclaredMethod(dialogType, "SetActive");
                if (settingsSetActive != null)
                    harmony.Patch(settingsSetActive,
                        prefix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(OpenPrefix))),
                        postfix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(SettingsOpenPostfix))));
                var transitionType = AccessTools.TypeByName("ScreenTransition");
                var baseSetActive = transitionType != null ? AccessTools.DeclaredMethod(transitionType, "SetActive") : null;
                if (baseSetActive != null)
                    harmony.Patch(baseSetActive,
                        prefix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(OpenPrefix))),
                        postfix: new HarmonyMethod(typeof(SettingsTabPatch).GetMethod(nameof(PauseOpenPostfix))));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SettingsDialog.ActivateTab: {ex.Message}");
            }
        }

        private static bool _opening;

        /// <summary>__state = the dialog was already open.</summary>
        public static void OpenPrefix(object __instance, out bool __state)
        {
            __state = (__instance as ScreenTransition)?.Active == true;
        }

        private static int _pauseOpenedAt = -1;
        private static int _settingsOpenedAt = -1;

        private static System.Collections.IEnumerator AnnouncePauseIfFrontmost(int frame)
        {
            yield return null;
            yield return null;
            if (_settingsOpenedAt >= frame || _pauseOpenedAt != frame) yield break;
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Pause menu");
        }

        public static void SettingsOpenPostfix(object __instance, bool setActive, bool __state)
        {
            _opening = false;
            if (setActive) _settingsOpenedAt = UnityEngine.Time.frameCount;
            try
            {
                if (!setActive || __state) return;
                int index = (int)_tabIndexField.GetValue(__instance);
                string tab = GetTabName(__instance, index);
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen(string.IsNullOrEmpty(tab)
                    ? "Settings. Press Tab to switch between tabs."
                    : $"Settings, {tab} tab. Press Tab to switch between tabs.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing settings dialog: {ex.Message}");
            }
        }

        public static void PauseOpenPostfix(object __instance, bool setActive, bool __state)
        {
            try
            {
                if (__instance is SettingsDialog && setActive && !__state)
                    _opening = true; // the override's ActivateTab(tabs[0]) follows
                if (!(__instance is PauseDialog) || !setActive || __state) return;
                // Settings from the main menu also activates the pause dialog under the
                // settings dialog: wait a moment and only speak if settings did not open
                _pauseOpenedAt = UnityEngine.Time.frameCount;
                MonsterTrainAccessibility.MenuHandler?.StartCoroutine(AnnouncePauseIfFrontmost(_pauseOpenedAt));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing pause dialog: {ex.Message}");
            }
        }

        public static void Prefix(object __instance, out int __state)
        {
            __state = -1;
            try { __state = (int)_tabIndexField.GetValue(__instance); } catch { }
        }

        public static void Postfix(object __instance, int __state)
        {
            try
            {
                int index = (int)_tabIndexField.GetValue(__instance);
                // -1 / _opening = the dialog opening on its first tab (announced with the dialog)
                if (_opening || __state < 0 || index == __state) return;
                string name = GetTabName(__instance, index);
                if (!string.IsNullOrEmpty(name))
                    MonsterTrainAccessibility.ScreenReader?.Speak($"{name} tab", false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SettingsTabPatch: {ex.Message}");
            }
        }

        internal static string GetTabName(object dialog, int index)
        {
            if (!(_tabsField?.GetValue(dialog) is System.Collections.IList tabs) || index < 0 || index >= tabs.Count)
                return null;
            var button = _buttonField?.GetValue(tabs[index]) as UnityEngine.Component;
            if (button == null) return null;
            string text = UITextHelper.GetTMPText(button.gameObject);
            if (string.IsNullOrEmpty(text))
                text = UITextHelper.CleanGameObjectName(button.gameObject.name);
            return TextUtilities.StripRichTextTags(text ?? "").Trim();
        }
    }
}
