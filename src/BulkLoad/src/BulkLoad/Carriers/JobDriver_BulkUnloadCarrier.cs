using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public class JobDriver_BulkUnloadCarrier : JobDriver_DrainToStorageBase
    {
        private const int ExclusiveMaxPawns = 1;
        private const int WholeStackCount = -1;

        private Pawn Carrier => job.GetTarget(TargetIndex.A).Thing as Pawn;

        private Dictionary<ThingDef, int> _carrierItems = new Dictionary<ThingDef, int>();
        private List<Thing> _grabbedItems = new List<Thing>();
        private Toil _gotoAnimal;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _carrierItems, "carrierItems", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref _grabbedItems, "grabbedItems", LookMode.Reference);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var carrier = Carrier;
            if (carrier == null || carrier.inventory == null || carrier.inventory.innerContainer.Count == 0)
            {
                BulkLoadDiagnostics.Debug("carriers", "Reservation skipped: carrier inventory was empty.");
                return false;
            }

            bool reserved = pawn.Reserve(carrier, job, ExclusiveMaxPawns, WholeStackCount, null, errorOnFailed);
            BulkLoadDiagnostics.Debug(
                "carriers",
                "Carrier reservation " + (reserved ? "succeeded" : "failed") + " carrier=" + carrier.LabelShort);
            return reserved;
        }

        protected override bool ReserveDestinationCell => false;
        protected override string DumpActionKind => "carrier_dump";
        protected override string UnplacedItemNoun => "carrier item";

        protected override void OnJobFinished(JobCondition jobCondition)
        {
            base.OnJobFinished(jobCondition);
            if (jobCondition == JobCondition.Succeeded || _carrierItems.Count == 0)
                return;
            BulkLoadDiagnostics.Debug(
                "carriers",
                "Returning tracked leftovers after " + jobCondition + " trackedDefs=" + _carrierItems.Count);
            BulkLoadServices.Pickups.DropTrackedItems(_carrierItems, pawn);
        }

        protected override IEnumerable<Toil> PreDrainToils()
        {
            _gotoAnimal = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            _gotoAnimal.FailOnDestroyedOrNull(TargetIndex.A);
            yield return _gotoAnimal;

            var grabAll = new Toil
            {
                initAction = () =>
                {
                    var carrier = Carrier;
                    if (carrier == null)
                        return;

                    BulkLoadDiagnostics.Debug(
                        "carriers",
                        "Pre-existing inventory for " + pawn.LabelShort + ": "
                        + BulkLoadDiagnostics.DescribeInventory(pawn) + " unloadEverything=" + pawn.inventory.UnloadEverything);

                    var sourceInv = carrier.inventory.innerContainer;
                    int movedCount = 0;

                    for (int i = sourceInv.Count - 1; i >= 0; i--)
                    {
                        var item = sourceInv[i];
                        if (item == null || item.Destroyed)
                            continue;

                        int canTake = MassUtility.CountToPickUpUntilOverEncumbered(pawn, item);
                        if (canTake <= 0)
                        {
                            BulkLoadDiagnostics.Debug(
                                "carriers",
                                "Skipping " + item.def?.defName + "x" + item.stackCount + " for " + pawn.LabelShort
                                + " encumbrance=" + MassUtility.EncumbrancePercent(pawn).ToString("F2"));
                            continue;
                        }

                        Thing toMove = item;
                        if (canTake < item.stackCount)
                            toMove = item.SplitOff(canTake);
                        else
                            sourceInv.Remove(item);

                        int itemCount = toMove.stackCount;
                        if (pawn.inventory.innerContainer.TryAdd(toMove))
                        {
                            _carrierItems[toMove.def] = _carrierItems.TryGetValue(toMove.def) + itemCount;
                            _grabbedItems.Add(toMove);
                            movedCount += itemCount;
                            BulkLoadDiagnostics.Debug(
                                "carriers",
                                "Grabbed " + itemCount + "x " + toMove.def?.defName + " for " + pawn.LabelShort);
                        }
                        else
                        {
                            sourceInv.TryAdd(toMove);
                            BulkLoadDiagnostics.Warning(
                                "carriers",
                                "Could not move " + toMove.def?.defName + " into " + pawn.LabelShort + " inventory.");
                        }
                    }

                    if (movedCount == 0 && carrier.inventory.innerContainer.Count > 0)
                    {
                        BulkLoadDiagnostics.Warning(
                            "carriers",
                            pawn.LabelShort + " cannot carry any carrier items (over-encumbered); leaving them for someone else.");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (carrier.inventory.innerContainer.Count == 0)
                    {
                        carrier.inventory.UnloadEverything = false;
                        if (carrier.RaceProps.packAnimal)
                            carrier.Drawer.renderer.SetAllGraphicsDirty();
                    }

                    BulkLoadDiagnostics.Debug(
                        "carriers",
                        "Moved " + movedCount + " items from " + carrier.LabelShort + " to " + pawn.LabelShort);
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "carrier_grab",
                        "carrier=" + carrier.LabelShort + " count=" + movedCount);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            grabAll.FailOnDestroyedOrNull(TargetIndex.A);
            yield return grabAll;
        }

        protected override IEnumerable<Toil> PostDrainToils()
        {
            yield return Toils_Jump.JumpIf(_gotoAnimal, () =>
            {
                bool moreCarrier = CarrierHasItems();
                BulkLoadDiagnostics.Debug(
                    "carriers",
                    "Loop check pawn=" + pawn.LabelShort + " moreCarrier=" + moreCarrier);
                return moreCarrier;
            });
        }

        protected override void FindNextItem()
        {
            var inv = pawn.inventory.innerContainer;
            Thing fallbackItem = null;

            for (int i = inv.Count - 1; i >= 0; i--)
            {
                var item = inv[i];
                if (item == null || item.Destroyed || !_grabbedItems.Contains(item))
                    continue;
                if (!_carrierItems.TryGetValue(item.def, out int remaining) || remaining <= 0)
                    continue;

                fallbackItem = item;
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
                    job.count = Mathf.Min(remaining, item.stackCount);
                    _storageTarget = true;
                    return;
                }
            }

            if (fallbackItem == null)
            {
                BulkLoadDiagnostics.Debug(
                    "carriers",
                    "Drain end pawn=" + pawn.LabelShort + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn)
                    + "] ledger=[" + DescribeLedger() + "] unloadEverything="
                    + pawn.inventory.UnloadEverything);
                if (_carrierItems.Count == 0)
                {
                    BulkLoadDiagnostics.Debug(
                        "carriers",
                        "Carrier drain complete; leaving untracked personal items in inventory.");
                    EndJobWith(JobCondition.Succeeded);
                }
                else
                {
                    BulkLoadDiagnostics.Warning(
                        "carriers",
                        "Tracked carrier items missing from inventory; aborting.");
                    EndJobWith(JobCondition.Incompletable);
                }
                return;
            }

            job.SetTarget(TargetIndex.B, pawn.Position);
            job.SetTarget(TargetIndex.C, fallbackItem);
            job.count = Mathf.Min(_carrierItems[fallbackItem.def], fallbackItem.stackCount);
            _storageTarget = false;
            BulkLoadDiagnostics.Warning(
                "carriers",
                "No storage cell found for " + fallbackItem.def?.defName
                + "; using the standard non-storage drop fallback.");
        }

        protected override int ClampTakeCount(Thing item, int requested)
        {
            if (!_carrierItems.TryGetValue(item.def, out int remaining) || remaining <= 0)
                return 0;
            return Mathf.Min(requested, remaining, item.stackCount);
        }

        protected override void OnPlaced(ThingDef def, int placedCount)
        {
            if (_carrierItems.TryGetValue(def, out int remaining))
            {
                _carrierItems[def] = remaining - placedCount;
                if (_carrierItems[def] <= 0)
                    _carrierItems.Remove(def);
            }
        }

        protected override bool HasMoreWork()
        {
            bool moreTracked = HasTrackedInventoryItems();
            BulkLoadDiagnostics.Debug(
                "carriers",
                "Loop check pawn=" + pawn.LabelShort + " moreTracked=" + moreTracked
                + " ledger=[" + DescribeLedger() + "]");
            return moreTracked;
        }

        private bool HasTrackedInventoryItems()
        {
            var inv = pawn.inventory.innerContainer;
            for (int i = 0; i < inv.Count; i++)
            {
                var item = inv[i];
                if (item != null && !item.Destroyed && _grabbedItems.Contains(item)
                    && _carrierItems.TryGetValue(item.def, out int count) && count > 0)
                    return true;
            }
            return false;
        }

        private bool CarrierHasItems()
        {
            var carrier = Carrier;
            return carrier != null && !carrier.Destroyed && carrier.inventory != null
                && carrier.inventory.innerContainer.Count > 0;
        }

        private string DescribeLedger()
        {
            string summary = "";
            foreach (var entry in _carrierItems)
                summary += entry.Key?.defName + "=" + entry.Value + "; ";
            return summary.Length == 0 ? "-" : summary;
        }

        public override string GetReport()
        {
            if (Carrier != null)
                return "Bulk unloading " + Carrier.LabelShort + ".";
            return "Bulk unloading pack animal.";
        }
    }
}
