using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace BulkLoad
{
    public abstract class JobDriver_DrainToStorageBase : JobDriver
    {
        protected ThingDef _activeDef;
        protected int _activeBeforeCount;
        protected bool _storageTarget;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref _activeDef, "activeDef");
            Scribe_Values.Look(ref _activeBeforeCount, "activeBeforeCount", 0);
            Scribe_Values.Look(ref _storageTarget, "storageTarget", false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected virtual bool ReserveDestinationCell => true;
        protected virtual string DumpActionKind => "dump";
        protected virtual string UnplacedItemNoun => "item";

        protected abstract void FindNextItem();
        protected abstract bool HasMoreWork();

        protected virtual int ClampTakeCount(Thing item, int requested)
        {
            return UnityEngine.Mathf.Min(requested, item.stackCount);
        }

        protected virtual void OnPlaced(ThingDef def, int placedCount)
        {
        }

        protected virtual IEnumerable<Toil> PreDrainToils()
        {
            yield break;
        }

        protected virtual IEnumerable<Toil> PostDrainToils()
        {
            yield break;
        }

        protected virtual void OnJobFinished(JobCondition jobCondition)
        {
            BulkLoadDiagnostics.Debug(
                "carriers",
                "Finished job=" + job.def?.defName
                + " condition=" + jobCondition
                + " inv=[" + BulkLoadDiagnostics.DescribeInventory(pawn) + "]");
        }

        protected override sealed IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(OnJobFinished);

            foreach (var toil in PreDrainToils())
                yield return toil;

            var findItem = new Toil
            {
                initAction = FindNextItem,
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return findItem;
            yield return MakeTakeItemToil();
            if (ReserveDestinationCell)
                yield return Toils_Reserve.Reserve(TargetIndex.B);
            var gotoDestination = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
            yield return gotoDestination;
            yield return Toils_Haul.PlaceHauledThingInCell(
                TargetIndex.B,
                gotoDestination,
                storageMode: true);
            yield return MakeFinishPlacementToil();
            yield return Toils_Jump.JumpIf(findItem, HasMoreWork);

            foreach (var toil in PostDrainToils())
                yield return toil;
        }

        private Toil MakeTakeItemToil()
        {
            return new Toil
            {
                initAction = () =>
                {
                    var held = pawn.carryTracker.CarriedThing;
                    if (held != null)
                    {
                        if (!pawn.carryTracker.innerContainer.TryTransferToContainer(
                            held,
                            pawn.inventory.innerContainer))
                        {
                            pawn.carryTracker.TryDropCarriedThing(
                                pawn.Position,
                                ThingPlaceMode.Near,
                                out _);
                        }
                    }

                    var item = job.GetTarget(TargetIndex.C).Thing;
                    if (item == null || item.Destroyed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    int amount = ClampTakeCount(item, job.count);
                    if (amount <= 0)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    Thing toMove = item;
                    bool moved;
                    if (amount < item.stackCount)
                    {
                        toMove = item.SplitOff(amount);
                        moved = pawn.carryTracker.GetDirectlyHeldThings().TryAdd(toMove);
                    }
                    else
                    {
                        moved = pawn.inventory.innerContainer.TryTransferToContainer(
                            toMove,
                            pawn.carryTracker.GetDirectlyHeldThings());
                    }

                    if (!moved)
                    {
                        BulkLoadDiagnostics.Warning(
                            "carriers",
                            "Could not move " + toMove.def?.defName + " into the pawn's hands.");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    _activeDef = toMove.def;
                    _activeBeforeCount = pawn.carryTracker.CarriedThing?.stackCount ?? 0;
                    BulkLoadDiagnostics.Debug(
                        "carriers",
                        "Hands holding " + _activeBeforeCount + "x " + _activeDef?.defName
                        + " pawn=" + pawn.LabelShort);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }

        private Toil MakeFinishPlacementToil()
        {
            return new Toil
            {
                initAction = () =>
                {
                    Thing remainder = pawn.carryTracker.CarriedThing;
                    int remainderCount = remainder?.stackCount ?? 0;
                    int placedCount = _activeBeforeCount - remainderCount;

                    if (placedCount > 0 && _activeDef != null)
                    {
                        BulkLoadDiagnostics.Debug(
                            "carriers",
                            "Placed " + placedCount + "x " + _activeDef.defName
                            + (_storageTarget ? " through storage." : " with the non-storage fallback."));
                        BulkLoadDiagnostics.RecordModAction(
                            pawn,
                            DumpActionKind,
                            "def=" + _activeDef.defName + " count=" + placedCount
                            + " storage=" + _storageTarget);
                        OnPlaced(_activeDef, placedCount);
                    }

                    if (remainder == null)
                        return;

                    bool returned = pawn.carryTracker.innerContainer.TryTransferToContainer(
                        remainder,
                        pawn.inventory.GetDirectlyHeldThings());
                    if (!returned)
                    {
                        BulkLoadDiagnostics.Warning(
                            "carriers",
                            "Could not return an unplaced " + UnplacedItemNoun + " to the pawn inventory.");
                        pawn.carryTracker.TryDropCarriedThing(
                            pawn.Position,
                            ThingPlaceMode.Near,
                            out _);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (placedCount <= 0)
                    {
                        BulkLoadDiagnostics.Warning(
                            "carriers",
                            "Storage placement made no progress for " + _activeDef?.defName + "; leaving it for vanilla hauling.");
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}
