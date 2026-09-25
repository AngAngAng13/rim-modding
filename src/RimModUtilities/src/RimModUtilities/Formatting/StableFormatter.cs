using Verse;
using Verse.AI;

namespace RimModUtilities
{
    public static class StableFormatter
    {
        public static string Pawn(Pawn pawn)
        {
            return Thing(pawn);
        }

        public static string Thing(Thing thing)
        {
            if (thing == null)
                return "-";

            string defName = thing.def?.defName ?? thing.GetType().Name;
            return Clean(defName) + "#" + thing.thingIDNumber;
        }

        public static string Target(LocalTargetInfo target)
        {
            try
            {
                if (target.Thing != null)
                    return Thing(target.Thing);
                if (target.Cell.IsValid)
                    return Clean(target.Cell.ToString());
            }
            catch
            {
            }

            return "-";
        }

        public static string Job(Job job)
        {
            if (job == null)
                return "job=-";

            return "job=" + Clean(job.def?.defName)
                + " targetA=" + Target(job, TargetIndex.A)
                + " targetB=" + Target(job, TargetIndex.B)
                + " targetC=" + Target(job, TargetIndex.C)
                + " count=" + job.count
                + " playerForced=" + job.playerForced;
        }

        public static string Target(Job job, TargetIndex index)
        {
            if (job == null)
                return "-";

            try
            {
                return Target(job.GetTarget(index));
            }
            catch
            {
                return "-";
            }
        }

        private static string Clean(string value)
        {
            return (value ?? "-")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "/");
        }
    }
}
