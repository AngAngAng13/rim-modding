using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    internal interface IActiveJobRegistry
    {
        void Register(Pawn pawn, Job job, Thing destination);
        bool Unregister(Pawn pawn, Job job);
        bool HasActiveJobFor(Thing destination);
        bool IsActiveLoader(Pawn pawn, Thing destination);
        string DescribeActive();
    }

    internal sealed class ActiveJobRegistry : IActiveJobRegistry
    {
        private sealed class ActiveJobState
        {
            public Pawn pawn;
            public Thing destination;
        }

        private readonly Dictionary<Job, ActiveJobState> activeJobs = new Dictionary<Job, ActiveJobState>();

        public void Register(Pawn pawn, Job job, Thing destination)
        {
            if (pawn == null || job == null)
                return;
            activeJobs[job] = new ActiveJobState { pawn = pawn, destination = destination };
        }

        public bool Unregister(Pawn pawn, Job job)
        {
            if (pawn == null || job == null)
                return false;
            return activeJobs.Remove(job);
        }

        public bool HasActiveJobFor(Thing destination)
        {
            if (destination == null)
                return false;
            foreach (var entry in activeJobs)
            {
                if (entry.Value.destination == destination)
                    return true;
            }
            return false;
        }

        public bool IsActiveLoader(Pawn pawn, Thing destination)
        {
            if (pawn == null || destination == null)
                return false;
            foreach (var entry in activeJobs)
            {
                if (entry.Value.pawn == pawn && entry.Value.destination == destination)
                    return true;
            }
            return false;
        }

        public string DescribeActive()
        {
            if (activeJobs.Count == 0)
                return "-";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var entry in activeJobs)
            {
                if (!first)
                    sb.Append(",");
                first = false;
                sb.Append(entry.Value.pawn?.LabelShort).Append("->")
                    .Append(entry.Value.destination?.def?.defName)
                    .Append("_").Append(entry.Value.destination?.thingIDNumber ?? -1);
            }
            return sb.ToString();
        }
    }
}
