using Verse;
using RimWorld;

namespace XenoDirector
{
    /// <summary>
    /// Observador que coleta sinais de reação do jogador.
    /// Projetado para ser chamado por Harmony patches em pontos de baixo custo
    /// (IncidentWorker.TryExecute, Quest finalização, death of colonist, etc.).
    /// </summary>
    public static class PlayerReactionObserver
    {
        public static void OnIncidentExecuted(IncidentDef def, IncidentResult result, Map map)
        {
            if (!XenoDirectorMod.Settings.EnablePlayerReactionTracking) return;
            if (map == null) return;

            var gc = Current.Game?.GetComponent<XenoDirectorGameComponent>();
            if (gc?.PlayerProfile == null) return;

            // Snapshot leve (não faz loops pesados)
            float wealthBefore = result.WealthBefore; // preenchido pelo patch
            float wealthAfter = GetTotalWealth(map);
            int deaths = result.ColonistDeathsDuringEvent;

            var sample = new ReactionSample
            {
                Tick = Find.TickManager.TicksGame,
                IncidentDefName = def?.defName ?? "Unknown",
                WealthDelta = wealthAfter - wealthBefore,
                ColonistDeaths = deaths,
                MoodSwing = result.AverageMoodDelta,
                WasPartOfQuestChain = result.LinkedToQuest,
                EventWasExploited = result.WasExploited
            };

            gc.PlayerProfile.RecordReaction(sample);
        }

        private static float GetTotalWealth(Map map)
        {
            return map.wealthWatcher.WealthTotal;
        }

        /// <summary>
        /// Estrutura leve passada pelos patches para evitar alocações excessivas.
        /// </summary>
        public struct IncidentResult
        {
            public float WealthBefore;
            public int ColonistDeathsDuringEvent;
            public float AverageMoodDelta;
            public bool LinkedToQuest;
            public bool WasExploited;
        }
    }
}
