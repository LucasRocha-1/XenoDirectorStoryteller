using System;
using System.Collections.Concurrent;
using System.Threading;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Fila de trabalho que PODE rodar fora da main thread.
    ///
    /// REGRAS ESTRITAS (RimWorld / Unity):
    /// - NUNCA tocar em Find.*, Current.Game, Map, Pawn, DefDatabase em mutação
    ///   a partir da thread de fundo.
    /// - Apenas computação pura: análise de dados já copiados, rebuild de índices
    ///   imutáveis, estatísticas, serialização de snapshots.
    /// - O resultado volta para a main thread via Completion queue.
    ///
    /// Isto NÃO "dá mais threads ao jogo" de forma geral — só isola trabalho
    /// que realmente é thread-safe. O ganho real de fluidez vem do
    /// CooperativeScheduler (prioridade + hibernação).
    /// </summary>
    public static class SafeBackgroundQueue
    {
        private static readonly ConcurrentQueue<Action> _jobs = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Action> _completions = new ConcurrentQueue<Action>();
        private static int _running;
        private const int MaxWorkers = 2;

        public static void EnqueueCompute(Action compute, Action onMainThreadComplete = null)
        {
            if (compute == null) return;

            _jobs.Enqueue(() =>
            {
                try
                {
                    compute();
                    if (onMainThreadComplete != null)
                        _completions.Enqueue(onMainThreadComplete);
                }
                catch (Exception e)
                {
                    Log.Warning($"[XenoDirector.Opt] Background compute failed: {e.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref _running);
                    TryStartWorkers();
                }
            });

            TryStartWorkers();
        }

        private static void TryStartWorkers()
        {
            while (_jobs.Count > 0 && _running < MaxWorkers)
            {
                if (!_jobs.TryDequeue(out var job)) break;
                Interlocked.Increment(ref _running);
                ThreadPool.QueueUserWorkItem(_ => job());
            }
        }

        /// <summary>
        /// Chamado na main thread (GameComponentTick) para aplicar resultados.
        /// </summary>
        public static void PumpCompletions(int maxPerCall = 4)
        {
            int n = 0;
            while (n < maxPerCall && _completions.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception e)
                {
                    Log.Warning($"[XenoDirector.Opt] Completion failed: {e.Message}");
                }
                n++;
            }
        }

        public static int PendingJobs => _jobs.Count;
        public static int PendingCompletions => _completions.Count;
    }
}
