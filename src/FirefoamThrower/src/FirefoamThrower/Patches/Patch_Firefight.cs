using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FirefoamThrower
{
    [HarmonyPatch("RimWorld.WorkGiver_FightFires", "JobOnThing")]
    public static class Patch_Firefight_JobOnThing
    {
        public static void Postfix(Pawn pawn, Thing t, ref Job __result)
        {
            Patch_Firefight.ConvertBeatFire(pawn, t, ref __result);
        }
    }

    [HarmonyPatch("RimWorld.JobGiver_FightFiresNearPoint", "TryGiveJob")]
    public static class Patch_Firefight_TryGiveJob
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null)
            {
                return;
            }
            Patch_Firefight.ConvertBeatFire(pawn, __result.targetA.Thing, ref __result);
        }
    }

    public static class Patch_Firefight
    {
        public static void ConvertBeatFire(Pawn pawn, Thing fire, ref Job job)
        {
            if (job == null || job.def != JobDefOf.BeatFire)
            {
                return;
            }
            if (FirefoamThrowUtility.TryMakeThrowJob(pawn, fire, out Job throwJob))
            {
                job = throwJob;
            }
        }
    }
}
