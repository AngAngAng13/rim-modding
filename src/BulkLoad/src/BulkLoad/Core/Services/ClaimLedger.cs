using System.Collections.Generic;
using Verse;

namespace BulkLoad
{
    internal interface IClaimLedger
    {
        int ClaimedByOthers(Thing destination, Pawn pawn, ThingDef def);
        void AddClaim(Pawn pawn, Thing destination, Dictionary<ThingDef, int> plan);
        void SettleTransaction(Pawn pawn, Thing destination, ThingDef def, int count);
        void ReleaseClaims(Pawn pawn);
        string DescribeClaims(Thing destination);
    }

    internal sealed class ClaimLedger : IClaimLedger
    {
        private sealed class DestinationClaims
        {
            public Dictionary<ThingDef, int> totalClaimed = new Dictionary<ThingDef, int>();
            public Dictionary<Pawn, Dictionary<ThingDef, int>> pawnClaims = new Dictionary<Pawn, Dictionary<ThingDef, int>>();
        }

        private readonly Dictionary<Thing, DestinationClaims> states = new Dictionary<Thing, DestinationClaims>();

        public int ClaimedByOthers(Thing destination, Pawn pawn, ThingDef def)
        {
            if (destination == null || pawn == null || def == null)
                return 0;
            if (!states.TryGetValue(destination, out DestinationClaims claims))
                return 0;

            int total = claims.totalClaimed.TryGetValue(def);
            int own = 0;
            if (claims.pawnClaims.TryGetValue(pawn, out Dictionary<ThingDef, int> ownClaims))
                own = ownClaims.TryGetValue(def);
            return System.Math.Max(0, total - own);
        }

        public void AddClaim(Pawn pawn, Thing destination, Dictionary<ThingDef, int> plan)
        {
            if (pawn == null || destination == null || plan == null)
                return;
            if (!states.TryGetValue(destination, out DestinationClaims claims))
            {
                claims = new DestinationClaims();
                states[destination] = claims;
            }

            ReleasePawnClaims(claims, pawn);
            if (plan.Count == 0)
                return;

            claims.pawnClaims[pawn] = new Dictionary<ThingDef, int>(plan);
            foreach (var entry in plan)
                claims.totalClaimed[entry.Key] = claims.totalClaimed.TryGetValue(entry.Key) + entry.Value;
        }

        public void SettleTransaction(Pawn pawn, Thing destination, ThingDef def, int count)
        {
            if (pawn == null || destination == null || def == null || count <= 0)
                return;
            if (!states.TryGetValue(destination, out DestinationClaims claims))
                return;

            int total = claims.totalClaimed.TryGetValue(def) - count;
            if (total > 0)
                claims.totalClaimed[def] = total;
            else
                claims.totalClaimed.Remove(def);

            if (claims.pawnClaims.TryGetValue(pawn, out Dictionary<ThingDef, int> ownClaims)
                && ownClaims.TryGetValue(def, out int own))
            {
                int left = own - count;
                if (left > 0)
                    ownClaims[def] = left;
                else
                    ownClaims.Remove(def);
                if (ownClaims.Count == 0)
                    claims.pawnClaims.Remove(pawn);
            }

            if (claims.totalClaimed.Count == 0 && claims.pawnClaims.Count == 0)
                states.Remove(destination);
        }

        public void ReleaseClaims(Pawn pawn)
        {
            if (pawn == null)
                return;
            List<Thing> emptied = null;
            foreach (var entry in states)
            {
                ReleasePawnClaims(entry.Value, pawn);
                if (entry.Value.totalClaimed.Count == 0 && entry.Value.pawnClaims.Count == 0)
                {
                    if (emptied == null)
                        emptied = new List<Thing>();
                    emptied.Add(entry.Key);
                }
            }
            if (emptied == null)
                return;
            foreach (Thing destination in emptied)
                states.Remove(destination);
        }

        public string DescribeClaims(Thing destination)
        {
            if (destination == null || !states.TryGetValue(destination, out DestinationClaims claims))
                return "-";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var entry in claims.totalClaimed)
            {
                if (!first)
                    sb.Append(",");
                first = false;
                sb.Append(entry.Key?.defName).Append("=").Append(entry.Value);
            }
            return sb.Length == 0 ? "-" : sb.ToString();
        }

        private static void ReleasePawnClaims(DestinationClaims claims, Pawn pawn)
        {
            if (!claims.pawnClaims.TryGetValue(pawn, out Dictionary<ThingDef, int> ownClaims))
                return;
            foreach (var entry in ownClaims)
            {
                int total = claims.totalClaimed.TryGetValue(entry.Key) - entry.Value;
                if (total > 0)
                    claims.totalClaimed[entry.Key] = total;
                else
                    claims.totalClaimed.Remove(entry.Key);
            }
            claims.pawnClaims.Remove(pawn);
        }
    }
}
