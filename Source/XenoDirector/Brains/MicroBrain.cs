using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// MICRO-BRAIN genérico. Pode ser especializado por Role.
    /// Consulta RestrictionManager antes de propor qualquer coisa.
    /// </summary>
    public class MicroBrain : IMicroBrain
    {
        public string Id { get; }
        public string TypeName => "Micro_" + Role;
        public bool IsTemporary { get; }
        public float CurrentInternalTension { get; private set; }
        public MicroBrainRole Role { get; }

        private int _lastProposalTick;
        private readonly System.Random _rng;

        public MicroBrain(MicroBrainRole role, bool temporary = false)
        {
            Role = role;
            IsTemporary = temporary;
            Id = $"Micro_{role}_{(temporary ? "T" : "P")}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            _rng = new System.Random(Id.GetHashCode());
        }

        public void Tick(int currentTick, XenoDirectorGameComponent state)
        {
            float bias = state.Restrictions?.MenaceBias ?? 0f;
            float target = (state.MenaceGauge + bias * 0.2f) * (Role == MicroBrainRole.Combat ? 1.15f : 0.9f);
            CurrentInternalTension = UnityEngine.Mathf.Lerp(CurrentInternalTension, UnityEngine.Mathf.Clamp01(target), 0.05f);
        }

        public IncidentCandidate ProposeIncident(IncidentContext context)
        {
            if (context.AggressiveOptimization && Find.TickManager.TicksGame - _lastProposalTick < 1200)
                return null;

            _lastProposalTick = Find.TickManager.TicksGame;
            var rm = context.Restrictions;

            // Social brain não tenta trazer gente nova sob "Single"
            if (rm != null && Role == MicroBrainRole.Social && rm.BlocksRecruitment)
                return null;

            float basePriority = CurrentInternalTension * 0.6f + (float)_rng.NextDouble() * 0.4f;

            // Combat + restrição de raid
            if (Role == MicroBrainRole.Combat && context.CurrentMenace > 0.35f)
            {
                var candidate = new IncidentCandidate
                {
                    Priority = basePriority + 0.2f,
                    SourceBrainId = Id,
                    NarrativeTag = (rm != null && rm.RestrictsRaids) ? "Combat_RestrictedRaid" : "Combat_Pressure",
                    SuggestedPoints = (int)(context.ColonyWealth * 0.12f)
                };
                return candidate;
            }

            // Anomaly brain sob Zombie Apocalypse
            if (Role == MicroBrainRole.Anomaly && rm != null && rm.IsActive("ZombieApocalypse"))
            {
                IncidentDef forced = null;
                foreach (var name in rm.GetForcedDefNames())
                {
                    forced = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
                    if (forced != null && !rm.IsIncidentBlocked(forced)) break;
                }

                if (forced != null)
                {
                    return new IncidentCandidate
                    {
                        Def = forced,
                        Priority = 0.92f,
                        SourceBrainId = Id,
                        NarrativeTag = "Anomaly_Forced"
                    };
                }
            }

            // CrossMod + Nature's Wrath
            if (Role == MicroBrainRole.CrossMod && rm != null && rm.ForcesNaturalDisasterSequence)
            {
                var disasters = ModIncidentCatalog.Instance.GetCrossModCandidates()
                    .Where(d => rm.IsNaturalDisaster(d) && !rm.IsIncidentBlocked(d))
                    .ToList();

                if (disasters.Count > 0)
                {
                    var pick = disasters[_rng.Next(disasters.Count)];
                    return new IncidentCandidate
                    {
                        Def = pick,
                        Priority = 0.88f,
                        SourceBrainId = Id,
                        NarrativeTag = "CrossMod_Disaster",
                        PreferredModPackageId = pick.modContentPack?.PackageId
                    };
                }
            }

            if (Role == MicroBrainRole.Narrative && context.PlayerProfile?.Style == PlayerStyle.StoryDriven)
            {
                return new IncidentCandidate
                {
                    Priority = basePriority + 0.15f,
                    SourceBrainId = Id,
                    NarrativeTag = "Narrative_Arc",
                    ChainId = "story_" + context.DaysPassed / 15
                };
            }

            if (Role == MicroBrainRole.CrossMod && XenoDirectorMod.Settings.EnableCrossModCoordination)
            {
                return new IncidentCandidate
                {
                    Priority = basePriority,
                    SourceBrainId = Id,
                    NarrativeTag = "CrossMod_Coordination"
                };
            }

            return null;
        }

        public void NotifyOutcome(IncidentCandidate candidate, IncidentOutcome outcome)
        {
            if (outcome == IncidentOutcome.PlayerExploited)
                CurrentInternalTension = UnityEngine.Mathf.Clamp01(CurrentInternalTension + 0.1f);
            else if (outcome == IncidentOutcome.PlayerThrived)
                CurrentInternalTension = UnityEngine.Mathf.Clamp01(CurrentInternalTension - 0.05f);
        }
    }

    public static class BrainFactory
    {
        public static DirectorBrain CreateDirector() => new DirectorBrain();

        public static IMicroBrain CreatePermanentMicroBrain(MicroBrainRole role)
            => new MicroBrain(role, temporary: false);

        public static IMicroBrain CreateTemporaryMicroBrain(MicroBrainRole role)
            => new MicroBrain(role, temporary: true);

        public static System.Collections.Generic.List<IMicroBrain> CreatePermanentSet(int count)
        {
            var list = new System.Collections.Generic.List<IMicroBrain>();
            var roles = new[]
            {
                MicroBrainRole.Combat,
                MicroBrainRole.Narrative,
                MicroBrainRole.Economy,
                MicroBrainRole.Social,
                MicroBrainRole.Anomaly,
                MicroBrainRole.CrossMod,
                MicroBrainRole.Generic
            };

            for (int i = 0; i < count; i++)
            {
                var role = roles[i % roles.Length];
                list.Add(CreatePermanentMicroBrain(role));
            }
            return list;
        }
    }
}
