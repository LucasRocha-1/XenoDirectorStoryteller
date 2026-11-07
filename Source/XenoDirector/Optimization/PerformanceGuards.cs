using System;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Utilitários de otimização focados em saves longos e muitas mods.
    /// </summary>
    public static class PerformanceGuards
    {
        public static bool ShouldRun(ref int lastTick, int minIntervalTicks)
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastTick < minIntervalTicks) return false;
            lastTick = now;
            return true;
        }

        public static void BoundList<T>(System.Collections.Generic.List<T> list, int maxSize)
        {
            if (list == null || list.Count <= maxSize) return;
            int remove = list.Count - maxSize;
            list.RemoveRange(0, remove);
        }

        public static bool IsHeavyLoad
        {
            get
            {
                int modCount = LoadedModManager.RunningModsListForReading.Count;
                int days = GenDate.DaysPassed;
                return modCount > 80 || days > 500;
            }
        }

        /// <summary>
        /// Consulta o OptimizationDirector (se existir) antes de rodar lógica pesada de um subsistema.
        /// Uso: if (!PerformanceGuards.AllowHeavy(GameSubsystemId.Wildlife)) return;
        /// </summary>
        public static bool AllowHeavy(GameSubsystemId id)
        {
            var opt = XenoDirectorGameComponent.GetOpt();
            if (opt == null || !opt.Enabled) return true; // sem diretor = comportamento vanilla
            return opt.ShouldRunHeavy(id);
        }

        public static void ReportActivity(GameSubsystemId id)
        {
            XenoDirectorGameComponent.GetOpt()?.NotifyActivity(id);
        }
    }
}
