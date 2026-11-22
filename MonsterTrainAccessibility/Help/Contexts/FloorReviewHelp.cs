namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context while a battle floor (not the hand) is focused
    /// </summary>
    public class FloorReviewHelp : IHelpContext
    {
        public string ContextId => "floor_review";
        public string ContextName => "Floors";
        public int Priority => 96; // Above battle (90); targeting returns focus to the hand

        public bool IsActive()
        {
            var review = MonsterTrainAccessibility.FloorReview;
            return review != null && review.IsActive;
        }

        public string GetHelpText()
        {
            return "Floors: Up arrow climbs from your hand to the bottom floor, the middle floor, the top floor and the pyre room; Down arrow comes back. " +
                   "Moving onto a floor also selects it in the game, like Page Up and Page Down, and reads it. " +
                   "Left and Right arrows: Step through what is on the floor in position order, " +
                   "your units back to front, then enemies front to back. In the pyre room the pyre comes first. " +
                   "At the ends the same unit is read again. " +
                   "Enter or F2: Read the focused floor or unit with full details. " +
                   "Ctrl plus Up/Down: Review the focused item's details in the UI buffer, " +
                   "including keyword explanations; units and the pyre also fill the Creature buffer. " +
                   "Down arrow from the bottom floor: Back to your hand, where Left and Right move through the cards again. " +
                   "Number keys still pick a card, and F ends your turn. ";
        }
    }
}
