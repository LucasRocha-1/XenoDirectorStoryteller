using System.Collections.Generic;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Sempre ativo com o mod carregado (qualquer storyteller).
    /// - OptimizationDirector + AdvancedPerformance (opt-in)
    /// - RestrictionManager (restrições sempre aplicadas via Harmony)
    /// - StoryArcManager (arcos narrativos se habilitados)
    /// Narrativa completa de micro-cérebros de storyteller só com XenoDirector selecionado.
    /// </summary>
    public class XenoDirectorGameComponent : GameComponent
    {
        public float MenaceGauge;
        public PlayerReactionProfile PlayerProfile;
        public RestrictionManager Restrictions = new RestrictionManager();
        public StoryArcManager Arcs = new StoryArcManager();
        public List<BrainState> ActiveBrainStates = new List<BrainState>();

        [Unsaved] public List<IMicroBrain> TemporaryBrains = new List<IMicroBrain>();
        [Unsaved] public OptimizationDirector OptDirector;
        [Unsaved] public AdvancedPerformanceController AdvPerf;

        private int _lastCleanupTick;
        private int _lastOptTick;

        public XenoDirectorGameComponent(Game game)
        {
            OptDirector = new OptimizationDirector();
            AdvPerf = new AdvancedPerformanceController();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref MenaceGauge, "MenaceGauge", 0.35f);
            Scribe_Deep.Look(ref PlayerProfile, "PlayerProfile");
            Scribe_Deep.Look(ref Restrictions, "Restrictions");
            Scribe_Deep.Look(ref Arcs, "Arcs");
            Scribe_Collections.Look(ref ActiveBrainStates, "ActiveBrainStates", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (PlayerProfile == null) PlayerProfile = new PlayerReactionProfile();
                if (Restrictions == null) Restrictions = new RestrictionManager();
                if (Arcs == null) Arcs = new StoryArcManager();
                if (ActiveBrainStates == null) ActiveBrainStates = new List<BrainState>();
                Restrictions.EnsureRegistry();
                if (OptDirector == null) OptDirector = new OptimizationDirector();
                if (AdvPerf == null) AdvPerf = new AdvancedPerformanceController();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (OptDirector == null) OptDirector = new OptimizationDirector();
            if (AdvPerf == null) AdvPerf = new AdvancedPerformanceController();
            OptDirector.EnsureInitialized();
            Restrictions?.EnsureRegistry();
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;

            // Otimização base — sempre
            if (tick - _lastOptTick >= 30)
            {
                _lastOptTick = tick;
                OptDirector?.Tick(this);
                AdvPerf?.Tick();
            }

            // Arcos narrativos — independentes do storyteller se setting ON
            Arcs?.Tick(this);

            // Menace decay
            if (tick % 250 != 0) return;

            float decay = 0.0008f * (XenoDirectorMod.Settings?.MenaceDecayMultiplier ?? 1f);
            float bias = Restrictions?.MenaceBias ?? 0f;
            decay *= (1f - UnityEngine.Mathf.Clamp(bias, -0.5f, 0.5f));
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge - decay);

            if (tick - _lastCleanupTick > 60000)
            {
                PlayerProfile?.PruneOldData(XenoDirectorMod.Settings?.HistoryRetentionDays ?? 60);
                _lastCleanupTick = tick;
            }
        }

        public void RaiseMenace(float amount, string reason = null)
        {
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge + amount);
            OptDirector?.NotifyActivity(GameSubsystemId.Incidents);
        }

        public void LowerMenace(float amount)
        {
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge - amount);
        }

        public static RestrictionManager GetRestrictions()
            => Current.Game?.GetComponent<XenoDirectorGameComponent>()?.Restrictions;

        public static OptimizationDirector GetOpt()
            => Current.Game?.GetComponent<XenoDirectorGameComponent>()?.OptDirector;

        public static StoryArcManager GetArcs()
            => Current.Game?.GetComponent<XenoDirectorGameComponent>()?.Arcs;

        public static bool IsXenoStorytellerActive()
        {
            var st = Find.Storyteller?.def;
            return st != null && st.defName == "XenoDirector";
        }
    }

    public class BrainState : IExposable
    {
        public string BrainId;
        public string BrainType;
        public float InternalTension;
        public int SpawnTick;
        public bool IsTemporary;

        public void ExposeData()
        {
            Scribe_Values.Look(ref BrainId, "BrainId");
            Scribe_Values.Look(ref BrainType, "BrainType");
            Scribe_Values.Look(ref InternalTension, "InternalTension");
            Scribe_Values.Look(ref SpawnTick, "SpawnTick");
            Scribe_Values.Look(ref IsTemporary, "IsTemporary");
        }
    }
}
