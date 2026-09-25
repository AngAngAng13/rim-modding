using CombatExtended;
using RimWorld;
using Verse;
using Verse.AI;

namespace FirefoamThrower
{
    public static class FirefoamThrowUtility
    {
        public static bool TryMakeThrowJob(Pawn pawn, Thing fire, out Job job)
        {
            job = null;
            Fire fireThing = fire as Fire;
            if (pawn == null || fireThing == null || !fireThing.Spawned || fireThing.Destroyed)
            {
                return false;
            }
            if (fireThing.parent is Pawn)
            {
                return false;
            }
            if (pawn.Faction != Faction.OfPlayer || pawn.Drafted)
            {
                return false;
            }
            ThingDef foamDef = DefDatabase<ThingDef>.GetNamed("CE_Weapon_GrenadeFirefoam", false);
            JobDef throwDef = DefDatabase<JobDef>.GetNamed("ThrowFirefoamGrenade", false);
            if (foamDef == null || throwDef == null)
            {
                return false;
            }
            ThingWithComps grenade = FindGrenade(pawn, foamDef);
            if (grenade == null)
            {
                return false;
            }
            if (HandledBy(fireThing, pawn) != null)
            {
                return false;
            }
            job = JobMaker.MakeJob(throwDef, grenade, fireThing);
            job.maxNumStaticAttacks = 1;
            return true;
        }

        private static ThingWithComps FindGrenade(Pawn pawn, ThingDef foamDef)
        {
            if (pawn.equipment?.Primary != null && pawn.equipment.Primary.def == foamDef)
            {
                return pawn.equipment.Primary;
            }
            ThingWithComps grenade = FindIn(pawn.TryGetComp<CompInventory>()?.container, foamDef);
            if (grenade != null)
            {
                return grenade;
            }
            grenade = FindIn(pawn.inventory?.innerContainer, foamDef);
            if (grenade != null)
            {
                return grenade;
            }
            Thing hauled = pawn.carryTracker?.CarriedThing;
            if (hauled != null && hauled.def == foamDef && hauled is ThingWithComps haulGrenade)
            {
                return haulGrenade;
            }
            return null;
        }

        private static ThingWithComps FindIn(ThingOwner owner, ThingDef foamDef)
        {
            if (owner == null)
            {
                return null;
            }
            foreach (Thing thing in owner)
            {
                if (thing.def == foamDef && thing is ThingWithComps grenade)
                {
                    return grenade;
                }
            }
            return null;
        }

        private static Pawn HandledBy(Fire fire, Pawn pawn)
        {
            Pawn reserver = fire.Map.reservationManager.FirstRespectedReserver(fire, pawn);
            return reserver != null && reserver.Position.InHorDistOf(fire.Position, 5f) ? reserver : null;
        }
    }
}
