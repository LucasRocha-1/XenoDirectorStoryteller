using HarmonyLib;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Patches mínimos para observar resultados de incidentes e alimentar o PlayerReactionProfile.
    /// Mantidos leves de propósito.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_IncidentWorker_TryExecute
    {
        // Prefixo só para capturar wealth antes (exemplo)
        public static void Prefix(IncidentParms parms, out float __state)
        {
            __state = 0f;
            if (parms?.target is Map map)
            {
                __state = map.wealthWatcher?.WealthTotal ?? 0f;
            }
        }

        public static void Postfix(IncidentDef ___def, IncidentParms parms, bool __result, float __state)
        {
            if (!__result || !XenoDirectorMod.Settings.EnablePlayerReactionTracking) return;

            var result = new PlayerReactionObserver.IncidentResult
            {
                WealthBefore = __state,
                ColonistDeathsDuringEvent = 0, // preencher com observação real de deaths se necessário
                AverageMoodDelta = 0f,
                LinkedToQuest = false,
                WasExploited = false
            };

            PlayerReactionObserver.OnIncidentExecuted(___def, result, parms?.target as Map);
        }
    }
}
