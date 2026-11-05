using System;
using System.Collections.Generic;

namespace XenoDirector
{
    /// <summary>
    /// Prioridade cooperativa de trabalho.
    /// RimWorld é majoritariamente single-thread no tick principal —
    /// isto NÃO cria OS threads mágicas, mas sim um scheduler que decide
    /// o que roda "agora", o que fica em segundo plano e o que só é
    /// verificado de tempos em tempos (terceiro plano).
    /// </summary>
    public enum WorkTier
    {
        /// <summary>Crítico: combate, pathfinding urgente, UI, input. Roda todo tick relevante.</summary>
        Foreground = 0,
        /// <summary>Importante mas não urgente: economia, needs, social leve. Intervalo curto.</summary>
        Secondary = 1,
        /// <summary>Baixa prioridade: análises, catálogos, limpeza, sistemas "frios". Intervalo longo.</summary>
        Background = 2,
        /// <summary>Quase inativo: só wake-up periódico ou quando o detector marca como "quente" de novo.</summary>
        Hibernating = 3
    }

    public enum GameSubsystemId
    {
        Storyteller,
        Incidents,
        Raids,
        ColonistNeeds,
        ColonistJobs,
        Pathfinding,
        Wildlife,
        WorldPawns,
        QuestManager,
        Trade,
        Research,
        Weather,
        MapGeneration,
        ModContentScan,
        PlayerAnalysis,
        RestrictionEval,
        MiscCleanup
    }

    /// <summary>
    /// Estado de um subsistema monitorado pelo otimizador.
    /// </summary>
    public class SubsystemState
    {
        public GameSubsystemId Id;
        public WorkTier Tier = WorkTier.Secondary;
        public int LastActivityTick;
        public int LastCheckedTick;
        public int CheckIntervalTicks = 250;
        public float Heat;              // 0 = frio, 1 = muito ativo
        public int ConsecutiveIdleCycles;
        public string DebugLabel;

        public bool ShouldCheckNow(int now)
        {
            return now - LastCheckedTick >= CheckIntervalTicks;
        }

        public void MarkChecked(int now) => LastCheckedTick = now;

        public void MarkActivity(int now)
        {
            LastActivityTick = now;
            Heat = Math.Min(1f, Heat + 0.25f);
            ConsecutiveIdleCycles = 0;
            // Sobe de tier se estava hibernando
            if (Tier == WorkTier.Hibernating)
                Tier = WorkTier.Background;
            else if (Tier == WorkTier.Background && Heat > 0.6f)
                Tier = WorkTier.Secondary;
        }

        public void MarkIdle(int now)
        {
            ConsecutiveIdleCycles++;
            Heat = Math.Max(0f, Heat - 0.08f);

            if (ConsecutiveIdleCycles >= 8 && Heat < 0.15f)
            {
                Tier = WorkTier.Hibernating;
                CheckIntervalTicks = 15000; // ~4 min de jogo
            }
            else if (ConsecutiveIdleCycles >= 4 && Heat < 0.35f)
            {
                Tier = WorkTier.Background;
                CheckIntervalTicks = 2500;  // ~40s
            }
            else if (Heat < 0.55f)
            {
                Tier = WorkTier.Secondary;
                CheckIntervalTicks = 600;   // ~10s
            }
            else
            {
                Tier = WorkTier.Foreground;
                CheckIntervalTicks = 60;    // ~1s
            }
        }
    }
}
