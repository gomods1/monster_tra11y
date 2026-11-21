namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for battle/combat (when not targeting)
    /// </summary>
    public class BattleHelp : IHelpContext
    {
        public string ContextId => "battle";
        public string ContextName => "Battle";
        public int Priority => 90;

        public bool IsActive()
        {
            // Active during battle, but not when floor targeting is active
            if (ScreenStateTracker.CurrentScreen != GameScreen.Battle)
                return false;

            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return false;

            // Check if floor targeting is NOT active
            var targeting = MonsterTrainAccessibility.FloorTargeting;
            return targeting == null || !targeting.IsTargeting;
        }

        public string GetHelpText()
        {
            return
                "Battle: enemies enter on the bottom floor and climb one floor after each round of combat. The pyre sits above the top floor. If the pyre runs out of health the run ends. " +
                "Each of your turns starts with one line: your ember, how many cards are in your hand and how many enemy waves remain. " +
                "Left and Right arrows: Move through your hand. Each card says its name, cost, stats and position, for example 2 of 5. " +
                "Number keys 1 to 9 and 0: Jump to a card in your hand. " +
                "Enter or E: Play the focused card. Units and floor spells then ask for a floor, targeted spells ask for a unit. " +
                "F2: Full details of the focused card. Ctrl plus Up then steps through them one piece at a time, including keyword explanations. " +
                "F: End your turn. Combat is then announced as it happens: attacks, deaths, trigger abilities such as Slay or Revenge, healing, enemies moving and pyre damage. " +
                "J: Read your hand. " +
                "K: Read the selected floor. L: Read all floors. " +
                "Shift plus L: Read all units with full detail. " +
                "G: Read ember. " +
                "Page Up and Page Down: Change the selected floor and read it. " +
                "Shift plus K: Open floor review, a cursor that walks the floors and their units without changing anything in the game. " +
                "N: Change game speed. " +
                "Game piles: Z deck, X draw pile, B eaten pile. Escape or Q closes a pile view. " +
                "Ctrl plus Left or Right to the Events buffer, then Ctrl plus Up, replays the combat log. ";
        }
    }
}
