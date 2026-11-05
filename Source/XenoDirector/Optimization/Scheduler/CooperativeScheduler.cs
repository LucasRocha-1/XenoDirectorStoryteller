using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Scheduler cooperativo de trabalho.
    ///
    /// Objetivo: distribuir o "orçamento de CPU" do tick entre subsistemas,
    /// empurrando partes frias para Background/Hibernating e garantindo que
    /// o Foreground (combate, player-facing) nunca sofra.
    ///
    /// NÃO é multi-threading real do Unity main thread. É priorização +
    /// time-slicing. Trabalho puro de análise (sem touch em Game state)
    /// pode ser enfileirado para ThreadPool com cuidado (ver SafeBackgroundQueue).
    /// </summary>
    public class CooperativeScheduler
    {
        private readonly Dictionary<GameSubsystemId, SubsystemState> _systems =
            new Dictionary<GameSubsystemId, SubsystemState>();

        private readonly Queue<ScheduledWorkItem> _deferred = new Queue<ScheduledWorkItem>();
        private const int MaxDeferredPerTick = 3;
        private const long SoftBudgetMs = 2; // orçamento soft por chamada de pump

        public CooperativeScheduler()
        {
            RegisterDefaults();
        }

        private void RegisterDefaults()
        {
            void Add(GameSubsystemId id, WorkTier tier, int interval, string label)
            {
                _systems[id] = new SubsystemState
                {
                    Id = id,
                    Tier = tier,
                    CheckIntervalTicks = interval,
                    DebugLabel = label,
                    LastActivityTick = 0,
                    LastCheckedTick = -999999
                };
            }

            Add(GameSubsystemId.ColonistNeeds, WorkTier.Foreground, 60, "Colonist Needs");
            Add(GameSubsystemId.ColonistJobs, WorkTier.Foreground, 60, "Colonist Jobs");
            Add(GameSubsystemId.Pathfinding, WorkTier.Foreground, 30, "Pathfinding");
            Add(GameSubsystemId.Raids, WorkTier.Secondary, 250, "Raids / Threats");
            Add(GameSubsystemId.Incidents, WorkTier.Secondary, 600, "Incidents");
            Add(GameSubsystemId.Storyteller, WorkTier.Secondary, 600, "Storyteller");
            Add(GameSubsystemId.Wildlife, WorkTier.Background, 1200, "Wildlife");
            Add(GameSubsystemId.WorldPawns, WorkTier.Background, 2500, "World Pawns");
            Add(GameSubsystemId.QuestManager, WorkTier.Secondary, 900, "Quests");
            Add(GameSubsystemId.Trade, WorkTier.Background, 1800, "Trade");
            Add(GameSubsystemId.Research, WorkTier.Background, 1800, "Research");
            Add(GameSubsystemId.Weather, WorkTier.Secondary, 600, "Weather");
            Add(GameSubsystemId.ModContentScan, WorkTier.Hibernating, 15000, "Mod Content Scan");
            Add(GameSubsystemId.PlayerAnalysis, WorkTier.Background, 2500, "Player Analysis");
            Add(GameSubsystemId.RestrictionEval, WorkTier.Secondary, 600, "Restriction Eval");
            Add(GameSubsystemId.MiscCleanup, WorkTier.Hibernating, 30000, "Misc Cleanup");
        }

        public SubsystemState Get(GameSubsystemId id)
        {
            if (_systems.TryGetValue(id, out var s)) return s;
            var created = new SubsystemState { Id = id, DebugLabel = id.ToString() };
            _systems[id] = created;
            return created;
        }

        public IEnumerable<SubsystemState> AllSystems => _systems.Values;

        /// <summary>
        /// Reporta atividade real observada (ex.: raid começou, colonist em combate).
        /// Sobe o Heat e pode promover o tier.
        /// </summary>
        public void ReportActivity(GameSubsystemId id)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            Get(id).MarkActivity(now);
        }

        /// <summary>
        /// Decide se um subsistema deve rodar lógica pesada neste momento.
        /// </summary>
        public bool ShouldRun(GameSubsystemId id)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            var s = Get(id);
            if (!s.ShouldCheckNow(now)) return false;
            s.MarkChecked(now);
            return true;
        }

        /// <summary>
        /// Enfileira trabalho para ser executado sob orçamento (time-slice).
        /// </summary>
        public void Enqueue(GameSubsystemId owner, Action work, string debugName = null)
        {
            if (work == null) return;
            _deferred.Enqueue(new ScheduledWorkItem
            {
                Owner = owner,
                Work = work,
                DebugName = debugName ?? owner.ToString()
            });
        }

        /// <summary>
        /// Processa até MaxDeferredPerTick itens ou até estourar SoftBudgetMs.
        /// Chamado pelo OptimizationDirector a cada tick throttled.
        /// </summary>
        public void Pump()
        {
            if (_deferred.Count == 0) return;

            var sw = Stopwatch.StartNew();
            int done = 0;

            while (_deferred.Count > 0 && done < MaxDeferredPerTick && sw.ElapsedMilliseconds < SoftBudgetMs)
            {
                var item = _deferred.Dequeue();
                try
                {
                    item.Work?.Invoke();
                    ReportActivity(item.Owner);
                }
                catch (Exception e)
                {
                    Log.Warning($"[XenoDirector.Opt] Deferred work failed ({item.DebugName}): {e.Message}");
                }
                done++;
            }
        }

        /// <summary>
        /// Atualiza Heat/Tier de sistemas que não reportaram atividade.
        /// </summary>
        public void DecayIdleSystems()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (var s in _systems.Values)
            {
                // Se não teve atividade recente, marca idle
                int idleWindow = s.CheckIntervalTicks * 2;
                if (now - s.LastActivityTick > idleWindow)
                    s.MarkIdle(now);
            }
        }

        public int DeferredCount => _deferred.Count;

        private struct ScheduledWorkItem
        {
            public GameSubsystemId Owner;
            public Action Work;
            public string DebugName;
        }
    }
}
