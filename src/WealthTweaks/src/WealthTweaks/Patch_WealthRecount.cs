using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WealthTweaks
{
    [HarmonyPatch(typeof(WealthWatcher), nameof(WealthWatcher.ForceRecount))]
    public static class Patch_WealthRecount
    {
        private static List<Thing> tmpThings = new List<Thing>();

        private static readonly FieldInfo f_wealthItems = AccessTools.Field(typeof(WealthWatcher), "wealthItems");
        private static readonly FieldInfo f_wealthBuildings = AccessTools.Field(typeof(WealthWatcher), "wealthBuildings");
        private static readonly FieldInfo f_wealthPawns = AccessTools.Field(typeof(WealthWatcher), "wealthPawns");
        private static readonly FieldInfo f_wealthFloorsOnly = AccessTools.Field(typeof(WealthWatcher), "wealthFloorsOnly");
        private static readonly FieldInfo f_cachedTerrainMarketValue = AccessTools.Field(typeof(WealthWatcher), "cachedTerrainMarketValue");
        private static readonly FieldInfo f_map = AccessTools.Field(typeof(WealthWatcher), "map");

        static void Postfix(WealthWatcher __instance)
        {
            if (f_map == null || f_wealthItems == null)
            {
                Log.ErrorOnce("[WealthTweaks] Failed to reflect WealthWatcher fields", 84123);
                return;
            }

            Map map = (Map)f_map.GetValue(__instance);
            if (map == null)
                return;

            var s = WealthTweaksMod.Settings;
            if (s == null)
                return;

            if (s.GlobalMultiplier == 1f && s.CategoryMultipliers.Count == 0 && s.Multipliers.Count == 0 && s.TerrainMultipliers.Count == 0)
                return;

            float itemsDelta = 0f;
            float buildingsDelta = 0f;
            float pawnsDelta = 0f;

            tmpThings.Clear();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), tmpThings, false, WealthWatcher.WealthItemsFilter);
            for (int i = 0; i < tmpThings.Count; i++)
            {
                Thing thing = tmpThings[i];
                if (thing.SpawnedOrAnyParentSpawned && !thing.PositionHeld.Fogged(map))
                {
                    float mult = s.GetMultiplier(thing.def.defName);
                    if (mult != 1f)
                        itemsDelta += thing.MarketValue * thing.stackCount * (mult - 1f);
                }
            }
            tmpThings.Clear();

            List<Thing> buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            for (int i = 0; i < buildings.Count; i++)
            {
                Thing thing = buildings[i];
                if (thing.Faction == Faction.OfPlayer)
                {
                    float mult = s.GetMultiplier(thing.def.defName);
                    if (mult != 1f)
                        buildingsDelta += thing.GetStatValue(StatDefOf.MarketValueIgnoreHp) * (mult - 1f);
                }
            }

            foreach (Pawn pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (!pawn.IsQuestLodger())
                {
                    float mult = s.GetMultiplier(pawn.def.defName);
                    if (mult != 1f)
                    {
                        float val = pawn.MarketValue;
                        if (pawn.IsSlave)
                            val *= 0.75f;
                        pawnsDelta += val * (mult - 1f);
                    }
                }
            }

            float oldItems = (float)f_wealthItems.GetValue(__instance);
            float oldBuildings = (float)f_wealthBuildings.GetValue(__instance);
            float oldPawns = (float)f_wealthPawns.GetValue(__instance);

            float floorsDelta = 0f;
            float oldFloors = 0f;
            if ((s.GlobalMultiplier != 1f || s.TerrainMultipliers.Count > 0) && f_wealthFloorsOnly != null)
            {
                oldFloors = (float)f_wealthFloorsOnly.GetValue(__instance);
                floorsDelta = ComputeFloorsDelta(map, s);
                f_wealthFloorsOnly.SetValue(__instance, oldFloors + floorsDelta);
            }

            f_wealthItems.SetValue(__instance, oldItems + itemsDelta);
            f_wealthBuildings.SetValue(__instance, oldBuildings + buildingsDelta + floorsDelta);
            f_wealthPawns.SetValue(__instance, oldPawns + pawnsDelta);

            if (WealthTweaksMod.VerboseLogging)
                Log.Message($"[WealthTweaks] Wealth recount patch: items {oldItems:F0} -> {oldItems + itemsDelta:F0} (delta {itemsDelta:F0}), buildings delta {buildingsDelta:F0}, floors delta {floorsDelta:F0}, pawns delta {pawnsDelta:F0}");
        }

        private static float ComputeFloorsDelta(Map map, WealthTweaksSettings s)
        {
            TerrainDef[] topGrid = map.terrainGrid.topGrid;
            TerrainDef[] foundationGrid = map.terrainGrid.foundationGrid;
            if (topGrid == null)
                return 0f;

            float[] cached = f_cachedTerrainMarketValue != null
                ? (float[])f_cachedTerrainMarketValue.GetValue(null)
                : null;

            int count = map.Size.x * map.Size.z;
            float delta = 0f;
            for (int i = 0; i < count; i++)
            {
                TerrainDef top = topGrid[i];
                if (top != null && !map.fogGrid.IsFogged(map.cellIndices.IndexToCell(i)))
                    delta += TerrainDelta(top, cached, s);

                if (foundationGrid != null)
                {
                    TerrainDef foundation = foundationGrid[i];
                    if (foundation != null)
                        delta += TerrainDelta(foundation, cached, s);
                }
            }
            return delta;
        }

        private static float TerrainDelta(TerrainDef terrain, float[] cached, WealthTweaksSettings s)
        {
            float mult = s.GetTerrainTotalMultiplier(terrain.defName);
            if (mult == 1f)
                return 0f;

            float baseVal;
            if (cached != null && terrain.index >= 0 && terrain.index < cached.Length)
                baseVal = cached[terrain.index];
            else
                baseVal = terrain.GetStatValueAbstract(StatDefOf.MarketValue);

            if (baseVal <= 0f)
                return 0f;
            return baseVal * (mult - 1f);
        }
    }
}
