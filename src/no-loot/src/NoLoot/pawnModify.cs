using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;
namespace NoLoot
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new System.Type[] { typeof(PawnGenerationRequest) })]
    static class pawnMod
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result)
        {
            if (!NoLootMod.Settings.modEnabled) return;
            if (__result == null || !__result.RaceProps.Humanlike || __result.Faction == null || __result.Faction.IsPlayer) return;
            if (NoLootMod.Settings.IsPawnExempted(__result)) return;

            BodyPartRecord torso = __result.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Torso).FirstOrDefault();

            if (torso != null && !__result.health.hediffSet.HasHediff(NoLoot.DeathAcidifierDef))
            {
                __result.health.AddHediff(NoLoot.DeathAcidifierDef, torso);
            }

        }

    }
    [HarmonyPatch(typeof(HediffComp_DissolveGearOnDeath), nameof(HediffComp_DissolveGearOnDeath.Notify_PawnKilled))]
    static class afterdDeath
    {
        [HarmonyPostfix]
        static void Postfix(HediffComp_DissolveGearOnDeath __instance)
        {
            if (!NoLootMod.Settings.modEnabled) return;
            if (__instance.Pawn != null && NoLootMod.Settings.IsPawnExempted(__instance.Pawn)) return;

            __instance.Pawn.inventory.DestroyAll();
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
    static class stripOnDown
    {
        [HarmonyPrefix]
        static bool Prefix(Pawn __instance)
        {
            if (!NoLootMod.Settings.modEnabled) return true;
            if (__instance == null || !__instance.RaceProps.Humanlike) return true;
            if (__instance.Faction == null || __instance.Faction.IsPlayer) return true;
            if (NoLootMod.Settings.IsPawnExempted(__instance)) return true;

            __instance.inventory.DestroyAll();
            __instance.equipment.DestroyAllEquipment();
            __instance.apparel.DestroyAll();

            return false;
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    static class afterRecurit
    {
        [HarmonyPostfix]
        static void Postfix(Pawn __instance, Faction newFaction)
        {
            if (newFaction != Faction.OfPlayer || !__instance.RaceProps.Humanlike) return;
            Hediff torso = __instance.health.hediffSet.GetFirstHediffOfDef(NoLoot.DeathAcidifierDef);
            if (torso != null)
            {
                __instance.health.RemoveHediff(torso);
            }
        }
    }
}
