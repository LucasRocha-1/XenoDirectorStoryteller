using System.Collections.Generic;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Perfil de como o JOGADOR reage aos eventos.
    /// Técnicas usadas:
    /// - Pré-processamento de sinais (wealth delta, mood, deaths, thoughts)
    /// - Classificação de estilo (PlayerStyle)
    /// - Histórico bounded (otimização para saves longos)
    /// - Pronto para clusterização / regressão futura (ex.: predizer próximo estilo)
    /// </summary>
    public class PlayerReactionProfile : IExposable
    {
        public PlayerStyle Style = PlayerStyle.Balanced;
        public float AggressionScore;   // 0..1
        public float CautionScore;
        public float StorySeekingScore;
        public float ExploitationScore; // quanto o jogador "quebra" eventos

        // Histórico bounded (não cresce indefinidamente)
        private List<ReactionSample> _recentSamples = new List<ReactionSample>();
        private const int MaxSamples = 128; // hard limit para performance

        public void ExposeData()
        {
            Scribe_Values.Look(ref Style, "Style", PlayerStyle.Balanced);
            Scribe_Values.Look(ref AggressionScore, "AggressionScore");
            Scribe_Values.Look(ref CautionScore, "CautionScore");
            Scribe_Values.Look(ref StorySeekingScore, "StorySeekingScore");
            Scribe_Values.Look(ref ExploitationScore, "ExploitationScore");
            Scribe_Collections.Look(ref _recentSamples, "RecentSamples", LookMode.Deep);
            if (_recentSamples == null) _recentSamples = new List<ReactionSample>();
        }

        /// <summary>
        /// Registra uma amostra de reação após um incidente.
        /// Chamado pelo sistema de observação (Harmony patches + GameComponent).
        /// </summary>
        public void RecordReaction(ReactionSample sample)
        {
            if (!XenoDirectorMod.Settings.EnablePlayerReactionTracking) return;

            _recentSamples.Add(sample);
            if (_recentSamples.Count > MaxSamples)
                _recentSamples.RemoveAt(0); // FIFO – bounded

            RecalculateScores();
        }

        private void RecalculateScores()
        {
            if (_recentSamples.Count == 0) return;

            float agg = 0, cau = 0, story = 0, expl = 0;
            int n = _recentSamples.Count;

            foreach (var s in _recentSamples)
            {
                // Pontua sinais observáveis; a classificação atual usa limiares simples.
                if (s.WealthDelta > 0 && s.ColonistDeaths == 0) agg += 0.3f;
                if (s.ColonistDeaths > 0) cau += 0.4f;
                if (s.MoodSwing < -15f) cau += 0.2f;
                if (s.WasPartOfQuestChain) story += 0.5f;
                if (s.EventWasExploited) expl += 0.6f;
            }

            AggressionScore = UnityEngine.Mathf.Clamp01(agg / n);
            CautionScore = UnityEngine.Mathf.Clamp01(cau / n);
            StorySeekingScore = UnityEngine.Mathf.Clamp01(story / n);
            ExploitationScore = UnityEngine.Mathf.Clamp01(expl / n);

            // Classificação final (pode ser substituída por modelo de ML offline treinado)
            Style = ClassifyStyle(AggressionScore, CautionScore, StorySeekingScore, ExploitationScore);
        }

        private static PlayerStyle ClassifyStyle(float agg, float cau, float story, float expl)
        {
            if (expl > 0.45f) return PlayerStyle.Exploiter;
            if (story > 0.4f) return PlayerStyle.StoryDriven;
            if (agg > 0.4f && cau < 0.3f) return PlayerStyle.Aggressive;
            if (cau > 0.4f) return PlayerStyle.Cautious;
            if (agg < 0.2f && cau < 0.2f) return PlayerStyle.ComfortSeeker;
            return PlayerStyle.Balanced;
        }

        public void PruneOldData(int retentionDays)
        {
            int cutoff = Find.TickManager.TicksGame - retentionDays * 60000;
            _recentSamples.RemoveAll(s => s.Tick < cutoff);
        }
    }

    public enum PlayerStyle
    {
        Balanced,
        Aggressive,
        Cautious,
        StoryDriven,
        ComfortSeeker,
        Exploiter
    }

    public class ReactionSample : IExposable
    {
        public int Tick;
        public string IncidentDefName;
        public float WealthDelta;
        public int ColonistDeaths;
        public float MoodSwing;
        public bool WasPartOfQuestChain;
        public bool EventWasExploited;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "Tick");
            Scribe_Values.Look(ref IncidentDefName, "IncidentDefName");
            Scribe_Values.Look(ref WealthDelta, "WealthDelta");
            Scribe_Values.Look(ref ColonistDeaths, "ColonistDeaths");
            Scribe_Values.Look(ref MoodSwing, "MoodSwing");
            Scribe_Values.Look(ref WasPartOfQuestChain, "WasPartOfQuestChain");
            Scribe_Values.Look(ref EventWasExploited, "EventWasExploited");
        }
    }
}
