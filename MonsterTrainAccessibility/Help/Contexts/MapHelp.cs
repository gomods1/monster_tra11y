namespace MonsterTrainAccessibility.Help.Contexts
{
    /// <summary>
    /// Help context for the map/route selection screen
    /// </summary>
    public class MapHelp : IHelpContext
    {
        public string ContextId => "map";
        public string ContextName => "Map";
        public int Priority => 60;

        public bool IsActive()
        {
            return ScreenStateTracker.CurrentScreen == GameScreen.Map;
        }

        public string GetHelpText()
        {
            return
                "Map screen: each ring has one or two paths with stops such as merchants, events and upgrades, and ends with a battle. " +
                "When a ring has two paths, Left and Right arrows choose between them and each path lists its stops. Enter picks the path. " +
                "After choosing, the arrow keys move between map stops. The game moves spatially, so a press can jump to the next ring's battle. Every stop says its name, path, ring and whether you can visit it now. " +
                "Enter: Visit the focused stop or start the battle. " +
                "Virtual map cursor, for reading the whole map without moving anything: Ctrl plus Up and Down move between rings, Ctrl plus Left and Right step through a ring's stops. " +
                "Each ring announces its number, whether you are there, the battle at its end and how many stops it has. " +
                "F2: Re-read the focused stop. " +
                "Tab: Train stats. F5: Gold. F6: Pyre health. ";
        }
    }
}
