using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using MonsterTrainAccessibility.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Development bridge for driving the game from outside without window focus
    /// (enabled by the Debug.Bridge config entry, off by default).
    ///
    /// - Keeps the game running and accepting input in the background
    ///   (Application.runInBackground, CoreInput's focus flag, EventSystem focus).
    /// - Reads commands from BepInEx/accessibility_bridge_cmd.txt (the file is
    ///   consumed and deleted), one per line:
    ///     key &lt;combo&gt; [combo...]  virtual keypress, e.g. "key down", "key ctrl+up", "key shift+k"
    ///     wait &lt;ms&gt;              pause the command queue
    ///     state                  screen tracker / battle / selection summary
    ///     focus                  selected object path + the mod's text for it
    ///     selectables            list active interactable Selectables (numbered)
    ///     select &lt;n&gt;             set EventSystem selection to entry n of the last list
    ///     submit                 send a submit event to the current selection
    ///     screens                list the game's active screens
    ///     mark &lt;text&gt;            write a marker line into the speech log
    /// - Writes every spoken string to BepInEx/accessibility_speech_log.txt and
    ///   command results to BepInEx/accessibility_bridge_out.txt.
    ///
    /// Virtual keys reach the mod via <see cref="KeyInput"/> and the game via
    /// InputSuppressionPatch (EventSystem axes/buttons and the game's keyboard driver).
    /// </summary>
    public class DebugBridge : MonoBehaviour
    {
        public static DebugBridge Instance { get; private set; }

        private static string _cmdPath;
        private static string _outPath;
        private static string _speechPath;
        private static readonly object _speechLock = new object();

        private readonly Queue<string> _commands = new Queue<string>();
        private float _resumeAt;
        private float _nextPoll;
        private List<GameObject> _lastSelectables = new List<GameObject>();

        private static FieldInfo _coreInputFocusField;
        private static FieldInfo _eventSystemFocusField;
        private static bool _reflectionDone;

        public static bool Enabled =>
            MonsterTrainAccessibility.AccessibilitySettings?.DebugBridge?.Value == true;

        /// <summary>Bridge active with muting on: speech is logged only, game audio silenced.</summary>
        public static bool Muted =>
            Enabled && MonsterTrainAccessibility.AccessibilitySettings?.DebugBridgeMute?.Value == true;

        private void Awake()
        {
            Instance = this;
            if (Enabled)
                Application.runInBackground = true;
            string root = Paths.BepInExRootPath;
            _cmdPath = Path.Combine(root, "accessibility_bridge_cmd.txt");
            _outPath = Path.Combine(root, "accessibility_bridge_out.txt");
            _speechPath = Path.Combine(root, "accessibility_speech_log.txt");
            if (Enabled)
            {
                try
                {
                    File.WriteAllText(_speechPath, $"Speech log started {DateTime.Now:HH:mm:ss}\n");
                    File.WriteAllText(_outPath, "");
                    if (File.Exists(_cmdPath)) File.Delete(_cmdPath);
                }
                catch { }
                MonsterTrainAccessibility.LogInfo($"Debug bridge enabled: {_cmdPath}");
            }
        }

        /// <summary>Record a spoken string (called from ScreenReaderOutput).</summary>
        public static void LogSpeech(string text)
        {
            if (!Enabled || _speechPath == null || string.IsNullOrEmpty(text)) return;
            try
            {
                lock (_speechLock)
                    File.AppendAllText(_speechPath, $"[{Time.frameCount} {Time.realtimeSinceStartup:F2}] {text}\n");
            }
            catch { }
        }

        private void Update()
        {
            if (!Enabled)
            {
                KeyInput.ForceFocus = false;
                return;
            }

            KeyInput.ForceFocus = true;
            KeepFocused();
            KeyInput.Cleanup();

            float now = Time.realtimeSinceStartup;
            if (now >= _nextPoll)
            {
                _nextPoll = now + 0.1f;
                PollCommandFile();
            }

            while (_commands.Count > 0 && Time.realtimeSinceStartup >= _resumeAt)
            {
                string cmd = _commands.Dequeue();
                try
                {
                    Execute(cmd);
                }
                catch (Exception ex)
                {
                    Write($"! {cmd}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void KeepFocused()
        {
            Application.runInBackground = true;
            if (Muted)
                MuteGameAudio();
            else
                RestoreGameAudio();
            try
            {
                if (!_reflectionDone)
                {
                    _reflectionDone = true;
                    var coreInput = ReflectionHelper.FindType("ShinyShoe.CoreInput");
                    _coreInputFocusField = coreInput?.GetField("isApplicationFocused",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _eventSystemFocusField = typeof(EventSystem).GetField("m_HasFocus",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                _coreInputFocusField?.SetValue(null, true);
                if (EventSystem.current != null)
                    _eventSystemFocusField?.SetValue(EventSystem.current, true);
            }
            catch { }
        }

        private static PropertyInfo _audioVolume;
        private static bool _mutedAudio;

        /// <summary>Undo the bridge's audio mute (when muting is turned off).</summary>
        private static void RestoreGameAudio()
        {
            if (!_mutedAudio || _audioVolume == null) return;
            try { _audioVolume.SetValue(null, 1f, null); } catch { }
            _mutedAudio = false;
        }
        private static bool _audioSearched;

        private static void MuteGameAudio()
        {
            try
            {
                if (!_audioSearched)
                {
                    _audioSearched = true;
                    var listener = Type.GetType("UnityEngine.AudioListener, UnityEngine.AudioModule");
                    _audioVolume = listener?.GetProperty("volume", BindingFlags.Public | BindingFlags.Static);
                }
                if (_audioVolume != null && (float)_audioVolume.GetValue(null, null) != 0f)
                {
                    _audioVolume.SetValue(null, 0f, null);
                    _mutedAudio = true;
                }
            }
            catch { }
        }

        private void PollCommandFile()
        {
            try
            {
                if (!File.Exists(_cmdPath)) return;
                var lines = File.ReadAllLines(_cmdPath);
                File.Delete(_cmdPath);
                foreach (var line in lines)
                {
                    var l = line.Trim();
                    if (l.Length > 0 && !l.StartsWith("#")) _commands.Enqueue(l);
                }
            }
            catch { /* file still being written; retry next poll */ }
        }

        private static void Write(string line)
        {
            try { File.AppendAllText(_outPath, line + "\n"); } catch { }
        }

        private void Execute(string cmd)
        {
            var parts = cmd.Split(new[] { ' ' }, 2);
            string verb = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";

            switch (verb)
            {
                case "key":
                    // One combo per queue step so each gets its own frames and
                    // clears the mod's 0.15s input cooldown
                    var combos = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (combos.Length == 0) return;
                    PressCombo(combos[0]);
                    var rest = string.Join(" ", combos.Skip(1).ToArray());
                    _resumeAt = Time.realtimeSinceStartup + 0.25f;
                    if (rest.Length > 0)
                    {
                        // Re-queue the remaining combos at the front
                        var remaining = new List<string> { "key " + rest };
                        remaining.AddRange(_commands);
                        _commands.Clear();
                        foreach (var c in remaining) _commands.Enqueue(c);
                    }
                    break;
                case "wait":
                    if (int.TryParse(arg, out int ms))
                        _resumeAt = Time.realtimeSinceStartup + ms / 1000f;
                    break;
                case "mark":
                    LogSpeechForce($"---- {arg} ----");
                    Write($"> mark {arg}");
                    break;
                case "state":
                    Write("> state\n" + DescribeState());
                    break;
                case "focus":
                    Write("> focus\n" + DescribeFocus());
                    break;
                case "selectables":
                    Write("> selectables\n" + ListSelectables());
                    break;
                case "select":
                    if (int.TryParse(arg, out int idx) && idx >= 0 && idx < _lastSelectables.Count && _lastSelectables[idx] != null)
                    {
                        EventSystem.current?.SetSelectedGameObject(_lastSelectables[idx]);
                        Write($"> select {idx}: {GetPath(_lastSelectables[idx])}");
                    }
                    else Write($"> select {arg}: invalid index");
                    break;
                case "submit":
                    var sel = EventSystem.current?.currentSelectedGameObject;
                    if (sel != null)
                    {
                        ExecuteEvents.Execute(sel, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                        Write($"> submit {GetPath(sel)}");
                    }
                    else Write("> submit: nothing selected");
                    break;
                case "navinfo":
                    {
                        var cur = EventSystem.current?.currentSelectedGameObject;
                        var navSel = cur != null ? cur.GetComponent<Selectable>() : null;
                        if (navSel == null) { Write("> navinfo: no selectable"); break; }
                        var nav = navSel.navigation;
                        Write($"> navinfo {GetPath(cur)} mode={nav.mode} interactable={navSel.IsInteractable()} " +
                              $"left={GetPath(navSel.FindSelectableOnLeft()?.gameObject)} right={GetPath(navSel.FindSelectableOnRight()?.gameObject)} " +
                              $"up={GetPath(navSel.FindSelectableOnUp()?.gameObject)} down={GetPath(navSel.FindSelectableOnDown()?.gameObject)} " +
                              $"module={EventSystem.current?.currentInputModule?.GetType().Name} sendNav={EventSystem.current?.sendNavigationEvents}");
                    }
                    break;
                case "screens":
                    Write("> screens\n" + ListScreens());
                    break;
                default:
                    Write($"! unknown command: {cmd}");
                    break;
            }
        }

        private static void LogSpeechForce(string text)
        {
            try
            {
                lock (_speechLock)
                    File.AppendAllText(_speechPath, $"[{Time.frameCount} {Time.realtimeSinceStartup:F2}] {text}\n");
            }
            catch { }
        }

        private static void PressCombo(string combo)
        {
            var tokens = combo.Split('+');
            int start = Time.frameCount + 2;
            var keys = new List<KeyCode>();
            foreach (var t in tokens)
            {
                if (TryParseKey(t, out var k)) keys.Add(k);
                else Write($"! unknown key '{t}' in {combo}");
            }
            if (keys.Count == 0) return;
            // Modifiers go down a frame early and are released a frame late
            for (int i = 0; i < keys.Count - 1; i++)
                KeyInput.SchedulePress(keys[i], start - 1, 5);
            KeyInput.SchedulePress(keys[keys.Count - 1], start, 2);
            Write($"> key {combo}");
        }

        private static bool TryParseKey(string token, out KeyCode key)
        {
            string t = token.Trim().ToLowerInvariant();
            switch (t)
            {
                case "up": key = KeyCode.UpArrow; return true;
                case "down": key = KeyCode.DownArrow; return true;
                case "left": key = KeyCode.LeftArrow; return true;
                case "right": key = KeyCode.RightArrow; return true;
                case "enter": case "return": key = KeyCode.Return; return true;
                case "esc": case "escape": key = KeyCode.Escape; return true;
                case "ctrl": key = KeyCode.LeftControl; return true;
                case "shift": key = KeyCode.LeftShift; return true;
                case "alt": key = KeyCode.LeftAlt; return true;
                case "pgup": key = KeyCode.PageUp; return true;
                case "pgdn": key = KeyCode.PageDown; return true;
                case "space": key = KeyCode.Space; return true;
                case "tab": key = KeyCode.Tab; return true;
                case "back": case "backspace": key = KeyCode.Backspace; return true;
            }
            if (t.Length == 1 && char.IsDigit(t[0]))
            {
                key = KeyCode.Alpha0 + (t[0] - '0');
                return true;
            }
            try
            {
                key = (KeyCode)Enum.Parse(typeof(KeyCode), token.Trim(), true);
                return true;
            }
            catch
            {
                key = KeyCode.None;
                return false;
            }
        }

        private static string DescribeState()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"  frame={Time.frameCount} tracker={Help.ScreenStateTracker.CurrentScreen} prev={Help.ScreenStateTracker.PreviousScreen}");
            var battle = MonsterTrainAccessibility.BattleHandler;
            sb.AppendLine($"  inBattle={battle?.IsInBattle} phase={Patches.CombatPhaseChangePatch.CurrentPhase} frontmost={Battle.FloorReviewSystem.IsBattleScreenFrontmost()}");
            sb.AppendLine($"  review={MonsterTrainAccessibility.FloorReview?.IsActive} floorTargeting={Battle.FloorTargetingSystem.Instance?.IsTargeting} unitTargeting={Battle.UnitTargetingSystem.Instance?.IsTargeting} help={MonsterTrainAccessibility.HelpSystem?.IsBrowsing}");
            sb.Append(DescribeFocus());
            return sb.ToString();
        }

        private static string DescribeFocus()
        {
            var go = EventSystem.current?.currentSelectedGameObject;
            if (go == null) return "  selected=null";
            string text = null;
            try { text = MonsterTrainAccessibility.MenuHandler?.DebugGetText(go); } catch (Exception ex) { text = "error " + ex.Message; }
            return $"  selected={GetPath(go)} active={go.activeInHierarchy}\n  text={text}";
        }

        private string ListSelectables()
        {
            _lastSelectables = new List<GameObject>();
            var sb = new StringBuilder();
            var current = EventSystem.current?.currentSelectedGameObject;
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) continue;
                int i = _lastSelectables.Count;
                _lastSelectables.Add(s.gameObject);
                string label = UITextHelper.GetTMPText(s.gameObject);
                if (label != null && label.Length > 60) label = label.Substring(0, 60);
                sb.AppendLine($"  [{i}]{(s.gameObject == current ? "*" : "")} {GetPath(s.gameObject)} | {label}");
            }
            return sb.ToString();
        }

        private static string ListScreens()
        {
            var sb = new StringBuilder();
            try
            {
                var sm = ReflectionHelper.FindManager("ScreenManager");
                if (sm == null) return "  no ScreenManager";
                var listField = sm.GetType().GetField("activeScreensSorted", BindingFlags.NonPublic | BindingFlags.Instance);
                if (!(listField?.GetValue(sm) is IList list)) return "  no activeScreensSorted";
                foreach (var item in list)
                {
                    var t = item.GetType();
                    var name = t.GetField("name")?.GetValue(item);
                    var screen = t.GetField("screen")?.GetValue(item);
                    var active = t.GetField("active")?.GetValue(item);
                    object interactable = null;
                    try { interactable = screen?.GetType().GetMethod("IsInteractable", Type.EmptyTypes)?.Invoke(screen, null); } catch { }
                    sb.AppendLine($"  {name} active={active} interactable={interactable}");
                }
            }
            catch (Exception ex) { sb.AppendLine("  error " + ex.Message); }
            return sb.ToString();
        }

        private static string GetPath(GameObject go)
        {
            if (go == null) return "null";
            var names = new List<string>();
            var t = go.transform;
            for (int i = 0; t != null && i < 5; i++, t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
