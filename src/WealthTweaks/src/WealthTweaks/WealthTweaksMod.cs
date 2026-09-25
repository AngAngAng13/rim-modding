using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace WealthTweaks
{
    public class WealthTweaksMod : Mod
    {
        public static WealthTweaksSettings Settings;
        public static bool VerboseLogging = false;

        public WealthTweaksMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WealthTweaksSettings>();
        }

        public override string SettingsCategory()
        {
            return "Wealth Tweaks";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.ColumnWidth = inRect.width / 2f - 8f;
            listing.Begin(inRect);

            listing.Gap(12f);
            listing.Label("Global Wealth Multiplier");
            listing.Label($"Applied to everything: {Settings.GlobalMultiplier:F2}x");
            Settings.GlobalMultiplier = listing.Slider(Settings.GlobalMultiplier, 0f, 5f);

            listing.Gap(24f);
            if (listing.ButtonText("Open Wealth Editor"))
            {
                Find.WindowStack.Add(new Window_WealthEditor());
            }

            listing.Gap(12f);
            listing.Label("Custom overrides: " + (Settings.Multipliers.Count + Settings.TerrainMultipliers.Count));

            listing.End();
        }
    }

    public class WealthTweaksSettings : ModSettings
    {
        public float GlobalMultiplier = 1f;
        public Dictionary<string, float> Multipliers = new Dictionary<string, float>();
        public Dictionary<string, float> CategoryMultipliers = new Dictionary<string, float>();
        public Dictionary<string, float> TerrainMultipliers = new Dictionary<string, float>();

        public static Dictionary<string, string> DefToTopCategory;
        public static Dictionary<string, List<string>> DefToCategories;

        public static void BuildDefToCategoryMap()
        {
            DefToTopCategory = new Dictionary<string, string>();
            DefToCategories = new Dictionary<string, List<string>>();

            foreach (var topCat in DefDatabase<ThingCategoryDef>.AllDefs.Where(c => c.parent == ThingCategoryDefOf.Root))
            {
                foreach (var category in topCat.ThisAndChildCategoryDefs)
                {
                    foreach (var def in category.childThingDefs)
                    {
                        if (!DefToCategories.TryGetValue(def.defName, out var categories))
                        {
                            categories = new List<string>();
                            DefToCategories[def.defName] = categories;
                        }

                        for (var current = category; current != null && current != ThingCategoryDefOf.Root; current = current.parent)
                        {
                            if (!categories.Contains(current.defName))
                                categories.Add(current.defName);
                        }

                        if (!DefToTopCategory.ContainsKey(def.defName))
                            DefToTopCategory[def.defName] = topCat.defName;
                    }
                }
            }
        }

        public float GetMultiplier(string defName)
        {
            float result = GlobalMultiplier;

            if (DefToCategories != null && DefToCategories.TryGetValue(defName, out var categories))
            {
                foreach (string catDefName in categories)
                {
                    if (CategoryMultipliers.TryGetValue(catDefName, out float catMult))
                        result *= catMult;
                }
            }
            else if (DefToTopCategory != null && DefToTopCategory.TryGetValue(defName, out var topCatDefName))
            {
                if (CategoryMultipliers.TryGetValue(topCatDefName, out float catMult))
                    result *= catMult;
            }

            if (Multipliers.TryGetValue(defName, out float specific))
                result *= specific;

            return result;
        }

        public float GetSpecificMultiplier(string defName)
        {
            if (Multipliers.TryGetValue(defName, out float specific))
                return specific;
            return 1f;
        }

        public void SetSpecificMultiplier(string defName, float mult)
        {
            if (mult == 1f)
                Multipliers.Remove(defName);
            else
                Multipliers[defName] = mult;
        }

        public float GetCategoryMultiplier(string catDefName)
        {
            return CategoryMultipliers.TryGetValue(catDefName, out float m) ? m : 1f;
        }

        public void SetCategoryMultiplier(string catDefName, float mult)
        {
            if (mult == 1f)
                CategoryMultipliers.Remove(catDefName);
            else
                CategoryMultipliers[catDefName] = mult;
        }

        public float GetTerrainMultiplier(string defName)
        {
            if (TerrainMultipliers.TryGetValue(defName, out float specific))
                return specific;
            return 1f;
        }

        public float GetTerrainTotalMultiplier(string defName)
        {
            return GlobalMultiplier * GetTerrainMultiplier(defName);
        }

        public void SetTerrainMultiplier(string defName, float mult)
        {
            if (mult == 1f)
                TerrainMultipliers.Remove(defName);
            else
                TerrainMultipliers[defName] = mult;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref GlobalMultiplier, "globalMultiplier", 1f);
            Scribe_Collections.Look(ref Multipliers, "multipliers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref CategoryMultipliers, "categoryMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref TerrainMultipliers, "terrainMultipliers", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (Multipliers == null)
                    Multipliers = new Dictionary<string, float>();
                if (CategoryMultipliers == null)
                    CategoryMultipliers = new Dictionary<string, float>();
                if (TerrainMultipliers == null)
                    TerrainMultipliers = new Dictionary<string, float>();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class WealthTweaksInitializer
    {
        static WealthTweaksInitializer()
        {
            var harmony = new Harmony("com.wealthtweaks.rimworld.mod");
            harmony.PatchAll();

            WealthTweaksSettings.BuildDefToCategoryMap();
            if (WealthTweaksMod.VerboseLogging)
                Log.Message("[WealthTweaks] Loaded.");
        }
    }
}
