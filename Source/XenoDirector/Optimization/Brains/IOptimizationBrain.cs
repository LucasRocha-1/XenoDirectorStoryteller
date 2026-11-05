namespace XenoDirector
{
    /// <summary>
    /// Cérebro de otimização — independente do storyteller selecionado.
    /// Cada um observa um domínio e decide se aquele domínio deve ficar
    /// em Foreground, Secondary, Background ou Hibernating.
    /// </summary>
    public interface IOptimizationBrain
    {
        string Id { get; }
        GameSubsystemId PrimarySubsystem { get; }

        /// <summary>
        /// Chamado pelo OptimizationDirector quando o scheduler autoriza.
        /// Deve ser barato; trabalho pesado vai para Enqueue/SafeBackgroundQueue.
        /// </summary>
        void ObserveAndAdvise(OptimizationContext ctx);
    }

    public class OptimizationContext
    {
        public CooperativeScheduler Scheduler;
        public XenoDirectorGameComponent GameState;
        public int Tick;
        public bool AggressiveMode;
        public int ColonistCount;
        public int MapCount;
        public int ModCount;
        public bool InCombat;
    }
}
