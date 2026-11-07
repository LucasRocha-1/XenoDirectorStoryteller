using HarmonyLib;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Hooks que alimentam StoryArcManager a partir de eventos reais do jogo.
    /// Restrições e arcos funcionam mesmo sem XenoDirector como storyteller.
    /// </summary>
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Add))]
    public static class Patch_QuestManager_Add
    {
        public static void Postfix(Quest quest)
        {
            if (quest == null) return;
            if (!XenoDirectorMod.Settings.EnableQuestMutation) return;

            // Mutação assíncrona leve
            var access = NarrativeWorldAccess.Instance;
            access.TryMutateQuest(quest);
            PerformanceGuards.ReportActivity(GameSubsystemId.QuestManager);
        }
    }

    /// <summary>
    /// Quando um prisioneiro/refugiado é "resgatado" (recrutado de facção),
    /// inicia arco RescueChain se arcos estão habilitados.
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.DoRecruit),
        new[] { typeof(Pawn), typeof(Pawn), typeof(bool) })]
    public static class Patch_DoRecruit_StartArc
    {
        // Assinatura varia entre versões; usamos overload comum
        public static void Postfix(Pawn recruiter, Pawn recruitee)
        {
            if (!XenoDirectorMod.Settings.EnableNarrativeArcs) return;
            if (recruitee == null) return;

            var gc = Current.Game?.GetComponent<XenoDirectorGameComponent>();
            if (gc?.Arcs == null) return;

            // Só inicia se vinha de facção não-player
            var fac = recruitee.Faction;
            if (fac != null && fac.IsPlayer) return;

            // Evita spam: um arco por pawn
            foreach (var a in gc.Arcs.ActiveArcs)
                if (a.AnchorPawnLoadId == recruitee.GetUniqueLoadID() && !a.Completed)
                    return;

            gc.Arcs.StartRescueChain(recruitee, fac);
        }
    }

}
