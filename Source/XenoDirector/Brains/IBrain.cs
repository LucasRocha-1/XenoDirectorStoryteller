using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Contrato comum para todos os cérebros (Director + Micros).
    /// Permite escalar o número de cérebros sem mudar a lógica de orquestração.
    /// </summary>
    public interface IBrain
    {
        string Id { get; }
        string TypeName { get; }
        bool IsTemporary { get; }
        float CurrentInternalTension { get; }

        /// <summary>
        /// Chamado periodicamente pelo orquestrador. Deve ser barato.
        /// </summary>
        void Tick(int currentTick, XenoDirectorGameComponent state);

        /// <summary>
        /// Decide se este cérebro quer propor um incidente/quest neste momento.
        /// Retorna null se não tiver proposta.
        /// </summary>
        IncidentCandidate ProposeIncident(IncidentContext context);

        /// <summary>
        /// Notifica o cérebro sobre o resultado de um evento (sucesso, falha, reação do jogador).
        /// Usado para aprendizado / adaptação de estilo.
        /// </summary>
        void NotifyOutcome(IncidentCandidate candidate, IncidentOutcome outcome);
    }

    public interface IMicroBrain : IBrain
    {
        /// <summary>
        /// Especialização do micro-cérebro (combate, narrativa, economia, anomalia, etc.).
        /// </summary>
        MicroBrainRole Role { get; }
    }

    public enum MicroBrainRole
    {
        Combat,
        Narrative,
        Economy,
        Social,
        Anomaly,
        CrossMod,
        Generic
    }

    /// <summary>
    /// Proposta de incidente gerada por um cérebro.
    /// </summary>
    public class IncidentCandidate
    {
        public IncidentDef Def;
        public float Priority;
        public string SourceBrainId;
        public string NarrativeTag;          // para encadear arcos
        public string PreferredModPackageId; // se veio de um mod específico
        public int SuggestedPoints;
        public string ChainId;               // para multi-step stories
    }

    public class IncidentContext
    {
        public Map Map;
        public float CurrentMenace;
        public PlayerReactionProfile PlayerProfile;
        public RestrictionManager Restrictions;
        public float ColonyWealth;
        public int DaysPassed;
        public bool AggressiveOptimization;
    }

    public enum IncidentOutcome
    {
        PlayerThrived,      // jogador lidou bem / se fortaleceu
        PlayerSuffered,     // perdas significativas
        PlayerIgnored,      // pouco impacto percebido
        PlayerExploited,    // jogador "quebrou" o evento
        Neutral
    }
}
