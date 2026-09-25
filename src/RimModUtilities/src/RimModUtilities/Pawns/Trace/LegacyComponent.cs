using Verse;

namespace RimModUtilities
{
    public sealed class PawnTraceGameComponent : GameComponent
    {
        public PawnTraceGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
#if DEBUG
            Pawns.Trace.Watcher.Tick();
#endif
        }
    }
}
