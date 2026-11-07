// Helper para evitar warnings de referência circular / facilitar testes unitários futuros.
// Em produção usar Verse.Current normalmente.

namespace XenoDirector
{
    public static class Current
    {
        public static Verse.Game Game => Verse.Current.Game;
    }
}
