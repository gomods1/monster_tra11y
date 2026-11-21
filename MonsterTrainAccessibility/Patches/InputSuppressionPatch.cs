using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Keeps accessibility navigation keys away from the game:
    ///
    /// - While Ctrl is held, the arrow keys belong to the review buffers and
    ///   map cursor (Ctrl+Up/Down/Left/Right). The game would otherwise still
    ///   see the arrows and move the real selection at the same time, causing
    ///   double actions and double announcements.
    /// - While the F1 help list is open, Up/Down browse help entries and
    ///   Enter/Escape close it, so those keys (and Submit/Cancel) must not
    ///   reach the game either.
    /// - While the battle floor review is open, the arrows navigate floors
    ///   and units, Enter reads details, and Escape closes it. The plain Up
    ///   arrow that opens the review is claimed whenever it could open it
    ///   (in battle, no targeting) - keyed off the physical key state like
    ///   the Ctrl checks, because script execution order is undefined and
    ///   the game could otherwise process the opening press first.
    ///
    /// Two input paths are suppressed:
    /// - UnityEngine.EventSystems.BaseInput.GetAxisRaw/GetButtonDown, which
    ///   feed StandaloneInputModule's UI focus navigation (the game's
    ///   GameInputModuleBridge inherits these unchanged)
    /// - ScreenManager.OnInputMappingSignaled, which forwards the game's own
    ///   InputManager.Controls (arrow keys, Enter, Escape via user key
    ///   mappings) to screens - e.g. HandUI cycles the selected card on
    ///   Controls.Left/Right. Only keyboard mappings are suppressed;
    ///   WASD and gamepad input pass through.
    /// </summary>
    public static class InputSuppressionPatch
    {
        private static FieldInfo _deviceIdField;
        private static FieldInfo _keyCodeField;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var baseInputType = typeof(UnityEngine.EventSystems.BaseInput);

                var getAxisRaw = AccessTools.Method(baseInputType, "GetAxisRaw");
                if (getAxisRaw != null)
                {
                    harmony.Patch(getAxisRaw,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(AxisPrefix))));
                }

                var getButtonDown = AccessTools.Method(baseInputType, "GetButtonDown");
                if (getButtonDown != null)
                {
                    harmony.Patch(getButtonDown,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(ButtonPrefix))));
                }

                var screenManagerType = AccessTools.TypeByName("ScreenManager");
                var onMappingSignaled = screenManagerType != null
                    ? AccessTools.Method(screenManagerType, "OnInputMappingSignaled")
                    : null;
                if (onMappingSignaled != null)
                {
                    harmony.Patch(onMappingSignaled,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(MappingPrefix))));
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("Could not patch ScreenManager.OnInputMappingSignaled - Ctrl+arrows may also move the game selection");
                }

                // Debug bridge: while the window is unfocused the game still reads the
                // global cursor position, so hovering moved the UI selection. Report no
                // mouse to the EventSystem then (StandaloneInputModule skips mouse events).
                var bridgeInput = AccessTools.TypeByName("ShinyShoe.GameInputModuleBridge");
                var mousePresent = bridgeInput != null ? AccessTools.PropertyGetter(bridgeInput, "mousePresent") : null;
                if (mousePresent != null)
                {
                    harmony.Patch(mousePresent,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(MousePresentPrefix))));
                }

                var mouseDriverType = AccessTools.TypeByName("ShinyShoe.CoreInputDriverMouse");
                var mouseLateUpdate = mouseDriverType != null ? AccessTools.Method(mouseDriverType, "OnLateUpdate") : null;
                if (mouseLateUpdate != null)
                {
                    harmony.Patch(mouseLateUpdate,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(MouseDriverPrefix))));
                }

                // Debug bridge: feed virtual keys into the game's keyboard driver
                var keyboardDriverType = AccessTools.TypeByName("ShinyShoe.CoreInputDriverKeyboard");
                var driverLateUpdate = keyboardDriverType != null
                    ? AccessTools.Method(keyboardDriverType, "OnLateUpdate")
                    : null;
                if (driverLateUpdate != null)
                {
                    _keyDataField = AccessTools.Field(keyboardDriverType, "_keyData");
                    _lastInputTimeSetter = AccessTools.PropertySetter(AccessTools.TypeByName("ShinyShoe.CoreInputDriver"), "lastInputTime");
                    _updateButtonParams = AccessTools.Method(AccessTools.TypeByName("ShinyShoe.CoreInputDataButton"), "UpdateButtonParams");
                    harmony.Patch(driverLateUpdate,
                        prefix: new HarmonyMethod(typeof(InputSuppressionPatch).GetMethod(nameof(KeyboardDriverPrefix))));
                }

                MonsterTrainAccessibility.LogInfo("Patched accessibility input suppression");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch input suppression: {ex.Message}");
            }
        }

        public static bool AxisPrefix(string __0, ref float __result)
        {
            bool suppress = (IsCtrlHeld() || IsHelpOpen() || IsReviewOpen()) && IsNavigationAxis(__0);

            if (suppress)
            {
                __result = 0f;
                return false;
            }

            if (KeyInput.HasVirtualKeys)
            {
                float v = VirtualAxis(__0);
                if (v != 0f)
                {
                    __result = v;
                    return false;
                }
            }
            return true;
        }

        public static bool ButtonPrefix(string __0, ref bool __result)
        {
            bool suppress = (IsCtrlHeld() || IsHelpOpen() || IsReviewOpen()) && IsNavigationAxis(__0);
            // While help or the floor review is open, Enter and Escape act on
            // the modal instead of the game
            suppress |= (IsHelpOpen() || IsReviewOpen()) && (__0 == "Submit" || __0 == "Cancel");

            if (suppress)
            {
                __result = false;
                return false;
            }

            if (KeyInput.HasVirtualKeys && VirtualButtonDown(__0))
            {
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>Debug bridge: freeze the game's mouse driver while unfocused.</summary>
        public static bool MouseDriverPrefix()
        {
            return !(KeyInput.ForceFocus && !Application.isFocused);
        }

        public static bool MousePresentPrefix(ref bool __result)
        {
            if (KeyInput.ForceFocus && !Application.isFocused)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static FieldInfo _keyDataField;
        private static MethodInfo _lastInputTimeSetter;
        private static MethodInfo _updateButtonParams;

        /// <summary>
        /// Debug bridge: replaces CoreInputDriverKeyboard.OnLateUpdate while virtual
        /// keys are scheduled so they count as held for the game's control mappings.
        /// Same loop as the original, with the virtual state OR'ed in.
        /// </summary>
        public static bool KeyboardDriverPrefix(object __instance)
        {
            if (!KeyInput.HasVirtualKeys || _keyDataField == null || _updateButtonParams == null)
                return true;
            try
            {
                if (!(_keyDataField.GetValue(__instance) is System.Collections.IDictionary keyData))
                    return true;
                var args = new object[1];
                foreach (System.Collections.DictionaryEntry entry in keyData)
                {
                    var code = (KeyCode)entry.Key;
                    bool active = Input.GetKey(code) || KeyInput.VirtualHeld(code);
                    if (active)
                        _lastInputTimeSetter?.Invoke(__instance, new object[] { Time.time });
                    args[0] = active;
                    _updateButtonParams.Invoke(entry.Value, args);
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static float VirtualAxis(string axis)
        {
            if (axis == "Horizontal")
            {
                if (KeyInput.VirtualHeld(KeyCode.RightArrow)) return 1f;
                if (KeyInput.VirtualHeld(KeyCode.LeftArrow)) return -1f;
            }
            else if (axis == "Vertical")
            {
                if (KeyInput.VirtualHeld(KeyCode.UpArrow)) return 1f;
                if (KeyInput.VirtualHeld(KeyCode.DownArrow)) return -1f;
            }
            return 0f;
        }

        private static bool VirtualButtonDown(string button)
        {
            switch (button)
            {
                case "Horizontal":
                    return KeyInput.VirtualDown(KeyCode.RightArrow) || KeyInput.VirtualDown(KeyCode.LeftArrow);
                case "Vertical":
                    return KeyInput.VirtualDown(KeyCode.UpArrow) || KeyInput.VirtualDown(KeyCode.DownArrow);
                case "Submit":
                    return KeyInput.VirtualDown(KeyCode.Return) || KeyInput.VirtualDown(KeyCode.KeypadEnter);
                case "Cancel":
                    return KeyInput.VirtualDown(KeyCode.Escape);
            }
            return false;
        }

        /// <summary>
        /// Swallow keyboard control mappings claimed by the accessibility layer
        /// before ScreenManager forwards them to the active screens.
        /// </summary>
        public static bool MappingPrefix(object __0)
        {
            bool helpOpen = IsHelpOpen();
            bool reviewOpen = IsReviewOpen();
            if (__0 == null || (!IsCtrlHeld() && !helpOpen && !reviewOpen))
                return true;

            try
            {
                if (_deviceIdField == null || _keyCodeField == null)
                {
                    var mappingType = __0.GetType();
                    _deviceIdField = mappingType.GetField("deviceID");
                    _keyCodeField = mappingType.GetField("keyCode");
                }

                if (_deviceIdField?.GetValue(__0)?.ToString() != "Keyboard")
                    return true;

                if (!(_keyCodeField?.GetValue(__0) is KeyCode keyCode))
                    return true;

                bool isArrow = keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow ||
                               keyCode == KeyCode.LeftArrow || keyCode == KeyCode.RightArrow;
                if (isArrow && (IsCtrlHeld() || helpOpen || reviewOpen))
                    return false;

                if (helpOpen &&
                    (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter ||
                     keyCode == KeyCode.Escape || keyCode == KeyCode.Space ||
                     keyCode == KeyCode.Home || keyCode == KeyCode.End))
                {
                    return false;
                }

                if (reviewOpen &&
                    (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter ||
                     keyCode == KeyCode.Escape))
                {
                    return false;
                }
            }
            catch
            {
                // Never break the game's input pipeline
            }

            return true;
        }

        private static bool IsNavigationAxis(string name)
        {
            return name == "Horizontal" || name == "Vertical";
        }

        private static bool IsCtrlHeld()
        {
            return KeyInput.GetKey(KeyCode.LeftControl) || KeyInput.GetKey(KeyCode.RightControl);
        }

        /// <summary>
        /// Includes the frame help was closed: the closing keypress reads as
        /// pressed for the whole frame and must not leak into the game.
        /// </summary>
        private static bool IsHelpOpen()
        {
            var help = MonsterTrainAccessibility.HelpSystem;
            return help != null && (help.IsBrowsing || help.ClosedThisFrame);
        }

        /// <summary>
        /// Includes the frame the floor review was closed, so the Escape that
        /// closed it does not also pause the game.
        /// </summary>
        private static bool IsReviewOpen()
        {
            var review = MonsterTrainAccessibility.FloorReview;
            return review != null && (review.IsActive || review.ClosedThisFrame);
        }

    }
}
