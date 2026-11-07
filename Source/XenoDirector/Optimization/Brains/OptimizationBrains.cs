using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Conjunto de micro-cérebros de otimização.
    /// Rodam sempre (mod ativo), independente de qual storyteller está selecionado.
    /// </summary>

    public class CombatHeatBrain : IOptimizationBrain
    {
        public string Id => "Opt_CombatHeat";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.Raids;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            if (ctx.InCombat)
            {
                ctx.Scheduler.ReportActivity(GameSubsystemId.Raids);
                ctx.Scheduler.ReportActivity(GameSubsystemId.ColonistJobs);
                ctx.Scheduler.ReportActivity(GameSubsystemId.Pathfinding);
                // Em combate, não hiberna wildlife/worldpawns agressivamente demais
                // (mas ainda não força foreground)
            }
        }
    }

    public class ColonistLoadBrain : IOptimizationBrain
    {
        public string Id => "Opt_ColonistLoad";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.ColonistNeeds;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            // Muitos colonos → needs/jobs ficam quentes
            if (ctx.ColonistCount > 20)
            {
                ctx.Scheduler.ReportActivity(GameSubsystemId.ColonistNeeds);
                ctx.Scheduler.ReportActivity(GameSubsystemId.ColonistJobs);
            }
            else if (ctx.ColonistCount < 5 && !ctx.InCombat)
            {
                // Colônia pequena e em paz: pode relaxar um pouco (o scheduler decay faz o resto)
            }
        }
    }

    public class WildlifeIdleBrain : IOptimizationBrain
    {
        public string Id => "Opt_WildlifeIdle";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.Wildlife;

        private int _lastWildCount = -1;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            if (!ctx.Scheduler.ShouldRun(GameSubsystemId.Wildlife)) return;

            int wild = 0;
            if (Find.CurrentMap != null)
            {
                // Contagem leve — só se o scheduler autorizou
                wild = Find.CurrentMap.mapPawns?.AllPawnsSpawned?.Count(p => p.Faction == null && p.RaceProps.Animal) ?? 0;
            }

            if (wild != _lastWildCount && wild > 0)
            {
                ctx.Scheduler.ReportActivity(GameSubsystemId.Wildlife);
                _lastWildCount = wild;
            }
            // Se wild está estável e baixo, o DecayIdleSystems vai empurrar para Background/Hibernating
        }
    }

    public class WorldPawnPressureBrain : IOptimizationBrain
    {
        public string Id => "Opt_WorldPawns";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.WorldPawns;

        private bool _pressureReported;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            if (!ctx.Scheduler.ShouldRun(GameSubsystemId.WorldPawns)) return;

            int count = Find.WorldPawns?.AllPawnsAlive?.Count() ?? 0;
            if (count > 200)
            {
                ctx.Scheduler.ReportActivity(GameSubsystemId.WorldPawns);
                if (!_pressureReported && Prefs.DevMode)
                {
                    Log.Message($"[XenoDirector.Opt] World pawn pressure: {count} living world pawns.");
                    _pressureReported = true;
                }
            }
            else
            {
                _pressureReported = false;
            }
        }
    }

    public class ModCatalogBrain : IOptimizationBrain
    {
        public string Id => "Opt_ModCatalog";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.ModContentScan;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            if (!ctx.Scheduler.ShouldRun(GameSubsystemId.ModContentScan)) return;

            // Rebuild do catálogo é caro → main-thread enqueue OU background se só leitura
            ctx.Scheduler.Enqueue(GameSubsystemId.ModContentScan, () =>
            {
                ModIncidentCatalog.Instance.EnsureBuilt(force: false);
            }, "ModIncidentCatalog ensure");
        }
    }

    public class PlayerAnalysisOptBrain : IOptimizationBrain
    {
        public string Id => "Opt_PlayerAnalysis";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.PlayerAnalysis;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            if (!ctx.Scheduler.ShouldRun(GameSubsystemId.PlayerAnalysis)) return;
            if (ctx.GameState?.PlayerProfile == null) return;

            // Prune de histórico é barato e pode ir deferred
            ctx.Scheduler.Enqueue(GameSubsystemId.PlayerAnalysis, () =>
            {
                ctx.GameState.PlayerProfile.PruneOldData(
                    XenoDirectorMod.Settings?.HistoryRetentionDays ?? 60);
            }, "PlayerProfile prune");
        }
    }

    public class ColdSystemDetectorBrain : IOptimizationBrain
    {
        public string Id => "Opt_ColdDetector";
        public GameSubsystemId PrimarySubsystem => GameSubsystemId.MiscCleanup;

        public void ObserveAndAdvise(OptimizationContext ctx)
        {
            // Este cérebro só força o decay periódico
            if (!ctx.Scheduler.ShouldRun(GameSubsystemId.MiscCleanup)) return;
            ctx.Scheduler.DecayIdleSystems();
        }
    }
}
