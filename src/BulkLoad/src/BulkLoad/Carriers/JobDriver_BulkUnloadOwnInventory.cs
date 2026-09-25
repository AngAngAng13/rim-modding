using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public class JobDriver_BulkUnloadOwnInventory : JobDriver_DrainToStorageBase
    {
        protected override string DumpActionKind => "own_dump";
        protected override string UnplacedItemNoun => "own item";

        protected override void FindNextItem()
        {
            if (!pawn.inventory.UnloadEverything)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            ThingCount first = pawn.inventory.FirstUnloadableThing;
            if (first == default(ThingCount) || first.Thing == null || first.Thing.Destroyed
                || !pawn.inventory.innerContainer.Contains(first.Thing))
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }

            var item = first.Thing;
            if (StoreUtility.TryFindBestBetterStoreCellFor(
                item,
                pawn,
                pawn.Map,
                StoragePriority.Unstored,
                pawn.Faction,
                out IntVec3 storeCell))
            {
                job.SetTarget(TargetIndex.B, storeCell);
                job.SetTarget(TargetIndex.C, item);
                job.count = Mathf.Min(first.Count, item.stackCount);
                _storageTarget = true;
                return;
            }

            job.SetTarget(TargetIndex.B, pawn.Position);
            job.SetTarget(TargetIndex.C, item);
            job.count = Mathf.Min(first.Count, item.stackCount);
            _storageTarget = false;
            BulkLoadDiagnostics.Warning(
                "carriers",
                "No storage cell found for " + item.def?.defName
                + "; using the standard non-storage drop fallback.");
        }

        protected override bool HasMoreWork()
        {
            if (!pawn.inventory.UnloadEverything)
                return false;
            ThingCount first = pawn.inventory.FirstUnloadableThing;
            return first != default(ThingCount) && first.Thing != null && !first.Thing.Destroyed
                && pawn.inventory.innerContainer.Contains(first.Thing);
        }

        public override string GetReport()
        {
            return "Bulk unloading own inventory.";
        }
    }
}
