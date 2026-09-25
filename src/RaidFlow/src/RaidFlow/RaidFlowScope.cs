using RimWorld;
using Verse;
using Verse.AI;

namespace RaidFlow
{
    public static class RaidFlowScope
    {
        public static bool IsRaidPath(PathRequest request)
        {
            if (request == null)
                return false;
            Pawn pawn = request.pawn;
            if (pawn == null || !pawn.Spawned)
                return false;
            if (request.map == null)
                return false;
            if (pawn.Faction == null || !pawn.Faction.HostileTo(Faction.OfPlayer))
                return false;
            if (!(request.RequesterLord?.LordJob is LordJob_AssaultColony))
                return false;
            if (request.TraverseParms.mode != TraverseMode.ByPawn)
                return false;
            if (request.EndMode != PathEndMode.OnCell)
                return false;
            if (request.customizer != null)
                return false;
            if (request.area != null)
                return false;
            return true;
        }
    }
}
