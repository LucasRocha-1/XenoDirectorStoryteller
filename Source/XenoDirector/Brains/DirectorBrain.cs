using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// MACRO-BRAIN (equivalente ao Director de Alien: Isolation).
    /// - Sempre conhece o estado global (menace, perfil do jogador, restrições, mods carregados).
    /// - Nunca "cheata" diretamente: apenas aponta / prioriza / filtra.
    /// - Controla o pacing e reage às restrições ativas (Single, Zombie Apocalypse, Nature's Wrath…).
    /// </summary>
    public class DirectorBrain : IBrain
    {
        public string Id { get; } = "Director_Main";
        public string TypeName => "Director";
        public bool IsTemporary => false;
        public float CurrentInternalTension { get; private set; }

        private readonly List<IMicroBrain> _microBrains = new List<IMicroBrain>();
        private int _lastCoordinationTick;
        private int _disasterSequenceIndex;

        public void RegisterMicroBrain(IMicroBrain brain)
        {
            if (!_microBrains.Contains(brain))
                _microBrains.Add(brain);
        }

        public void UnregisterMicroBrain(IMicroBrain brain)
        {
            _microBrains.Remove(brain);
        }

        public void Tick(int currentTick, XenoDirectorGameComponent state)
        {
            float bias = state.Restrictions?.MenaceBias ?? 0f;
            CurrentInternalTension = UnityEngine.Mathf.Clamp01(state.MenaceGauge + bias * 0.3f);

            if (currentTick - _lastCoordinationTick > 600)
            {
                ConsiderTemporaryBrainSpawn(state);
                _lastCoordinationTick = currentTick;
            }
        }

        public IncidentCandidate ProposeIncident(IncidentContext context)
        {
            var rm = context.Restrictions;
            if (rm == null) return ProposeDefault(context);

            // --- Nature's Wrath: força sequência de desastres naturais de qualquer mod ---
            if (rm.ForcesNaturalDisasterSequence)
            {
                var disaster = PickNextNaturalDisaster(context);
                if (disaster != null)
                {
                    return new IncidentCandidate
                    {
                        Def = disaster,
                        Priority = 0.95f,
                        SourceBrainId = Id,
                        NarrativeTag = "NaturesWrath_Sequence",
                        ChainId = "natures_wrath_" + (context.DaysPassed / 5)
                    };
                }
            }

            // --- Zombie Apocalypse / forced defs: prioriza eventos forçados ---
            if (rm.HasForcedIncidents)
            {
                var forced = PickForcedIncident(rm);
                if (forced != null)
                {
                    return new IncidentCandidate
                    {
                        Def = forced,
                        Priority = 0.9f,
                        SourceBrainId = Id,
                        NarrativeTag = "Restriction_Forced",
                        SuggestedPoints = (int)(context.ColonyWealth * 0.1f)
                    };
                }
            }

            return ProposeDefault(context);
        }

        private IncidentCandidate ProposeDefault(IncidentContext context)
        {
            if (context.CurrentMenace < 0.25f && context.PlayerProfile?.Style == PlayerStyle.ComfortSeeker)
            {
                return new IncidentCandidate
                {
                    Priority = 0.7f,
                    SourceBrainId = Id,
                    NarrativeTag = "Director_PressureInjection",
                    SuggestedPoints = (int)(context.ColonyWealth * 0.08f)
                };
            }
            return null;
        }

        private IncidentDef PickForcedIncident(RestrictionManager rm)
        {
            foreach (var defName in rm.GetForcedDefNames())
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
                if (def != null && !rm.IsIncidentBlocked(def))
                    return def;
            }

            var candidates = ModIncidentCatalog.Instance.GetCrossModCandidates()
                .Where(d => rm.IsIncidentForced(d) && !rm.IsIncidentBlocked(d))
                .ToList();

            if (candidates.Count == 0) return null;
            return candidates[Rand.Range(0, candidates.Count)];
        }

        private IncidentDef PickNextNaturalDisaster(IncidentContext context)
        {
            var rm = context.Restrictions;
            var all = ModIncidentCatalog.Instance.GetCrossModCandidates()
                .Where(d => rm.IsNaturalDisaster(d) && !rm.IsIncidentBlocked(d))
                .ToList();

            foreach (var def in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (rm.IsNaturalDisaster(def) && !rm.IsIncidentBlocked(def) && !all.Contains(def))
                    all.Add(def);
            }

            if (all.Count == 0) return null;

            _disasterSequenceIndex = (_disasterSequenceIndex + 1) % all.Count;
            return all[_disasterSequenceIndex];
        }

        public void NotifyOutcome(IncidentCandidate candidate, IncidentOutcome outcome)
        {
            var gc = Current.Game?.GetComponent<XenoDirectorGameComponent>();
            if (gc == null) return;

            switch (outcome)
            {
                case IncidentOutcome.PlayerThrived:
                    gc.LowerMenace(0.08f);
                    break;
                case IncidentOutcome.PlayerSuffered:
                    gc.RaiseMenace(0.12f, "player suffered");
                    break;
                case IncidentOutcome.PlayerExploited:
                    gc.RaiseMenace(0.18f, "player exploited event");
                    break;
            }
        }

        private void ConsiderTemporaryBrainSpawn(XenoDirectorGameComponent state)
        {
            if (!XenoDirectorMod.Settings.AggressiveOptimization) return;
            if (state.TemporaryBrains.Count >= 4) return;

            var rm = state.Restrictions;
            if (rm != null && rm.IsActive("ZombieApocalypse") && state.MenaceGauge > 0.5f)
            {
                if (!state.TemporaryBrains.Any(b => b.Role == MicroBrainRole.Anomaly))
                {
                    var temp = BrainFactory.CreateTemporaryMicroBrain(MicroBrainRole.Anomaly);
                    state.TemporaryBrains.Add(temp);
                    RegisterMicroBrain(temp);
                    Log.Message("[XenoDirector] Temporary Anomaly micro-brain spawned for Zombie Apocalypse");
                }
            }

            if (rm != null && rm.ForcesNaturalDisasterSequence)
            {
                if (!state.TemporaryBrains.Any(b => b.Role == MicroBrainRole.CrossMod))
                {
                    var temp = BrainFactory.CreateTemporaryMicroBrain(MicroBrainRole.CrossMod);
                    state.TemporaryBrains.Add(temp);
                    RegisterMicroBrain(temp);
                    Log.Message("[XenoDirector] Temporary CrossMod micro-brain spawned for Nature's Wrath");
                }
            }

            if (state.MenaceGauge > 0.7f && state.PlayerProfile?.Style == PlayerStyle.Aggressive)
            {
                var temp = BrainFactory.CreateTemporaryMicroBrain(MicroBrainRole.Combat);
                state.TemporaryBrains.Add(temp);
                RegisterMicroBrain(temp);
            }
        }

        public IReadOnlyList<IMicroBrain> GetActiveMicroBrains() => _microBrains;
    }
}
