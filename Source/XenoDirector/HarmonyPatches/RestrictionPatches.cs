using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Patches de restrições — ativos com qualquer storyteller quando
    /// Settings.EnableRestrictionsAlways (default true).
    /// </summary>
    public static class RestrictionPatchGate
    {
        public static bool RestrictionsLive
        {
            get
            {
                if (XenoDirectorMod.Settings == null) return true;
                return XenoDirectorMod.Settings.EnableRestrictionsAlways
                    || XenoDirectorGameComponent.IsXenoStorytellerActive();
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
    public static class Patch_IncidentWorker_CanFireNow
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!__result) return;
            if (!RestrictionPatchGate.RestrictionsLive) return;

            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm == null) return;

            var def = __instance.def;
            if (def != null && rm.IsIncidentBlocked(def))
            {
                __result = false;
            }
        }
    }

    // Filtra as opções em uma janela curta e restaura a lista mesmo se a geração falhar.
    [HarmonyPatch(typeof(PawnGroupMaker), nameof(PawnGroupMaker.GeneratePawns))]
    public static class Patch_PawnGroupMaker_Instance_GeneratePawns
    {
        public static bool Prefix(
            PawnGroupMaker __instance,
            PawnGroupMakerParms parms,
            ref IEnumerable<Pawn> __result,
            ref List<PawnGenOption> __state)
        {
            __state = null;
            if (!RestrictionPatchGate.RestrictionsLive) return true;
            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm == null || !rm.RestrictsRaids) return true;
            if (parms?.groupKind != PawnGroupKindDefOf.Combat
                && parms?.groupKind != PawnGroupKindDefOf.Settlement)
                return true;

            var originalOptions = __instance.options;
            if (originalOptions == null) return true;

            var allowedOptions = new List<PawnGenOption>();
            foreach (var opt in originalOptions)
            {
                if (opt?.kind != null && rm.IsRaidPawnKindAllowed(opt.kind))
                    allowedOptions.Add(opt);
            }

            if (allowedOptions.Count == 0)
            {
                __result = Enumerable.Empty<Pawn>();
                return false;
            }

            if (allowedOptions.Count == originalOptions.Count) return true;

            __state = originalOptions;
            __instance.options = allowedOptions;
            return true;
        }

        public static void Postfix(ref IEnumerable<Pawn> __result, List<PawnGenOption> __state)
        {
            if (__state != null && __result != null)
                __result = __result.ToList();
        }

        public static System.Exception Finalizer(
            PawnGroupMaker __instance,
            List<PawnGenOption> __state,
            System.Exception __exception)
        {
            if (__state != null)
                __instance.options = __state;
            return __exception;
        }
    }

    // ---------- Bloqueia recompensas de quest que adicionam pawns (Single) ----------
    [HarmonyPatch(typeof(QuestPart_SpawnPawn), "Notify_QuestSignalReceived")]
    public static class Patch_QuestPart_SpawnPawn
    {
        public static bool Prefix()
        {
            if (!RestrictionPatchGate.RestrictionsLive) return true;
            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm != null && rm.BlocksQuestPawnRewards)
                return false;
            return true;
        }
    }

    // Rede de segurança: IncidentWorker_WandererJoin e similares
    [HarmonyPatch]
    public static class Patch_WandererJoin_CanFireNow
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            // IncidentWorker_WandererJoin existe no core
            var type = AccessTools.TypeByName("RimWorld.IncidentWorker_WandererJoin");
            if (type == null) return null;
            return AccessTools.Method(type, "CanFireNowSub");
        }

        public static void Postfix(ref bool __result)
        {
            if (!__result) return;
            if (!RestrictionPatchGate.RestrictionsLive) return;
            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm != null && rm.BlocksRecruitment)
                __result = false;
        }
    }
}
