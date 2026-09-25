using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public class JobDriver_BulkDoBill : JobDriver_DoBill
    {
        private Dictionary<ThingDef, int> _pickedUp = new Dictionary<ThingDef, int>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _pickedUp, "billPickedUp", LookMode.Def, LookMode.Value);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(jobCondition =>
            {
                BulkLoadDiagnostics.Debug(
                    "bills",
                    "Finished bulk bill condition=" + jobCondition + " trackedDefs=" + _pickedUp.Count);
                BulkLoadDiagnostics.RecordModAction(
                    pawn,
                    "bill_finished",
                    "condition=" + jobCondition + " trackedDefs=" + _pickedUp.Count);
                if (jobCondition == JobCondition.Succeeded)
                    return;
                BulkLoadServices.Pickups.DropTrackedItems(_pickedUp, pawn);
            });


            AddEndCondition(delegate
            {
                Thing thing = GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                return (!(thing is Building) || thing.Spawned) ? JobCondition.Ongoing : JobCondition.Incompletable;
            });
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(delegate
            {
                if (job.GetTarget(TargetIndex.A).Thing is IBillGiver billGiver)
                {
                    if (job.bill.DeletedOrDereferenced)
                        return true;
                    if (!billGiver.CurrentlyUsableForBills())
                        return true;
                }
                return false;
            });

            var gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil startToil = ToilMaker.MakeToil();
            startToil.initAction = delegate
            {
                if (job.targetQueueB != null && job.targetQueueB.Count == 1 && job.targetQueueB[0].Thing is UnfinishedThing { Destroyed: false } uft)
                {
                    uft.BoundBill = (Bill_ProductionWithUft)job.bill;
                }
                job.bill.Notify_DoBillStarted(pawn);
                BulkLoadDiagnostics.Debug(
                    "bills",
                    "Started bulk bill at=" + job.GetTarget(TargetIndex.A).Thing?.def?.defName
                    + " ingredients=" + (job.targetQueueB?.Count ?? 0));
                BulkLoadDiagnostics.RecordModAction(
                    pawn,
                    "bill_started",
                    "ingredients=" + (job.targetQueueB?.Count ?? 0));
            };
            yield return startToil;

            yield return Toils_Jump.JumpIf(gotoBillGiver, () => job.GetTargetQueue(TargetIndex.B).NullOrEmpty());

            foreach (var toil in GatherIngredientsToInventory())
                yield return toil;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            yield return Toils_Jump.JumpIf(gotoBillGiver, () =>
                !BulkLoadServices.Pickups.HasTrackedItems(_pickedUp, pawn));

            foreach (var toil in DepositIngredientsFromInventory())
                yield return toil;

            var validatePlaced = new Toil
            {
                initAction = () =>
                {
                    if (_pickedUp.Count == 0)
                        return;

                    var placedDefs = new HashSet<ThingDef>();
                    if (job.placedThings != null)
                    {
                        foreach (var placed in job.placedThings)
                        {
                            if (placed.Count > 0 && placed.thing != null)
                                placedDefs.Add(placed.thing.def);
                        }
                    }

                    foreach (var def in _pickedUp.Keys)
                    {
                        if (!placedDefs.Contains(def))
                        {
                            BulkLoadDiagnostics.Error(
                                "bills",
                                "Ingredient was picked up but not registered as placed: " + def?.defName);
                            EndJobWith(JobCondition.Incompletable);
                            return;
                        }
                    }
                }
            };
            yield return validatePlaced;

            yield return gotoBillGiver;
            yield return Toils_Recipe.MakeUnfinishedThingIfNeeded();
            yield return Toils_Recipe.DoRecipeWork().FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Recipe.CheckIfRecipeCanFinishNow();
            yield return Toils_Recipe.FinishRecipeAndStartStoringProduct(TargetIndex.None);
        }

        private IEnumerable<Toil> GatherIngredientsToInventory()
        {
            var extractNext = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B);

            var gotoIngredient = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            gotoIngredient.FailOnDespawnedNullOrForbidden(TargetIndex.B);

            var takeIngredient = new Toil
            {
                initAction = () =>
                {
                    var thing = job.targetB.Thing;
                    if (thing == null || !thing.Spawned)
                        return;

                    if (thing is UnfinishedThing)
                        return;

                    var bench = job.GetTarget(TargetIndex.A).Thing;
                    if (bench != null)
                    {
                        var owner = bench.TryGetInnerInteractableThingOwner();
                        if (owner != null && owner.Contains(thing))
                            return;
                    }

                    int take = Mathf.Min(job.count, thing.stackCount);
                    if (take <= 0)
                    {
                        BulkLoadDiagnostics.Debug(
                            "bills",
                            "Skipped ingredient=" + thing.def?.defName + " count=" + job.count);
                        return;
                    }

                    var split = thing.SplitOff(take);
                    pawn.inventory.GetDirectlyHeldThings().TryAdd(split, true);
                    BulkLoadServices.Pickups.Track(_pickedUp, split.def, take);
                    BulkLoadDiagnostics.Debug(
                        "bills",
                        "Picked up ingredient=" + split.def?.defName + " count=" + take);
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "bill_pickup",
                        "def=" + split.def?.defName + " count=" + take);
                }
            };

            yield return extractNext;
            yield return gotoIngredient;
            yield return takeIngredient;
            yield return Toils_Jump.JumpIf(extractNext, () => !job.targetQueueB.NullOrEmpty());
        }

        private IEnumerable<Toil> DepositIngredientsFromInventory()
        {
            var moveToHands = new Toil
            {
                initAction = () =>
                {
                    var inv = pawn.inventory.innerContainer;

                    Thing item = null;
                    for (int i = 0; i < inv.Count; i++)
                    {
                        if (BulkLoadServices.Pickups.IsTracked(_pickedUp, inv[i].def))
                        {
                            item = inv[i];
                            break;
                        }
                    }

                    if (item == null)
                    {
                        BulkLoadDiagnostics.Warning(
                            "bills",
                            "Could not move tracked ingredient into hands before deposit.");
                        return;
                    }

                    inv.TryTransferToContainer(item, pawn.carryTracker.GetDirectlyHeldThings());
                    job.SetTarget(TargetIndex.B, pawn.carryTracker.CarriedThing);
                }
            };

            var findPlaceCell = Toils_JobTransforms.SetTargetToIngredientPlaceCell(TargetIndex.A, TargetIndex.B, TargetIndex.C);

            var placeAndRegister = new Toil
            {
                initAction = delegate
                {
                    Thing carried = pawn.carryTracker.CarriedThing;
                    if (carried == null)
                        return;

                    IntVec3 cell = job.GetTarget(TargetIndex.C).Cell;
                    int placedCount = carried.stackCount;

                    if (pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out Thing placed))
                    {
                        Thing resolvedPlaced = ResolvePlacedThing(carried, placed, cell);
                        if (resolvedPlaced == null)
                        {
                            BulkLoadDiagnostics.Error(
                                "bills",
                                "Ingredient was dropped but the placed stack could not be resolved: " + carried.def?.defName);
                            EndJobWith(JobCondition.Incompletable);
                            return;
                        }

                        HaulAIUtility.UpdateJobWithPlacedThings(job, resolvedPlaced, placedCount);
                        pawn.Reserve(resolvedPlaced, job, errorOnFailed: false);
                        BulkLoadDiagnostics.Debug(
                            "bills",
                            "Placed ingredient=" + resolvedPlaced.def?.defName + " count=" + placedCount + " at=" + cell);
                        BulkLoadDiagnostics.RecordModAction(
                            pawn,
                            "bill_deposit",
                            "def=" + resolvedPlaced.def?.defName + " count=" + placedCount + " cell=" + cell);
                    }
                    else
                    {
                        BulkLoadDiagnostics.Warning(
                            "bills",
                            "Could not place carried ingredient at=" + cell);
                        pawn.jobs.curDriver.JumpToToil(findPlaceCell);
                    }
                }
            };

            var checkMore = new Toil
            {
                initAction = () =>
                {
                    var inv = pawn.inventory.innerContainer;
                    for (int i = 0; i < inv.Count; i++)
                    {
                        if (BulkLoadServices.Pickups.IsTracked(_pickedUp, inv[i].def))
                        {
                            pawn.jobs.curDriver.JumpToToil(moveToHands);
                            return;
                        }
                    }
                }
            };

            yield return moveToHands;
            yield return findPlaceCell;
            yield return placeAndRegister;
            yield return checkMore;
        }

        private Thing ResolvePlacedThing(Thing carried, Thing placed, IntVec3 cell)
        {
            if (placed != null)
                return placed;

            if (pawn.Map != null && cell.InBounds(pawn.Map))
            {
                var things = cell.GetThingList(pawn.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def == carried.def)
                        return things[i];
                }
            }

            var owner = job.GetTarget(TargetIndex.A).Thing?.TryGetInnerInteractableThingOwner();
            if (owner != null)
            {
                foreach (Thing thing in owner)
                {
                    if (thing.def == carried.def)
                        return thing;
                }
            }

            if (carried != null && !carried.Destroyed)
                return carried;

            return null;
        }
    }
}
