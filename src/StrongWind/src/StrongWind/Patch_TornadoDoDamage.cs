using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace StrongWind
{
    [HarmonyPatch(typeof(Tornado), "DoDamage")]
    public static class Patch_TornadoDoDamage
    {
        public static void Postfix(Tornado __instance, IntVec3 c)
        {
            StrongWindSettings s = StrongWindMod.Settings;
            if (!s.enabled)
            {
                return;
            }
            Map map = __instance.Map;
            if (map == null || !c.InBounds(map))
            {
                return;
            }
            Thing target = null;
            if (s.swirlPawns)
            {
                Pawn pawn = c.GetFirstPawn(map);
                if (pawn != null && pawn.Spawned)
                {
                    target = pawn;
                }
            }
            if (target == null && s.swirlItems)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def.category == ThingCategory.Item && things[i].Spawned)
                    {
                        target = things[i];
                        break;
                    }
                }
            }
            if (target == null)
            {
                return;
            }
            if (SwirlFlyer.ActiveCount(map) >= s.maxActiveSwirls)
            {
                return;
            }
            if (!Rand.Chance(s.grabChance))
            {
                return;
            }
            IntVec3 spawnCell = target.Position;
            SwirlFlyer flyer = SwirlFlyer.MakeSwirl(target, __instance);
            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, spawnCell, map);
            }
        }
    }
}
