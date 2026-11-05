using HarmonyLib;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Patches opt-in de performance avançada.
    /// Só têm efeito quando EnableAdvancedPerformance + AdvDynamicEntityThrottling estão ON.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Tick))]
    public static class Patch_Pawn_Tick_Throttle
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance)
        {
            // return false = skip o Tick original deste pawn neste frame
            if (AdvancedPerformanceController.ShouldThrottlePawn(__instance))
                return false;
            return true;
        }
    }
}
