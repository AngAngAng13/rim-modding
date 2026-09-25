using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BulkLoad
{
    internal interface IPickupPolicy
    {
        void Track(Dictionary<ThingDef, int> pickedUp, ThingDef def, int count);
        bool IsTracked(Dictionary<ThingDef, int> pickedUp, ThingDef def);
        bool HasTrackedItems(Dictionary<ThingDef, int> pickedUp, Pawn pawn);
        void DropTrackedItems(Dictionary<ThingDef, int> pickedUp, Pawn pawn);
    }

    internal sealed class PickupPolicy : IPickupPolicy
    {
        public void Track(Dictionary<ThingDef, int> pickedUp, ThingDef def, int count)
        {
            pickedUp[def] = pickedUp.TryGetValue(def) + count;
        }

        public bool IsTracked(Dictionary<ThingDef, int> pickedUp, ThingDef def)
        {
            return pickedUp.ContainsKey(def);
        }

        public bool HasTrackedItems(Dictionary<ThingDef, int> pickedUp, Pawn pawn)
        {
            var inv = pawn.inventory.innerContainer;
            for (int i = 0; i < inv.Count; i++)
            {
                if (pickedUp.ContainsKey(inv[i].def))
                    return true;
            }
            return false;
        }

        public void DropTrackedItems(Dictionary<ThingDef, int> pickedUp, Pawn pawn)
        {
            if (pickedUp.Count == 0)
                return;
            if (pawn.Map == null || !pawn.Spawned)
            {
                BulkLoadDiagnostics.Warning(
                    "cleanup",
                    "Could not drop tracked items because the pawn was not spawned on a map.");
                return;
            }

            BulkLoadDiagnostics.Debug(
                "cleanup",
                "Dropping tracked items after interrupted job defs=" + pickedUp.Count);

            var inv = pawn.inventory.innerContainer;
            for (int i = inv.Count - 1; i >= 0; i--)
            {
                var item = inv[i];
                if (!pickedUp.ContainsKey(item.def))
                    continue;

                int remaining = pickedUp[item.def];
                int dropCount = remaining < item.stackCount ? remaining : item.stackCount;
                if (dropCount <= 0)
                    continue;

                Thing toDrop = item;
                if (dropCount < item.stackCount)
                    toDrop = item.SplitOff(dropCount);

                IntVec3 targetCell = pawn.Position;
                bool foundStorage = StoreUtility.TryFindBestBetterStoreCellFor(
                    toDrop,
                    pawn,
                    pawn.Map,
                    StoragePriority.Unstored,
                    pawn.Faction,
                    out IntVec3 storeCell);
                if (foundStorage)
                    targetCell = storeCell;

                bool returned;
                if (toDrop == item)
                    returned = inv.TryDrop(toDrop, targetCell, pawn.Map, ThingPlaceMode.Near, out _);
                else
                    returned = GenPlace.TryPlaceThing(toDrop, targetCell, pawn.Map, ThingPlaceMode.Near);

                if (returned)
                {
                    pickedUp[item.def] -= dropCount;
                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "cleanup",
                        "def=" + item.def?.defName
                        + " count=" + dropCount
                        + " state=returned_after_failure"
                        + " storage=" + foundStorage
                        + " cell=" + targetCell);
                    BulkLoadDiagnostics.Debug(
                        "cleanup",
                        "Returned " + dropCount + "x " + item.def?.defName + " by " + pawn.LabelShort
                        + (foundStorage ? " to storage at " + targetCell + "." : " near pawn at " + targetCell + " because no storage was available."));
                }
                else
                {
                    if (toDrop != item && inv.TryAdd(toDrop, true))
                    {
                        pickedUp[item.def] -= dropCount;
                        BulkLoadDiagnostics.Warning(
                            "cleanup",
                            "Could not place " + dropCount + "x " + item.def?.defName
                            + "; returned it to the pawn inventory.");
                        continue;
                    }

                    BulkLoadDiagnostics.RecordModAction(
                        pawn,
                        "cleanup_failed",
                        "def=" + item.def?.defName
                        + " count=" + dropCount
                        + " state=unresolved_after_failure");
                    BulkLoadDiagnostics.Warning(
                        "cleanup",
                        "Could not return " + dropCount + "x " + item.def?.defName + " after interrupted job.");
                }
            }
            pickedUp.Clear();
        }
    }
}
