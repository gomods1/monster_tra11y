using System.Collections.Generic;
using UnityEngine;

namespace MonsterTrainAccessibility
{
    /// <summary>
    /// Keyboard state for the mod's own hotkey handling: the physical keys
    /// (UnityEngine.Input) OR'ed with virtual presses injected by the debug
    /// bridge (<see cref="Core.DebugBridge"/>). All mod key checks go through
    /// here so the game can be driven from outside without window focus.
    ///
    /// A virtual press is frame-scheduled: down on its start frame, held from
    /// start until (not including) its end frame, up on its end frame. Because
    /// every reader compares against Time.frameCount, the result does not depend
    /// on script execution order within a frame.
    /// </summary>
    public static class KeyInput
    {
        private struct VirtualPress
        {
            public int Start;
            public int End;
        }

        private static readonly Dictionary<KeyCode, VirtualPress> _virtual = new Dictionary<KeyCode, VirtualPress>();

        /// <summary>True while the debug bridge wants the game to behave as focused.</summary>
        public static bool ForceFocus { get; set; }

        /// <summary>Application.isFocused, or always true while the debug bridge is active.</summary>
        public static bool AppFocused => ForceFocus || Application.isFocused;

        public static bool HasVirtualKeys => _virtual.Count > 0;

        /// <summary>Realtime of the last physical or virtual key press.</summary>
        public static float LastKeyTime { get; private set; } = -100f;

        /// <summary>Record a key press (called each frame by InputInterceptor when any key went down).</summary>
        public static void MarkKeyPressed() => LastKeyTime = Time.realtimeSinceStartup;

        /// <summary>Realtime of the last navigation key press (arrows, Tab).</summary>
        public static float LastNavKeyTime { get; private set; } = -100f;

        public static void MarkNavKeyPressed() => LastNavKeyTime = Time.realtimeSinceStartup;

        public static bool NavKeyPressedWithin(float seconds) => Time.realtimeSinceStartup - LastNavKeyTime <= seconds;

        /// <summary>True when a key was pressed within the last <paramref name="seconds"/>.</summary>
        public static bool KeyPressedWithin(float seconds) => Time.realtimeSinceStartup - LastKeyTime <= seconds;

        /// <summary>
        /// Schedule a virtual press: key goes down on <paramref name="startFrame"/> and
        /// is released on <paramref name="startFrame"/> + <paramref name="holdFrames"/>.
        /// </summary>
        public static void SchedulePress(KeyCode key, int startFrame, int holdFrames)
        {
            if (holdFrames < 1) holdFrames = 1;
            MarkKeyPressed();
            _virtual[key] = new VirtualPress { Start = startFrame, End = startFrame + holdFrames };
        }

        /// <summary>Drop presses whose release frame has passed.</summary>
        public static void Cleanup()
        {
            if (_virtual.Count == 0) return;
            int frame = Time.frameCount;
            List<KeyCode> done = null;
            foreach (var kv in _virtual)
            {
                if (frame > kv.Value.End)
                    (done ?? (done = new List<KeyCode>())).Add(kv.Key);
            }
            if (done != null)
                foreach (var k in done) _virtual.Remove(k);
        }

        public static bool VirtualHeld(KeyCode key)
        {
            if (!_virtual.TryGetValue(key, out var p)) return false;
            int f = Time.frameCount;
            return f >= p.Start && f < p.End;
        }

        public static bool VirtualDown(KeyCode key)
        {
            return _virtual.TryGetValue(key, out var p) && Time.frameCount == p.Start;
        }

        public static bool VirtualUp(KeyCode key)
        {
            return _virtual.TryGetValue(key, out var p) && Time.frameCount == p.End;
        }

        public static bool GetKey(KeyCode key) => Input.GetKey(key) || VirtualHeld(key);

        public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key) || VirtualDown(key);

        public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key) || VirtualUp(key);
    }
}
