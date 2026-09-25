#if DEBUG
using LudeonTK;
using Verse;

namespace StrongWind
{
    public static class Debug_SwirlTest
    {
        [DebugAction("StrongWind", "Swirl test pawn", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SwirlTestPawn(Pawn p)
        {
            if (p == null || !p.Spawned)
            {
                return;
            }
            Map map = p.Map;
            IntVec3 spawnCell = p.Position;
            SwirlFlyer flyer = SwirlFlyer.MakeSwirl(p, null);
            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, spawnCell, map);
            }
        }
    }
}
#endif
