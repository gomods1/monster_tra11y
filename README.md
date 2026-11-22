# Monster Train Accessibility Mod

A comprehensive accessibility mod for Monster Train that enables totally blind players to fully enjoy the game through screen reader integration and complete keyboard navigation.

## Features

- **Full Screen Reader Support**: Works with NVDA, JAWS, Window-Eyes, and Windows Narrator (SAPI)
- **Complete Keyboard Navigation**: Navigate all game elements without a mouse
- **Battle Accessibility**: Read cards in hand, floor status, enemy info and intents
- **Menu Navigation**: Full access to main menu, settings, clan selection
- **Card Draft Support**: Browse and select cards with full descriptions
- **Map Navigation**: Choose your path through events, shops, and battles
- **Configurable Verbosity**: Choose how much detail you want announced
- **Braille Display Support**: Text sent to braille display if available

## Requirements

- Monster Train (Steam version)
- BepInEx 5.4.x mod loader
- A screen reader (NVDA recommended) or Windows Narrator
- Windows operating system

## Installation

### Step 1: Install BepInEx

1. Open Steam and go to the [Monster Train Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=2187468759) on Steam Workshop
2. Click **Subscribe** to download the mod loader
3. For detailed modding instructions, see the [Official Monster Train Modding Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2257843164)

### Step 2: Install the Mod

1. Download the latest release of this mod
2. Extract **all contents** directly to your Monster Train game folder:
   - Default location: `C:\Program Files (x86)\Steam\steamapps\common\Monster Train`
3. The folder structure should look like:
   ```
   Monster Train/
   ├── BepInEx/
   │   ├── core/
   │   ├── plugins/    (create if not exists)
   │   └── config/     (created automatically)
   ├── MonsterTrain.exe
   ├── winhttp.dll     (BepInEx loader - must be here)
   └── doorstop_config.ini
   └── tolk.dll, NVDAControllerClient64.dll, etc
   ```
4. Launch Monster Train once to let BepInEx initialize

### Step 3: Launch the game

1. Start your screen reader (NVDA, JAWS, or enable Windows Narrator)
2. Launch Monster Train
3. You should hear "Monster Train Accessibility loaded" when the game starts

## Playing in Spanish / Jugar en español

The mod adds Spanish to the game's own language list: a fan-made translation of the game's text (completed for the current game version) and a Spanish version of everything the mod says (announcements, F1 help, F9 settings, scene descriptions). Nothing in the game's files changes: the translation lives in `BepInEx\plugins\Spanish`.

In the game, open Settings, the Other tab, and pick **Español** in the Language dropdown (the last option). The game and the mod switch right away. The mod always speaks the game's language; to force one, set `ModLanguage` in `BepInEx\config\com.accessibility.monstertrain.cfg`. Full instructions in Spanish: `jugar_en_español.txt` in the download.

## Keyboard Controls

### Navigation
| Key | Action |
|-----|--------|
| Arrow Keys | Navigate between items |
| Enter | Select / Activate current item |
| Space | Alternate select key |
| Escape | Go back / Cancel |

### Main Menu: The Last Divinity Toggle

If you own The Last Divinity DLC, the main menu has a toggle that enables or disables it for your next run. From the menu options, press the **Right arrow**, then **Up**, to reach the toggle. Press **Enter** to switch it - the toggle announces whether the DLC is on or off. Press the **Left arrow** to return to the menu options.

### No Game Keys Taken

Every mod key is a key the game itself does not use, so pressing a mod key never also triggers a game action (opening a pile, toggling tooltips, sending an emote). The mod only adds announcements after a few game keys: Tab (train stats), N (game speed) and Page Up/Down (floor change).

Keys changed in this version (old installs are migrated automatically; keys you rebound yourself are kept):

| Action | Old key | New key |
|--------|---------|---------|
| Re-read focused item | C | F2 |
| Read all screen text / run summary | T / S | F3 |
| Cycle verbosity | V | F4 |
| Gold | Ctrl + G | F5 |
| Pyre health | Ctrl + H | F6 |
| Pact shards and threat | Ctrl + R | F7 |
| Read hand | H | J |
| Read all units with detail | U | Shift + L |
| Read ember | R | G |
| Open floor review | Up arrow | Shift + K |
| Re-read map node | M | F2 |

### Information Hotkeys (any screen)
| Key | Action |
|-----|--------|
| F1 | Open the help list for the current screen (Up/Down read entries, Home/End jump, F1, Enter or Escape close). Each list ends with the keys that work everywhere |
| F2 | Re-read current focused item (for cards, shop items, and artifacts this reads the full details, not just the short focus announcement) |
| F3 | Read all text on screen (patch notes, descriptions, the run summary after a run) |
| F4 | Cycle verbosity level (Minimal/Normal/Verbose) |
| F5 | Read current gold |
| F6 | Read current Pyre health |
| F7 | Read pact shards and threat level (The Last Divinity runs) |
| Tab | Read train stats (pyre health, gold, deck size) - the game's own HUD key, the mod reads the panel it opens |

### Review Buffers (Ctrl + Arrow Keys)

Buffers are review lists for extra information, working the same way as in the Monster Train 2 accessibility mod. They are separate from normal focus navigation: moving focus chooses what information is available, and the buffer commands let you inspect that information without moving focus away.

Focus announcements stay short on purpose: cards announce their name, cost, and stats; shop items announce their name, price, and whether you can afford them; artifacts announce just their name. The full details - rarity, type, descriptions, and keyword explanations - are always waiting in the buffers below.

| Key | Action |
|-----|--------|
| Ctrl + Up | Read the next item in the current buffer |
| Ctrl + Down | Move back toward the top of the buffer |
| Ctrl + Right | Switch to the next available buffer |
| Ctrl + Left | Switch to the previous available buffer |

The current item sits at the Ctrl+Down side of a buffer: from the starting point, the first Ctrl+Up reads the current/top item (the newest event, or the first detail line), and further presses move up through the buffer. Ctrl+Down brings you back down toward the current item.

Available buffers:
- **UI**: Text and details for the currently focused UI element.
- **Events**: Every combat announcement (damage, deaths, status effects, card plays, turns) is recorded here. If several things were announced at once, step through them at your own pace; the first Ctrl+Up reads the newest event and further presses go back in history.
- **Card**: Full details for the focused card (name, cost, effects, keywords).
- **Creature**: Full details for the unit currently selected while targeting a spell.
- **Artifact**: Full details for the focused artifact.
- **Reward**: Details for the focused reward element.
- **Story**: The current story event's narrative text, one piece at a time.
- **Hand** (battle only): One item per card, with cost, type, and description.
- **Floors** (battle only): One item per floor, with capacity, corruption, and units.
- **Units** (battle only): One detailed item per unit on the train, floor by floor.
- **Resources** (battle only): Ember, gold, pyre health, cards in hand, and DLC info as separate items.

When the game's **Lore Tooltips** setting is enabled (game settings), the flavor text the game shows for cards, units, and artifacts is included as a final "Lore:" item in the Card, Creature, and Artifact details.

Only buffers that currently have relevant information are available; the others are skipped when cycling. While Ctrl is held, the arrow keys never move the game's real selection, so reviewing is always safe. The buffer position is remembered per buffer while you switch around, and resets when the underlying content changes.

### Map Browsing (Ctrl + Arrow Keys on the Map)

On the map screen, the Ctrl+arrows drive a **virtual map cursor** instead, so you can explore the whole run without moving your real selection:

| Key | Action |
|-----|--------|
| Ctrl + Up | Move forward one ring (toward the final boss). The first press announces your current ring. |
| Ctrl + Down | Move back one ring |
| Ctrl + Right | Next stop on this ring |
| Ctrl + Left | Previous stop on this ring |

Each ring announces its number ("Ring 3 of 8"), whether it's your current position or already traveled, which path was taken on branching rings, the battle waiting at the end of the ring (including boss battles), and how many stops it has. Each stop announces its name, which path it's on (left, right, or both), whether you've visited it, whether it's available right now, and what it does.

The map cursor is review-only. To actually travel, use the normal arrow keys and Enter on the real map selection.

### Automatic Text Reading

The mod automatically reads text when:
- **Scroll views**: When you focus on a scrollable area (like patch notes), the content is read automatically
- **Dialogs/popups**: New dialogs, tooltips, and text panels are announced when they appear
- **Content changes**: If text content updates while you're focused on it, the new content is announced
- **Chatter**: The speech bubbles units show in battle ("These chains would suit you!") and the merchant's lines in shops are announced as "Name says: ..." (toggle with the AnnounceDialogue config setting)

### Battle Hotkeys
| Key | Action |
|-----|--------|
| J | Read all cards in hand |
| K | Read the selected floor - capacity and units |
| L | Read all floors |
| Shift + L | Read all units with detail - your monsters front-to-back, then enemies |
| G | Read current ember |
| Shift + K | Open floor review - a cursor to walk the floors and their units (see below) |
| N | Toggle combat speed (game's native key) - announces the new speed |
| Page Up/Down | Change the selected floor (game's native keys) - announces the floor |

Each of your turns starts with one line - "Your turn, 3 ember, 5 cards in hand, 2 waves remaining" - and cards in hand announce their position ("2 of 5"). During combat the mod announces attacks, deaths, unit trigger abilities (Summon, Slay, Revenge, Resolve...), healing, status effects, enemies moving, cards added to your deck or hand, and pyre damage. Everything is also kept in the Events buffer.

### Floor Review (Shift+K in battle)

Monster Train 1 has no keyboard way to inspect units in place - hovering with the mouse is the game's only option. The floor review fills that gap: press **Shift+K** during battle to open a virtual cursor over the floors. Opening it announces the current floor and reminds you that Escape closes it.

| Key | Action |
|-----|--------|
| Up/Down arrows | Move between floors (bottom floor, middle, top, then the Pyre room) |
| Left/Right arrows | Step through the units on the floor in their battle positions |
| Enter | Read the focused floor or unit with full details |
| Escape | Close the review |

Each floor first announces an overview: capacity (or pyre health in the Pyre room), frozen state, corruption, and the unit names. Pressing Right then walks the lineup in spatial order - your units from back to front, then the enemies from front to back, so the middle of the lineup is where the frontmost fighters meet. Each unit announces its name and stats, with its position ("Your unit 2 of 3", "Enemy 1 of 2, front") at the end. Pressing Left past the first unit returns to the floor overview.

Full details are always waiting in the buffers: the focused floor or unit feeds the **UI** buffer (and units also the **Creature** buffer), so Ctrl+Up/Down can step through status effects with keyword explanations, abilities, enemy intents, and floor corruption or enchantments piece by piece.

The review cursor is review-only - it never moves the game's real selection, and the view is re-read on every keypress so it stays current while units move or die. It closes automatically when a card starts targeting, when a pile view or menu opens, or when the battle ends. Other battle hotkeys (J, K, L, G, F1...) keep working while it is open.

### Unit Targeting (when playing spells that target units)
| Key | Action |
|-----|--------|
| Left/Right arrows | Select target unit |
| Number keys 1-5 | Select target directly |
| Enter | Confirm target |
| Escape | Cancel spell |

**Targeting Order**: Your first summoned unit is on the far right (front). Go LEFT to target your other units. Go RIGHT to target enemies. Floor announcements list your units front-to-back, then enemies.

### Floor Targeting (when playing a card that requires floor selection)
| Key | Action |
|-----|--------|
| Page Up/Down | Cycle between floors |
| Enter | Confirm and play card on selected floor |
| Escape | Cancel card play |

## Monster Train Native Keyboard Shortcuts

These are the game's built-in default key mappings (from the game's own input setup), which work with or without the accessibility mod. They can be rebound in the game's settings.

### Navigation

| Action | Keyboard Shortcut |
|--------|-------------------|
| Submit / Activate | `E` / `Enter` |
| Cancel / Settings | `Esc` |
| Close panel | `Q` / `Backspace` |
| Move | `Arrow keys` / `WASD` |
| Scroll / Floor up and down | `Page Up` / `Page Down` |
| Jump to start / end | `Home` / `End` |

### Combat & Card Actions

| Action | Keyboard Shortcut |
|--------|-------------------|
| Context action (e.g. end turn) | `F` |
| Game speed toggle | `N` |
| Hold for preview mode | `Left Shift` |
| Select card slot 1-10 | `1` - `0` |
| Show Deck | `Z` |
| Show Draw Pile | `X` |
| Show Discard Pile | `C` |
| Show Exhaust Pile | `V` |
| Show Eaten Pile | `B` |
| Toggle synthesis tooltips | `H` |

### Menu & System

| Action | Keyboard Shortcut |
|--------|-------------------|
| Open Minimap | `M` |
| Access HUD | `Tab` |
| Emotes (multiplayer) | `Y` / `U` / `I` / `O` / `P` |
| Feedback | `F8` |
| Full Screen Toggle | `Alt + Enter` |

None of these game keys is used by the mod (see "No Game Keys Taken" above).

### Note for Controller Users

If you are playing on a PC with a controller, the game has a "Keyboard Mode" that activates when you press a key. If your cursor behaves unexpectedly, check your input settings (Hybrid/Mouse/Controller modes in the options menu).

## Configuration

Press **F9** on any screen to open the accessibility settings: a spoken list of every option - what gets announced (damage, deaths, units entering, status effects, card draws, artifact triggers, chatter), the combat summary, verbosity, braille, and every mod key. Up and Down choose a setting, Left, Right or Enter change it, F2 reads its description, Escape closes. To rebind a key, press Enter on it and then the new key; keys the game itself uses are refused, so a mod key never triggers a game action. Changes are saved immediately. There is no language option: the mod reads the game's own text, so it follows the game's language.

The same settings live in a configuration file, created after first launch at:
`BepInEx/config/com.accessibility.monstertrain.cfg`

You can edit this file to customize:
- Key bindings for all controls
- Verbosity level (Minimal, Normal, Verbose)
- Which events to announce (card draws, damage, status effects)
- SAPI fallback settings
- Braille display options

### Scene Descriptions

The mod describes the moments a sighted player only watches: the game launching, a run starting, arriving at each ring of Hell, a boss's entrance before its battle, a boss turning Relentless, a boss falling, a boss acting from outside the train, and the end of a run. They are never spoken during automated combat: a boss turning Relentless is described when your next turn starts. Turn them off in F9 ("Scene descriptions").

The texts are in `BepInEx/plugins/SceneDescriptions.txt`, one `key: text` per line. Edit it to change them, or delete a line to silence that moment; the mod keeps a built-in copy in case the file is missing.

### Combat Summary

By default combat is narrated as it happens. Turn on **Combat summary instead of live combat narration** (F9) to keep automated combat quiet and hear one summary right before each of your turns: for every floor, the damage your units dealt and took, kills, losses and the health left on every unit that was hit, then enemy moves, pyre damage and new arrivals. Everything is still in the Events buffer either way.

### Verbosity Levels
- **Minimal**: Card names and numbers only
- **Normal**: Standard descriptions with key stats
- **Verbose**: Full details including flavor text

## Battle Navigation

The battle screen uses the game's native navigation. Use hotkeys for information:

- Press **J** to hear all cards in your hand
- Press **K** to hear the selected floor, **L** for all floors
- Press **Shift+L** to hear all units with detail - your monsters front-to-back, then enemies
- Press **Shift+K** to open the floor review and walk the floors and units with the arrow keys (see Floor Review above)
- Press **G** to hear your current ember
- Press **F5**, **F6**, or **F7** for gold, pyre health, or pact shards
- Press **N** to toggle combat speed (announces the new speed)
- Press **Tab** to hear train stats (pyre health, gold, deck size)
- Press **F1** for a browsable help list of all available keys

### Playing Cards
- Navigate to cards using the game's controls
- Press **Enter** to play the selected card
- When a card requires floor placement (like monster cards), floor targeting mode activates:
  - Use **Page Up/Down** to cycle through floors (same as game's native floor navigation)
  - The current floor's units will be announced as you select
  - Press **Enter** to confirm and play the card
  - Press **Escape** to cancel

## Tips for Blind Players

1. **Press F1 for Help**: On any screen, press F1 to open the help list, then browse it with Up/Down
2. **Start Simple**: Begin with the tutorial to learn the game flow
3. **Read Text**: Press F3 to read patch notes, event descriptions, or any screen text
4. **Use Battle Hotkeys**: J, K, L, G provide quick status updates during battle
5. **Check Ember**: Press G to know how much you can play; F5 and F6 cover gold and pyre health
6. **Enemy Intents**: Press Shift+L to hear what enemies plan to do next turn
7. **Re-read Items**: Press F2 to re-read the currently focused menu item - for cards, shop items, and artifacts it reads the complete details in one go. Or step through the same details piece by piece with the UI and Card buffers (Ctrl+arrows)
8. **Combat Speed**: Press N to toggle combat speed - the mod announces the new speed

## Troubleshooting

### No Speech Output
1. Verify your screen reader is running
2. Check that `Tolk.dll` is in the plugins folder
3. Try enabling SAPI fallback in the config file

### Mod Not Loading
1. Verify BepInEx is installed correctly
2. Check `BepInEx/LogOutput.log` for error messages
3. Ensure .NET Framework is installed

### Keys Not Responding
1. Make sure the game window has focus
2. Check for key conflicts in the config file
3. Try the alternate keys (Space instead of Enter)

## Known Limitations

- Some UI animations may cause brief delays in announcements
- Complex card effects may require verbose mode for full details
- Multiplayer modes have limited accessibility support
- Unlike the Monster Train 2 accessibility mod, there are no combat outcome predictions (I / Ctrl+I), no jump-to-hand key, and no in-game mod settings screen - configuration is done through the BepInEx config file

## Support

Report issues or request features at:
[GitHub Issues](https://github.com/yourusername/MonsterTrainAccessibility/issues)

## Credits

- **Tolk Library**: Davy Kager (screen reader integration)
- **BepInEx Team**: Mod loading framework
- **Trainworks**: Community modding toolkit reference
- **Shiny Shoe**: Monster Train developers

## License

This mod is provided free of charge for accessibility purposes.
Tolk library is licensed under LGPLv3.

---

*Making games accessible, one train ride at a time.*
