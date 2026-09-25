#if DEBUG
using Verse;

namespace RimModUtilities.Pawns.Trace
{
    public sealed class TraceComponent : GameComponent
    {
        public TraceComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            Watcher.Tick();
        }
    }
}
#endif
