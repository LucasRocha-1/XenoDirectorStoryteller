using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// StorytellerComp que conecta o Director + micro-cérebros ao pipeline oficial de incidentes.
    /// Aplica filtros de RestrictionManager antes de disparar qualquer evento.
    /// </summary>
    public class StorytellerCompProperties_XenoDirector : StorytellerCompProperties
    {
        public int baseMicroBrainCount = 2;
        public int maxMicroBrainCount = 8;
        public float menaceGaugeDecayPerDay = 0.15f;
        public int playerReactionWindowTicks = 15000;

        public StorytellerCompProperties_XenoDirector()
        {
            compClass = typeof(StorytellerComp_XenoDirector);
        }
    }

    public class StorytellerComp_XenoDirector : StorytellerComp
    {
        private DirectorBrain _director;
        private List<IMicroBrain> _permanentMicros = new List<IMicroBrain>();
        private bool _initialized;

        protected StorytellerCompProperties_XenoDirector Props => (StorytellerCompProperties_XenoDirector)props;

        public override void Initialize()
        {
            base.Initialize();
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            var gc = Current.Game?.GetComponent<XenoDirectorGameComponent>();
            if (gc == null) return;

            _director = BrainFactory.CreateDirector();
            int count = XenoDirectorMod.Settings?.PermanentMicroBrainCount ?? Props.baseMicroBrainCount;
            count = UnityEngine.Mathf.Clamp(count, 1, Props.maxMicroBrainCount);

            _permanentMicros = BrainFactory.CreatePermanentSet(count);
            foreach (var m in _permanentMicros)
                _director.RegisterMicroBrain(m);

            foreach (var t in gc.TemporaryBrains)
                _director.RegisterMicroBrain(t);

            gc.Restrictions?.EnsureRegistry();
            _initialized = true;
            Log.Message($"[XenoDirector] Initialized with {_permanentMicros.Count} permanent micro-brains + Director");
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            EnsureInitialized();
            if (_director == null) yield break;

            var map = target as Map;
            var gc = Current.Game.GetComponent<XenoDirectorGameComponent>();
            if (gc == null) yield break;

            var rm = gc.Restrictions;
            int tick = Find.TickManager.TicksGame;

            _director.Tick(tick, gc);
            foreach (var m in _director.GetActiveMicroBrains())
                m.Tick(tick, gc);

            var context = new IncidentContext
            {
                Map = map,
                CurrentMenace = gc.MenaceGauge,
                PlayerProfile = gc.PlayerProfile,
                Restrictions = rm,
                ColonyWealth = map != null ? map.wealthWatcher.WealthTotal : 0f,
                DaysPassed = GenDate.DaysPassed,
                AggressiveOptimization = XenoDirectorMod.Settings.AggressiveOptimization
            };

            var candidates = new List<IncidentCandidate>();

            var dirProp = _director.ProposeIncident(context);
            if (dirProp != null) candidates.Add(dirProp);

            foreach (var micro in _director.GetActiveMicroBrains())
            {
                var prop = micro.ProposeIncident(context);
                if (prop != null) candidates.Add(prop);
            }

            if (candidates.Count == 0) yield break;

            foreach (var best in candidates.OrderByDescending(c => c.Priority))
            {
                if (best.Def == null && XenoDirectorMod.Settings.EnableCrossModCoordination)
                {
                    best.Def = ModIncidentCatalog.Instance.PickCoordinatedIncident(null);
                }

                if (best.Def == null)
                    best.Def = DefDatabase<IncidentDef>.GetRandom();

                if (best.Def == null) continue;

                // FILTRO CENTRAL DE RESTRIÇÕES
                if (rm != null && rm.IsIncidentBlocked(best.Def))
                    continue;

                if (rm != null && rm.HasForcedIncidents && !rm.IsIncidentForced(best.Def)
                    && best.SourceBrainId != "Director_Main" && best.Priority < 0.85f)
                    continue;

                var parms = StorytellerUtility.DefaultParmsNow(best.Def.category, target);
                if (best.SuggestedPoints > 0)
                    parms.points = best.SuggestedPoints;

                yield return new FiringIncident(best.Def, this, parms);
                yield break;
            }
        }
    }
}
