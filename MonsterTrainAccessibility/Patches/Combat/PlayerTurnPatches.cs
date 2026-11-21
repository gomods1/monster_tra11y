using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Announce the start of the player's turn at the moment the game hands
    /// control to the player: CombatManager.monsterTurnPlayerHasControlSignal
    /// fires (true) after the turn's draw, start-of-turn relics and status
    /// triggers, so ember and hand are final. Entering the MonsterTurn phase
    /// (CombatPhaseChangePatch) is too early - the draw hasn't happened yet.
    ///
    /// The earlier StartPlayerTurn/EndPlayerTurn targets never existed on
    /// CombatManager, so there was no turn-start cue at all.
    ///
    /// Subscribes from a postfix on CombatManager.StartCombat (once per
    /// CombatManager instance).
    /// </summary>
    public static class PlayerTurnStartPatch
    {
        private static CombatManager _subscribed;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(typeof(CombatManager), "StartCombat");
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(PlayerTurnStartPatch).GetMethod(nameof(Postfix))));
                    MonsterTrainAccessibility.LogInfo("Patched CombatManager.StartCombat for player turn control");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StartCombat: {ex.Message}");
            }
        }

        public static void Postfix(CombatManager __instance)
        {
            try
            {
                if (__instance == null || ReferenceEquals(__instance, _subscribed))
                    return;
                _subscribed?.monsterTurnPlayerHasControlSignal.RemoveListener(OnPlayerControl);
                __instance.monsterTurnPlayerHasControlSignal.AddListener(OnPlayerControl);
                _subscribed = __instance;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error subscribing to player control signal: {ex.Message}");
            }
        }

        private static void OnPlayerControl(bool hasControl)
        {
            try
            {
                if (hasControl)
                    MonsterTrainAccessibility.BattleHandler?.OnPlayerTurnStarted();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in player turn start: {ex.Message}");
            }
        }
    }
}
