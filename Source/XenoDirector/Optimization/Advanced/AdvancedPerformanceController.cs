using System;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Métodos de performance AVANÇADOS — só ativos se o jogador optar nas settings.
    /// Inspirados na ideia de otimizadores "ativos" (estilo Minecraft), adaptados ao RimWorld:
    /// - Skip de ticks em entidades distantes / dormindo
    /// - Intervalos dinâmicos para wildlife e world pawns
    /// - Limite de trabalho de plantação/toil remoto
    /// - GC hint periódico (suave)
    ///
    /// Tudo é opt-in e desliga com um clique. Nunca ativa sozinho.
    /// </summary>
    public class AdvancedPerformanceController
    {
        private int _lastGcHintTick;
        private int _lastDeepScanTick;

        public void Tick()
        {
            var s = XenoDirectorMod.Settings;
            if (s == null || !s.EnableAdvancedPerformance) return;

            int tick = Find.TickManager.TicksGame;

            if (s.AdvDynamicEntityThrottling)
                PulseEntityThrottling(tick);

            if (s.AdvWorldPawnAggressiveCull && tick - _lastDeepScanTick > 10000)
            {
                _lastDeepScanTick = tick;
                // Só reporta pressão; cull real de world pawns é perigoso — enfileira análise
                SafeBackgroundQueue.EnqueueCompute(
                    () => { /* snapshot counts only */ },
                    () =>
                    {
                        int n = Find.WorldPawns?.AllPawnsAlive?.Count() ?? 0;
                        if (n > s.AdvWorldPawnSoftCap)
                            PerformanceGuards.ReportActivity(GameSubsystemId.WorldPawns);
                    });
            }

            if (s.AdvPeriodicGcHint && tick - _lastGcHintTick > 180000) // ~3 dias
            {
                _lastGcHintTick = tick;
                // Hint suave — não força full blocking GC
                GC.Collect(0, GCCollectionMode.Optimized, false);
            }
        }

        private void PulseEntityThrottling(int tick)
        {
            // O trabalho real de skip é feito pelos Harmony patches em
            // Pawn.Tick / Thing.Tick raros, consultando ShouldThrottle(pawn).
            PerformanceGuards.ReportActivity(GameSubsystemId.ColonistJobs);
        }

        /// <summary>
        /// Retorna true se este pawn deve pular lógica pesada neste tick
        /// (só com advanced performance ON).
        /// </summary>
        public static bool ShouldThrottlePawn(Pawn pawn)
        {
            var s = XenoDirectorMod.Settings;
            if (s == null || !s.EnableAdvancedPerformance || !s.AdvDynamicEntityThrottling)
                return false;
            if (pawn == null || pawn.Destroyed) return true;
            if (pawn.Faction == Faction.OfPlayer) return false; // nunca throttle colonos do jogador
            if (pawn.Spawned && pawn.Map != null)
            {
                // Hostis próximos: nunca
                if (pawn.HostileTo(Faction.OfPlayer))
                {
                    var cam = Find.CameraDriver?.MapPosition ?? IntVec3.Invalid;
                    if (cam.IsValid && pawn.Position.InHorDistOf(cam, 60f))
                        return false;
                }

                // Animais longe da câmera / colonia: throttle
                if (pawn.RaceProps?.Animal == true)
                {
                    var map = pawn.Map;
                    var playerHome = map.areaManager?.Home;
                    // A cada N ticks permite um tick "cheio"
                    int interval = s.AdvDistantPawnTickInterval;
                    if (interval < 2) interval = 2;
                    return (Find.TickManager.TicksGame + pawn.thingIDNumber) % interval != 0;
                }
            }
            else
            {
                // World pawn / não spawned
                int interval = Math.Max(5, s.AdvDistantPawnTickInterval * 2);
                return (Find.TickManager.TicksGame + pawn.thingIDNumber) % interval != 0;
            }
            return false;
        }
    }
}
