using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Diretor de otimização — sempre ativo enquanto o mod estiver carregado,
    /// independentemente de XenoDirector ser o storyteller selecionado.
    ///
    /// Usa múltiplos "cérebros de otimização" para:
    /// 1. Observar calor de subsistemas (combate, colonos, wildlife, world pawns…)
    /// 2. Empurrar sistemas frios para Background / Hibernating
    /// 3. Time-slicing de trabalho deferred sob orçamento de ms
    /// 4. (Opcional) computação pura em ThreadPool via SafeBackgroundQueue
    ///
    /// IMPORTANTE: isto NÃO multiplica threads do loop principal do RimWorld.
    /// O ganho vem de NÃO fazer trabalho inútil todo tick.
    /// </summary>
    public class OptimizationDirector
    {
        public CooperativeScheduler Scheduler { get; } = new CooperativeScheduler();
        private readonly List<IOptimizationBrain> _brains = new List<IOptimizationBrain>();
        private bool _initialized;
        private int _lastFullPulse;

        public bool Enabled = true;

        public void EnsureInitialized()
        {
            if (_initialized) return;

            _brains.Add(new CombatHeatBrain());
            _brains.Add(new ColonistLoadBrain());
            _brains.Add(new WildlifeIdleBrain());
            _brains.Add(new WorldPawnPressureBrain());
            _brains.Add(new ModCatalogBrain());
            _brains.Add(new PlayerAnalysisOptBrain());
            _brains.Add(new ColdSystemDetectorBrain());

            _initialized = true;
            Log.Message($"[XenoDirector.Opt] OptimizationDirector online with {_brains.Count} opt-brains (storyteller-independent)");
        }

        /// <summary>
        /// Chamado todo tick pelo GameComponent (já throttled).
        /// </summary>
        public void Tick(XenoDirectorGameComponent gc)
        {
            if (!enabled) return;
            if (XenoDirectorMod.Settings != null && !XenoDirectorMod.Settings.EnableOptimizationLayer)
                return;

            EnsureInitialized();

            int tick = Find.TickManager.TicksGame;

            // Context leve
            var ctx = BuildContext(gc, tick);

            // Cada cérebro observa (só os que o scheduler autorizar internamente)
            foreach (var brain in _brains)
            {
                try { brain.ObserveAndAdvise(ctx); }
                catch (System.Exception e)
                {
                    Log.Warning($"[XenoDirector.Opt] Brain {brain.Id} failed: {e.Message}");
                }
            }

            // Processa fila deferred sob orçamento
            Scheduler.Pump();

            // Aplica completions de background threads
            SafeBackgroundQueue.PumpCompletions();

            // Pulse mais raro: log de diagnóstico
            if (tick - _lastFullPulse > 15000)
            {
                _lastFullPulse = tick;
                if (Prefs.DevMode)
                    LogDiagnostics();
            }
        }

        private OptimizationContext BuildContext(XenoDirectorGameComponent gc, int tick)
        {
            int colonists = 0;
            bool inCombat = false;

            if (Find.CurrentMap != null)
            {
                var pawns = Find.CurrentMap.mapPawns;
                colonists = pawns?.FreeColonistsCount ?? 0;
                // Heurística barata de combate: algum colono em mental state de fight ou hostiles no mapa
                inCombat = (pawns?.AllPawnsSpawned?.Any(p =>
                    p.HostileTo(Faction.OfPlayer) && p.Spawned && !p.Downed) ?? false);
            }

            return new OptimizationContext
            {
                Scheduler = Scheduler,
                GameState = gc,
                Tick = tick,
                AggressiveMode = XenoDirectorMod.Settings?.AggressiveOptimization ?? true,
                ColonistCount = colonists,
                MapCount = Find.Maps?.Count ?? 0,
                ModCount = LoadedModManager.RunningModsListForReading.Count,
                InCombat = inCombat
            };
        }

        private void LogDiagnostics()
        {
            var hibernating = Scheduler.AllSystems.Count(s => s.Tier == WorkTier.Hibernating);
            var background = Scheduler.AllSystems.Count(s => s.Tier == WorkTier.Background);
            var secondary = Scheduler.AllSystems.Count(s => s.Tier == WorkTier.Secondary);
            var foreground = Scheduler.AllSystems.Count(s => s.Tier == WorkTier.Foreground);

            Log.Message(
                $"[XenoDirector.Opt] Tiers F/S/B/H = {foreground}/{secondary}/{background}/{hibernating} | " +
                $"deferred={Scheduler.DeferredCount} bgJobs={SafeBackgroundQueue.PendingJobs}");
        }

        /// <summary>
        /// API pública para outros sistemas (storyteller, patches) reportarem atividade.
        /// </summary>
        public void NotifyActivity(GameSubsystemId id) => Scheduler.ReportActivity(id);

        public bool ShouldRunHeavy(GameSubsystemId id) => Scheduler.ShouldRun(id);
    }
}
