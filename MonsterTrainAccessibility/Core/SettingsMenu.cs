using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Accessible settings list for the mod's own options (the settings key,
    /// F9 by default, on any screen). It is speech-only and modal, like the F1
    /// help list: while it is open InputSuppressionPatch keeps every keyboard
    /// key away from the game.
    ///
    /// Up/Down choose a setting, Home/End jump, Left/Right or Enter change it,
    /// the re-read key (F2) reads its description, Escape or the settings key
    /// close. Key settings: Enter, then press the new key (Escape cancels,
    /// Backspace clears an optional key); keys the game itself uses are refused
    /// so a mod key never triggers a game action. BepInEx saves each change to
    /// the config file immediately. The first item, Language / Idioma, switches
    /// the mod (and, for Spanish, the game's text) between English and Spanish.
    /// </summary>
    public class SettingsMenu
    {
        private class Item
        {
            public string Label;
            public ConfigEntryBase Entry;
            public string Hint;
        }

        private readonly List<Item> _items = new List<Item>();
        private int _index;
        private bool _capturing;
        private int _closedFrame = -1;

        public bool IsOpen { get; private set; }

        /// <summary>True during the frame the menu closed (its closing key must not reach the game).</summary>
        public bool ClosedThisFrame => _closedFrame == Time.frameCount;

        // UserKeyMappingHelper defaults: the game's own keyboard controls
        private static readonly HashSet<KeyCode> GameKeys = new HashSet<KeyCode>
        {
            KeyCode.E, KeyCode.Return, KeyCode.R, KeyCode.Q, KeyCode.Backspace, KeyCode.Escape,
            KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home,
            KeyCode.End, KeyCode.Z, KeyCode.X, KeyCode.V, KeyCode.B, KeyCode.C, KeyCode.M,
            KeyCode.F, KeyCode.Tab, KeyCode.LeftShift, KeyCode.Alpha1, KeyCode.Alpha2,
            KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7,
            KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Y, KeyCode.U, KeyCode.I,
            KeyCode.O, KeyCode.P, KeyCode.N, KeyCode.F8, KeyCode.T, KeyCode.H, KeyCode.Space,
            KeyCode.KeypadEnter
        };

        private static readonly HashSet<KeyCode> Modifiers = new HashSet<KeyCode>
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftCommand, KeyCode.RightCommand,
            KeyCode.LeftWindows, KeyCode.RightWindows, KeyCode.AltGr
        };

        private void Build()
        {
            var c = MonsterTrainAccessibility.AccessibilitySettings;
            _items.Clear();
            void Add(string label, ConfigEntryBase entry, string hint = null)
            {
                if (entry != null) _items.Add(new Item { Label = label, Entry = entry, Hint = hint });
            }

            // Language first, so a player who doesn't read the current one finds it
            Add("Language, Idioma", c.Language);
            // Announcements
            Add("Combat summary instead of live combat narration", c.CombatSummaryMode);
            Add("Announce damage", c.AnnounceDamage);
            Add("Announce deaths", c.AnnounceDeaths);
            Add("Announce units entering and being summoned", c.AnnounceSpawns);
            Add("Announce status effects", c.AnnounceStatusEffects);
            Add("Announce card draws", c.AnnounceCardDraws);
            Add("Announce artifact triggers", c.AnnounceRelicTriggers);
            Add("Announce unit and merchant chatter", c.AnnounceDialogue);
            Add("Scene descriptions: game launch, run start, rings, bosses, run end", c.SceneDescriptions);
            // Speech
            Add("Verbosity", c.VerbosityLevel);
            Add("Braille output", c.EnableBraille);
            // Keys
            Add("Help key", c.HelpKey);
            Add("Settings key", c.SettingsKey);
            Add("Re-read focused item key", c.ReadCurrentKey);
            Add("Read all screen text key", c.ReadTextKey);
            Add("Cycle verbosity key", c.ToggleVerbosityKey);
            Add("Read gold key", c.ReadGoldKey);
            Add("Read pyre health key", c.ReadPyreHealthKey);
            Add("Read pact shards key", c.ReadPactShardsKey);
            Add("Read hand key, battle", c.ReadHandKey);
            Add("Read selected floor key, battle", c.ReadCurrentFloorKey);
            Add("Read all floors key, battle", c.ReadFloorsKey);
            Add("Read all units key, battle, with Shift", c.ReadEnemiesKey);
            Add("Floor review key, battle, with Shift", c.FloorReviewKey);
            Add("Read ember key, battle", c.ReadEmberKey);
            Add("Extra map node key, optional", c.ReadMapNodeKey);
            Add("Extra run summary key, optional", c.ReadRunSummaryKey);
        }

        public void Open()
        {
            if (MonsterTrainAccessibility.AccessibilitySettings == null) return;
            Build();
            if (_items.Count == 0) return;
            IsOpen = true;
            _capturing = false;
            _index = 0;
            Speak($"Accessibility settings, {_items.Count} settings. Up and Down choose, Left, Right or Enter change, " +
                  $"{KeyName(MonsterTrainAccessibility.AccessibilitySettings.ReadCurrentKey.Value)} reads the description, Escape closes. " +
                  "Changes are saved right away");
            AnnounceItem();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _capturing = false;
            _closedFrame = Time.frameCount;
            Speak("Settings closed");
        }

        /// <summary>Handle this frame's keys. Returns true when a key was used.</summary>
        public bool HandleInput()
        {
            if (!IsOpen) return false;
            var config = MonsterTrainAccessibility.AccessibilitySettings;

            if (_capturing)
                return HandleCapture();

            if (KeyInput.GetKeyDown(KeyCode.Escape) || KeyInput.GetKeyDown(config.SettingsKey.Value))
            {
                Close();
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (_index < _items.Count - 1) _index++;
                AnnounceItem();
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (_index > 0) _index--;
                AnnounceItem();
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                _index = 0;
                AnnounceItem();
                return true;
            }
            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                _index = _items.Count - 1;
                AnnounceItem();
                return true;
            }
            if (KeyInput.GetKeyDown(config.ReadCurrentKey.Value))
            {
                string desc = _items[_index].Entry.Description?.Description;
                Speak(string.IsNullOrEmpty(desc) ? "No description" : desc);
                return true;
            }

            bool right = KeyInput.GetKeyDown(KeyCode.RightArrow);
            bool left = KeyInput.GetKeyDown(KeyCode.LeftArrow);
            bool enter = KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter) ||
                         KeyInput.GetKeyDown(KeyCode.Space);
            if (right || left || enter)
            {
                Change(_items[_index], left ? -1 : 1, enter);
                return true;
            }
            return false;
        }

        private void Change(Item item, int direction, bool enter)
        {
            var entry = item.Entry;
            switch (entry.BoxedValue)
            {
                case bool b:
                    entry.BoxedValue = !b;
                    break;
                case KeyCode _:
                    if (!enter)
                    {
                        Speak("Press Enter, then the new key");
                        return;
                    }
                    _capturing = true;
                    Speak($"Press the new key for {item.Label}. Escape cancels, Backspace clears it");
                    return;
                case Translation.Language lang:
                    var next = lang == Translation.Language.English ? Translation.Language.Spanish : Translation.Language.English;
                    entry.BoxedValue = next;
                    Translation.ModLanguage.Apply(next, refreshGame: true);
                    Speak(next == Translation.Language.Spanish
                        ? "Español. Reinicia el juego para que todo el texto cambie de idioma. El idioma del juego debe estar en inglés"
                        : "English. Restart the game so all text changes language");
                    return;
                case Enum e:
                    var values = Enum.GetValues(e.GetType());
                    int i = Array.IndexOf(values, e);
                    i = (i + direction + values.Length) % values.Length;
                    entry.BoxedValue = values.GetValue(i);
                    break;
                case int n:
                    entry.BoxedValue = n + direction;
                    break;
                case float f:
                    entry.BoxedValue = (float)Math.Round(f + 0.1f * direction, 2);
                    break;
                default:
                    Speak("This setting can only be changed in the config file");
                    return;
            }
            Speak(FormatValue(entry.BoxedValue));
        }

        private bool HandleCapture()
        {
            if (KeyInput.GetKeyDown(KeyCode.Escape))
            {
                _capturing = false;
                Speak("Cancelled. " + FormatValue(_items[_index].Entry.BoxedValue));
                return true;
            }

            var item = _items[_index];
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                _capturing = false;
                if (item.Label.Contains("optional"))
                {
                    item.Entry.BoxedValue = KeyCode.None;
                    Speak("Cleared");
                }
                else
                {
                    Speak("This key can't be empty. " + FormatValue(item.Entry.BoxedValue));
                }
                return true;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || Modifiers.Contains(key) || key >= KeyCode.Mouse0) continue;
                if (!KeyInput.GetKeyDown(key)) continue;

                _capturing = false;
                if (GameKeys.Contains(key))
                {
                    Speak($"{KeyName(key)} is one of the game's own keys, choose another. Press Enter to try again");
                    return true;
                }
                item.Entry.BoxedValue = key;
                string clash = FindClash(item, key);
                Speak(clash == null ? $"Set to {KeyName(key)}" : $"Set to {KeyName(key)}. Also used by {clash}");
                return true;
            }
            return false;
        }

        /// <summary>Another mod key on the same key (Shift pairs like K / Shift+K are fine).</summary>
        private string FindClash(Item changed, KeyCode key)
        {
            bool changedShift = changed.Label.Contains("with Shift");
            foreach (var other in _items)
            {
                if (other == changed || !(other.Entry.BoxedValue is KeyCode k) || k != key) continue;
                if (other.Label.Contains("with Shift") != changedShift) continue;
                return other.Label;
            }
            return null;
        }

        private void AnnounceItem()
        {
            var item = _items[_index];
            Speak($"{item.Label}: {FormatValue(item.Entry.BoxedValue)}. {_index + 1} of {_items.Count}");
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case bool b: return b ? "on" : "off";
                case KeyCode k: return KeyName(k);
                case Translation.Language l: return l == Translation.Language.Spanish ? "Español" : "English";
                default: return value?.ToString() ?? "none";
            }
        }

        internal static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "none";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Escape";
                case KeyCode.BackQuote: return "Backquote";
                default:
                    if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                        return ((int)(key - KeyCode.Alpha0)).ToString();
                    return key.ToString();
            }
        }

        private static void Speak(string text) => MonsterTrainAccessibility.ScreenReader?.Speak(text, false);
    }
}
