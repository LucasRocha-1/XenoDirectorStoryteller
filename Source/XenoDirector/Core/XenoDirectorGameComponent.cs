using System.Collections.Generic;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// GameComponent persistente. Mantém o estado global dos cérebros, menace gauge,
    /// perfil de reação do jogador e restrições narrativas ativas entre saves.
    /// Projetado para ser leve mesmo em saves de centenas de anos.
    /// </summary>
    public class XenoDirectorGameComponent : GameComponent
    {
        public float MenaceGauge;                 // 0..1 (inspirado no menace do Director de Alien Isolation)
        public PlayerReactionProfile PlayerProfile;
        public RestrictionManager Restrictions = new RestrictionManager();
        public List<BrainState> ActiveBrainStates = new List<BrainState>();

        // Runtime-only temporary brains (não serializados permanentemente além do necessário)
        [Unsaved] public List<IMicroBrain> TemporaryBrains = new List<IMicroBrain>();

        private int _lastCleanupTick;

        public XenoDirectorGameComponent(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref MenaceGauge, "MenaceGauge", 0.35f);
            Scribe_Deep.Look(ref PlayerProfile, "PlayerProfile");
            Scribe_Deep.Look(ref Restrictions, "Restrictions");
            Scribe_Collections.Look(ref ActiveBrainStates, "ActiveBrainStates", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (PlayerProfile == null) PlayerProfile = new PlayerReactionProfile();
                if (Restrictions == null) Restrictions = new RestrictionManager();
                if (ActiveBrainStates == null) ActiveBrainStates = new List<BrainState>();
                Restrictions.EnsureRegistry();
            }
        }

        public override void GameComponentTick()
        {
            // Throttled: a cada 250 ticks (~4s) para não pesar em saves longos
            if (Find.TickManager.TicksGame % 250 != 0) return;

            // Decay natural da menace gauge + bias das restrições ativas
            float decay = 0.0008f * XenoDirectorMod.Settings.MenaceDecayMultiplier;
            float bias = Restrictions?.MenaceBias ?? 0f;
            // Bias positivo reduz o decay (mantém pressão); negativo acelera o decay
            decay *= (1f - UnityEngine.Mathf.Clamp(bias, -0.5f, 0.5f));
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge - decay);

            // Limpeza periódica de histórico antigo (otimização)
            if (Find.TickManager.TicksGame - _lastCleanupTick > 60000) // ~1 dia de jogo
            {
                PlayerProfile?.PruneOldData(XenoDirectorMod.Settings.HistoryRetentionDays);
                _lastCleanupTick = Find.TickManager.TicksGame;
            }
        }

        public void RaiseMenace(float amount, string reason = null)
        {
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge + amount);
        }

        public void LowerMenace(float amount)
        {
            MenaceGauge = UnityEngine.Mathf.Clamp01(MenaceGauge - amount);
        }

        /// <summary>
        /// Acesso rápido ao RestrictionManager (null-safe).
        /// </summary>
        public static RestrictionManager GetRestrictions()
        {
            return Current.Game?.GetComponent<XenoDirectorGameComponent>()?.Restrictions;
        }
    }

    /// <summary>
    /// Estado serializável de um cérebro (para restore após load).
    /// </summary>
    public class BrainState : IExposable
    {
        public string BrainId;
        public string BrainType; // "Director", "Micro_Combat", "Micro_Narrative", etc.
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
