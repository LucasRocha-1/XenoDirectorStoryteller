using System;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Utilitários de otimização focados em:
    /// - Saves que duram dezenas/centenas de anos
    /// - Listas de 100+ mods
    /// - Evitar GC spikes e trabalho por tick desnecessário
    ///
    /// Técnicas: throttling, object pooling simples, bounded collections, lazy evaluation.
    /// </summary>
    public static class PerformanceGuards
    {
        /// <summary>
        /// Executa ação apenas se o intervalo mínimo de ticks foi respeitado.
        /// </summary>
        public static bool ShouldRun(ref int lastTick, int minIntervalTicks)
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastTick < minIntervalTicks) return false;
            lastTick = now;
            return true;
        }

        /// <summary>
        /// Limita o tamanho de qualquer lista serializada / histórica.
        /// </summary>
        public static void BoundList<T>(System.Collections.Generic.List<T> list, int maxSize)
        {
            if (list == null || list.Count <= maxSize) return;
            int remove = list.Count - maxSize;
            list.RemoveRange(0, remove);
        }

        /// <summary>
        /// Flag global de modo "heavy load" (muitos mods ou save muito antigo).
        /// Pode ser usado para reduzir frequência de análises mais caras.
        /// </summary>
        public static bool IsHeavyLoad
        {
            get
            {
                // Heurística simples
                int modCount = LoadedModManager.RunningModsListForReading.Count;
                int days = GenDate.DaysPassed;
                return modCount > 80 || days > 500;
            }
        }
    }
}
