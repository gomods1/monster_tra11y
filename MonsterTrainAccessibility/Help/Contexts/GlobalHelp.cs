namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Fallback help context that is always active.
    /// Provides basic navigation help that applies to all screens.
    /// </summary>
    public class GlobalHelp : IHelpContext
    {
        public string ContextId => "global";
        public string ContextName => "General";
        public int Priority => 0; // Lowest priority - only used as fallback

        public bool IsActive()
        {
            // Always active as fallback
            return true;
        }

        public string GetHelpText()
        {
            return
                "F1: Open help for the current screen. " +
                "F9: Accessibility settings: what gets announced, combat summary, verbosity, braille and every mod key. Up and Down choose, Left, Right or Enter change, Escape closes. " +
                "Arrow keys move the game's focus, Enter selects, Escape goes back or opens the pause menu. " +
                "F2: Re-read the focused item. Cards, shop items and artifacts read their full details. " +
                "F3: Read all text on the screen. " +
                "Tab: Read train stats: pyre health, gold and deck size. " +
                "F5: Gold. F6: Pyre health. F7: Pact shards and threat. " +
                "Ctrl plus Up: Read the next item in the review buffer. The first press reads the newest item, more presses go back in time or deeper into details. " +
                "Ctrl plus Down: Move back toward the newest item. " +
                "Ctrl plus Left and Right: Switch review buffers: UI, Events, Card, Creature, Artifact, Reward and Story, plus Hand, Floors, Units and Resources in battle. " +
                "The Events buffer keeps every combat event, so you can replay anything you missed. " +
                "F4: Cycle verbosity. ";
        }
    }
}
