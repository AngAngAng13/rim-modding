using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace WealthTweaks
{
    public class Window_WealthEditor : Window
    {
        private class SidebarEntry
        {
            public ThingCategoryDef category;
            public int depth;
            public List<ThingDef> items;
            public bool hasChildren;
        }

        private static List<ThingDef> allEditableDefs;
        private static List<TerrainDef> allEditableTerrains;
        private static List<SidebarEntry> sidebarEntries;
        private static Dictionary<ThingCategoryDef, List<ThingDef>> categoryItemMap;
        private static Dictionary<ThingDef, float> baseValueCache;
        private static Dictionary<TerrainDef, float> baseTerrainValueCache;
        private static HashSet<ThingCategoryDef> expandedCategories;
        private static List<string> modFilterOptions;

        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private string searchQuery = "";
        private ThingCategoryDef selectedCategory;
        private bool showingFloors;
        private string selectedModFilter;

        private List<ThingDef> visibleDefs;
        private List<TerrainDef> visibleTerrainDefs;
        private string lastSearchQuery = "__init__";
        private ThingCategoryDef lastCategory;
        private bool lastShowingFloors;
        private string lastModFilter = "__init__";

        private const string AllModFilter = "All mods";
        private const string CoreModFilter = "Core game";
        private const float RowHeight = 42f;
        private const float CatRowHeight = 28f;
        private const float LeftPaneWidth = 260f;
        private const float IndentPerDepth = 18f;

        public override Vector2 InitialSize => new Vector2(1150f, 700f);

        public Window_WealthEditor()
        {
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            closeOnClickedOutside = true;
            doCloseX = true;

            BuildCategoryMap();
        }

        private static void BuildCategoryMap()
        {
            if (sidebarEntries != null)
                return;

            allEditableDefs = new List<ThingDef>();
            allEditableTerrains = new List<TerrainDef>();
            baseValueCache = new Dictionary<ThingDef, float>();
            baseTerrainValueCache = new Dictionary<TerrainDef, float>();
            sidebarEntries = new List<SidebarEntry>();
            categoryItemMap = new Dictionary<ThingCategoryDef, List<ThingDef>>();
            expandedCategories = new HashSet<ThingCategoryDef>();
            modFilterOptions = new List<string> { AllModFilter };

            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                float mv = def.GetStatValueAbstract(StatDefOf.MarketValue);
                if (mv <= 0f)
                    continue;

                allEditableDefs.Add(def);
                baseValueCache[def] = mv;
            }

            foreach (var terrain in DefDatabase<TerrainDef>.AllDefs)
            {
                float mv = terrain.GetStatValueAbstract(StatDefOf.MarketValue);
                if (mv <= 0f)
                    continue;

                allEditableTerrains.Add(terrain);
                baseTerrainValueCache[terrain] = mv;
            }

            modFilterOptions.AddRange(allEditableDefs
                .Select(GetModLabel)
                .Concat(allEditableTerrains.Select(GetModLabel))
                .Where(label => label != AllModFilter)
                .Distinct()
                .OrderBy(label => label));

            var topLevelCats = DefDatabase<ThingCategoryDef>.AllDefs
                .Where(c => c.parent == ThingCategoryDefOf.Root)
                .OrderBy(c => c.label);

            foreach (var topCat in topLevelCats)
                AddCategoryEntries(topCat, 0);
        }

        private static void AddCategoryEntries(ThingCategoryDef category, int depth)
        {
            var items = category.DescendantThingDefs
                .Where(def => baseValueCache.ContainsKey(def))
                .Distinct()
                .ToList();

            if (items.Count > 0)
            {
                sidebarEntries.Add(new SidebarEntry
                {
                    category = category,
                    depth = depth,
                    items = items,
                    hasChildren = category.childCategories.Any(child => child.DescendantThingDefs.Any(def => baseValueCache.ContainsKey(def)))
                });
                categoryItemMap[category] = items;
                expandedCategories.Add(category);
            }

            foreach (var childCategory in category.childCategories.OrderBy(c => c.label))
                AddCategoryEntries(childCategory, depth + 1);
        }

        private static string GetModLabel(Def def)
        {
            return def.modContentPack == null || def.modContentPack.IsCoreMod
                ? CoreModFilter
                : def.modContentPack.Name;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, 300f, 30f), "Wealth Editor");
            Text.Font = GameFont.Small;

            float topHeight = 36f;
            Rect searchRect = new Rect(inRect.x, inRect.y + topHeight, 200f, 24f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            Rect resetRect = new Rect(inRect.x + 210f, inRect.y + topHeight, 100f, 24f);
            if (Widgets.ButtonText(resetRect, "Reset All"))
            {
                WealthTweaksMod.Settings.GlobalMultiplier = 1f;
                WealthTweaksMod.Settings.CategoryMultipliers.Clear();
                WealthTweaksMod.Settings.Multipliers.Clear();
                WealthTweaksMod.Settings.TerrainMultipliers.Clear();
                WealthTweaksMod.Settings.Write();
            }

            Rect countRect = new Rect(inRect.x + 320f, inRect.y + topHeight, 200f, 24f);
            Widgets.Label(countRect, (WealthTweaksMod.Settings.Multipliers.Count + WealthTweaksMod.Settings.TerrainMultipliers.Count) + " custom overrides");

            Rect modFilterRect = new Rect(inRect.x + 530f, inRect.y + topHeight, 260f, 24f);
            Widgets.Dropdown(
                modFilterRect,
                this,
                window => window.selectedModFilter,
                window => window.GenerateModFilterOptions(),
                ("Source: " + (selectedModFilter ?? AllModFilter)).Truncate(250f));

            float contentY = inRect.y + topHeight + 28f;
            float contentHeight = inRect.height - (topHeight + 28f) - 4f;

            Rect leftRect = new Rect(inRect.x, contentY, LeftPaneWidth, contentHeight);
            DrawCategoryList(leftRect);

            Rect rightRect = new Rect(leftRect.xMax + 4f, contentY, inRect.width - LeftPaneWidth - 8f, contentHeight);
            DrawItemList(rightRect);
        }

        private void DrawCategoryList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 1f));

            List<SidebarEntry> visibleEntries = GetVisibleSidebarEntries();
            int totalRows = visibleEntries.Count + 2;
            float contentH = totalRows * CatRowHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentH);

            Widgets.BeginScrollView(rect, ref leftScroll, viewRect);

            int firstVisible = Mathf.FloorToInt(leftScroll.y / CatRowHeight);
            int lastVisible = Mathf.Min(firstVisible + Mathf.CeilToInt(rect.height / CatRowHeight) + 1, totalRows);

            for (int i = firstVisible; i < lastVisible; i++)
            {
                float y = i * CatRowHeight;

                if (i == 0)
                {
                    Rect allRect = new Rect(2f, y, viewRect.width - 4f, CatRowHeight - 2f);
                    if (selectedCategory == null && !showingFloors)
                        Widgets.DrawHighlight(allRect);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(allRect, "  All (" + GetFilteredCount(allEditableDefs) + ")");
                    if (Widgets.ButtonInvisible(allRect))
                    {
                        selectedCategory = null;
                        showingFloors = false;
                        rightScroll = Vector2.zero;
                    }
                    continue;
                }

                if (i == 1)
                {
                    Rect floorsRect = new Rect(2f, y, viewRect.width - 4f, CatRowHeight - 2f);
                    if (showingFloors)
                        Widgets.DrawHighlight(floorsRect);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(floorsRect, "  Floors / Terrain (" + GetFilteredCount(allEditableTerrains) + ")");
                    if (Widgets.ButtonInvisible(floorsRect))
                    {
                        selectedCategory = null;
                        showingFloors = true;
                        rightScroll = Vector2.zero;
                    }
                    continue;
                }

                var entry = visibleEntries[i - 2];
                float indent = entry.depth * IndentPerDepth;
                Rect rowRect = new Rect(2f + indent, y, viewRect.width - 4f - indent, CatRowHeight - 2f);

                if (selectedCategory == entry.category)
                    Widgets.DrawHighlight(rowRect);

                DrawTreeGuides(rowRect, visibleEntries, i - 2, entry.depth);

                bool hasVisibleChildren = entry.hasChildren && entry.category.childCategories.Any(child =>
                    categoryItemMap.TryGetValue(child, out var childItems) && childItems.Any(DefMatchesModFilter));
                float labelX = rowRect.x + 20f;

                if (hasVisibleChildren)
                {
                    Rect toggleRect = new Rect(rowRect.x, rowRect.y + (rowRect.height - 18f) / 2f, 18f, 18f);
                    bool isExpanded = expandedCategories.Contains(entry.category);
                    if (Widgets.ButtonImage(toggleRect, isExpanded ? TexButton.Minus : TexButton.Plus))
                    {
                        if (isExpanded)
                        {
                            expandedCategories.Remove(entry.category);
                            SoundDefOf.TabClose.PlayOneShotOnCamera();
                        }
                        else
                        {
                            expandedCategories.Add(entry.category);
                            SoundDefOf.TabOpen.PlayOneShotOnCamera();
                        }
                    }
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                string label = entry.category.label.CapitalizeFirst() + " (" + GetFilteredCount(entry.items) + ")";
                Rect labelRect = new Rect(labelX, rowRect.y, rowRect.xMax - labelX - 2f, rowRect.height);
                Widgets.Label(labelRect, label.Truncate(labelRect.width));

                if (Widgets.ButtonInvisible(labelRect))
                {
                    selectedCategory = entry.category;
                    showingFloors = false;
                    rightScroll = Vector2.zero;
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndScrollView();
        }

        private static void DrawTreeGuides(Rect rowRect, List<SidebarEntry> entries, int entryIndex, int depth)
        {
            if (depth <= 0)
                return;

            Color guideColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            float rowCenter = rowRect.center.y;
            float previousCenter = rowCenter - CatRowHeight;

            for (int level = 0; level < depth; level++)
            {
                float guideX = 2f + level * IndentPerDepth + 9f;
                float endY = HasContinuationAtDepth(entries, entryIndex, level)
                    ? rowCenter + CatRowHeight / 2f
                    : rowCenter;
                DrawSolidVerticalLine(guideX, previousCenter, endY, guideColor);
            }

            float parentX = 2f + (depth - 1) * IndentPerDepth + 9f;
            float nodeX = 2f + depth * IndentPerDepth;
            DrawSolidHorizontalLine(parentX, nodeX, rowCenter, guideColor);
        }

        private static void DrawSolidVerticalLine(float x, float startY, float endY, Color color)
        {
            if (endY <= startY)
                return;
            Widgets.DrawBoxSolid(new Rect(Mathf.Floor(x), Mathf.Floor(startY), 1f, Mathf.Ceil(endY - startY)), color);
        }

        private static void DrawSolidHorizontalLine(float startX, float endX, float y, Color color)
        {
            if (endX <= startX)
                return;
            Widgets.DrawBoxSolid(new Rect(Mathf.Floor(startX), Mathf.Floor(y), Mathf.Ceil(endX - startX), 1f), color);
        }

        private static bool HasContinuationAtDepth(List<SidebarEntry> entries, int entryIndex, int depth)
        {
            int nextIndex = entryIndex + 1;
            return nextIndex < entries.Count && entries[nextIndex].depth > depth;
        }

        private List<SidebarEntry> GetVisibleSidebarEntries()
        {
            var result = new List<SidebarEntry>();
            foreach (SidebarEntry entry in sidebarEntries)
            {
                if (!entry.items.Any(DefMatchesModFilter) || !IsCategoryVisible(entry.category))
                    continue;
                result.Add(entry);
            }
            return result;
        }

        private static bool IsCategoryVisible(ThingCategoryDef category)
        {
            for (ThingCategoryDef parent = category.parent; parent != null && parent != ThingCategoryDefOf.Root; parent = parent.parent)
            {
                if (!expandedCategories.Contains(parent))
                    return false;
            }
            return true;
        }

        private bool DefMatchesModFilter(Def def)
        {
            return selectedModFilter == null || GetModLabel(def) == selectedModFilter;
        }

        private int GetFilteredCount(IEnumerable<Def> defs)
        {
            return defs.Count(DefMatchesModFilter);
        }

        private IEnumerable<Widgets.DropdownMenuElement<string>> GenerateModFilterOptions()
        {
            foreach (string modName in modFilterOptions)
            {
                string chosenMod = modName == AllModFilter ? null : modName;
                yield return new Widgets.DropdownMenuElement<string>
                {
                    payload = chosenMod,
                    option = new FloatMenuOption(modName, delegate
                    {
                        selectedModFilter = chosenMod;
                        rightScroll = Vector2.zero;
                    })
                };
            }
        }

        private void DrawItemList(Rect rect)
        {
            RebuildVisibleDefsIfNeeded();

            float topBannerHeight = 0f;

            bool isAll = selectedCategory == null && !showingFloors;
            bool hasCategory = selectedCategory != null;
            bool isFloors = showingFloors;

            if (hasCategory || isAll || isFloors)
            {
                string bannerLabel = isFloors
                    ? "Floors / terrain (global multiplier)"
                    : isAll ? "Global multiplier" : selectedCategory.label.CapitalizeFirst() + " category multiplier";
                float curMult = (isAll || isFloors)
                    ? WealthTweaksMod.Settings.GlobalMultiplier
                    : WealthTweaksMod.Settings.GetCategoryMultiplier(selectedCategory.defName);

                Rect bannerRect = new Rect(rect.x, rect.y, rect.width, 30f);
                Widgets.DrawBoxSolid(bannerRect, new Color(0.2f, 0.2f, 0.25f, 1f));
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(bannerRect.x + 8f, bannerRect.y, 260f, bannerRect.height);
                Widgets.Label(labelRect, (bannerLabel + ": " + curMult.ToString("F2") + "x").Truncate(labelRect.width));

                Rect sliderRect = new Rect(labelRect.xMax + 8f, bannerRect.y + 4f, 240f, bannerRect.height - 8f);
                float newMult = DrawFineSlider(sliderRect, curMult, 0f, 5f, 0.05f, true);

                if (newMult != curMult)
                {
                    if (isAll || isFloors)
                        WealthTweaksMod.Settings.GlobalMultiplier = Mathf.Round(newMult * 100f) / 100f;
                    else
                        WealthTweaksMod.Settings.SetCategoryMultiplier(selectedCategory.defName, Mathf.Round(newMult * 100f) / 100f);
                }

                if (hasCategory && curMult != 1f)
                {
                    Rect resetRect = new Rect(sliderRect.xMax + 8f, bannerRect.y + 3f, 70f, bannerRect.height - 6f);
                    if (Widgets.ButtonText(resetRect, "1.0x"))
                        WealthTweaksMod.Settings.SetCategoryMultiplier(selectedCategory.defName, 1f);
                }

                Text.Anchor = TextAnchor.UpperLeft;
                topBannerHeight = 34f;
            }

            Rect listRect = new Rect(rect.x, rect.y + topBannerHeight, rect.width, rect.height - topBannerHeight);

            if (isFloors)
            {
                if (visibleTerrainDefs == null || visibleTerrainDefs.Count == 0)
                {
                    Widgets.BeginScrollView(listRect, ref rightScroll, listRect);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(listRect, "No floors found.");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.EndScrollView();
                    return;
                }

                float terrainContentH = visibleTerrainDefs.Count * RowHeight;
                Rect terrainViewRect = new Rect(0f, 0f, listRect.width - 16f, terrainContentH);

                Widgets.BeginScrollView(listRect, ref rightScroll, terrainViewRect);

                int firstTerrain = Mathf.FloorToInt(rightScroll.y / RowHeight);
                int lastTerrain = Mathf.Min(firstTerrain + Mathf.CeilToInt(listRect.height / RowHeight) + 1, visibleTerrainDefs.Count);

                for (int i = firstTerrain; i < lastTerrain; i++)
                {
                    Rect rowRect = new Rect(0f, i * RowHeight, terrainViewRect.width, RowHeight - 2f);
                    DrawTerrainRow(rowRect, visibleTerrainDefs[i]);
                }

                Widgets.EndScrollView();
                return;
            }

            if (visibleDefs == null || visibleDefs.Count == 0)
            {
                Widgets.BeginScrollView(listRect, ref rightScroll, listRect);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listRect, "No items found.");
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndScrollView();
                return;
            }

            float contentH = visibleDefs.Count * RowHeight;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, contentH);

            Widgets.BeginScrollView(listRect, ref rightScroll, viewRect);

            int firstVisible = Mathf.FloorToInt(rightScroll.y / RowHeight);
            int lastVisible = Mathf.Min(firstVisible + Mathf.CeilToInt(listRect.height / RowHeight) + 1, visibleDefs.Count);

            for (int i = firstVisible; i < lastVisible; i++)
            {
                Rect rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);
                DrawItemRow(rowRect, visibleDefs[i]);
            }

            Widgets.EndScrollView();
        }

        private void RebuildVisibleDefsIfNeeded()
        {
            bool searchChanged = searchQuery != lastSearchQuery;
            bool catChanged = selectedCategory != lastCategory;
            bool floorsChanged = showingFloors != lastShowingFloors;
            bool modFilterChanged = selectedModFilter != lastModFilter;

            if (!searchChanged && !catChanged && !floorsChanged && !modFilterChanged && visibleDefs != null && visibleTerrainDefs != null)
                return;

            lastSearchQuery = searchQuery;
            lastCategory = selectedCategory;
            lastShowingFloors = showingFloors;
            lastModFilter = selectedModFilter;

            if (showingFloors)
            {
                IEnumerable<TerrainDef> tsource;

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    string q = searchQuery.ToLower();
                    tsource = allEditableTerrains.Where(d =>
                        (d.label != null && d.label.ToLower().Contains(q)) ||
                        d.defName.ToLower().Contains(q));
                }
                else
                {
                    tsource = allEditableTerrains;
                }

                if (selectedModFilter != null)
                    tsource = tsource.Where(DefMatchesModFilter);

                visibleTerrainDefs = tsource.OrderBy(d => d.label).ToList();
                visibleDefs = new List<ThingDef>();
                return;
            }

            visibleTerrainDefs = new List<TerrainDef>();

            IEnumerable<ThingDef> source;

            if (!string.IsNullOrEmpty(searchQuery))
            {
                string q = searchQuery.ToLower();
                source = allEditableDefs.Where(d =>
                    (d.label != null && d.label.ToLower().Contains(q)) ||
                    d.defName.ToLower().Contains(q));
            }
            else if (selectedCategory != null && categoryItemMap.TryGetValue(selectedCategory, out var list))
            {
                source = list;
            }
            else
            {
                source = allEditableDefs;
            }

            if (selectedModFilter != null)
                source = source.Where(DefMatchesModFilter);

            visibleDefs = source.OrderBy(d => d.label).ToList();
        }

        private static float DrawFineSlider(Rect rect, float value, float min, float max, float step, bool alwaysShowButtons)
        {
            bool showButtons = alwaysShowButtons || Mathf.Abs(value - 1f) > 0.001f;
            const float buttonW = 22f;
            const float gap = 3f;

            float sliderW = showButtons ? rect.width - (buttonW + gap) * 2f : rect.width;
            Rect sliderRect = new Rect(rect.x, rect.y, sliderW, rect.height);
            float result = Widgets.HorizontalSlider(sliderRect, value, min, max, middleAlignment: true, value.ToString("F2") + "x");

            if (showButtons)
            {
                Rect minusRect = new Rect(sliderRect.xMax + gap, rect.y, buttonW, rect.height);
                Rect plusRect = new Rect(minusRect.xMax + gap, rect.y, buttonW, rect.height);
                if (Widgets.ButtonText(minusRect, "-"))
                    result = Mathf.Clamp(Mathf.Round((value - step) / step) * step, min, max);
                if (Widgets.ButtonText(plusRect, "+"))
                    result = Mathf.Clamp(Mathf.Round((value + step) / step) * step, min, max);
            }

            return result;
        }

        private static void DrawItemRow(Rect rect, ThingDef def)
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            float x = rect.x + 2f;

            Rect iconRect = new Rect(x, rect.y + (rect.height - 30f) / 2f, 30f, 30f);
            x += 32f;
            try { Widgets.ThingIcon(iconRect, def); } catch { }

            float specificMult = WealthTweaksMod.Settings.GetSpecificMultiplier(def.defName);
            float baseVal = baseValueCache.TryGetValue(def, out var cached) ? cached : def.GetStatValueAbstract(StatDefOf.MarketValue);
            float effectiveVal = baseVal * WealthTweaksMod.Settings.GetMultiplier(def.defName);

            Rect nameRect = new Rect(x, rect.y + 2f, 240f, rect.height - 4f);
            Text.Anchor = TextAnchor.MiddleLeft;
            bool oldWordWrap = Text.WordWrap;
            Text.WordWrap = true;
            Widgets.Label(nameRect, def.label.CapitalizeFirst());
            Text.WordWrap = oldWordWrap;
            x += 244f;

            Rect valRect = new Rect(x, rect.y, 120f, rect.height);
            string valText = "$" + baseVal.ToString("F0");
            if (effectiveVal != baseVal)
                valText += " > $" + effectiveVal.ToString("F0");
            Widgets.Label(valRect, valText);
            x += 124f;

            Rect sliderRect = new Rect(x, rect.y + 3f, 280f, rect.height - 6f);
            float newMult = DrawFineSlider(sliderRect, specificMult, 0f, 5f, 0.05f, false);

            if (newMult != specificMult)
            {
                WealthTweaksMod.Settings.SetSpecificMultiplier(def.defName, Mathf.Round(newMult * 100f) / 100f);
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawTerrainRow(Rect rect, TerrainDef def)
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            float x = rect.x + 2f;

            Rect iconRect = new Rect(x, rect.y + (rect.height - 30f) / 2f, 30f, 30f);
            x += 32f;
            Widgets.DrawBoxSolid(iconRect, def.DrawColor);
            Widgets.DrawBox(iconRect);

            float specificMult = WealthTweaksMod.Settings.GetTerrainMultiplier(def.defName);
            float baseVal = baseTerrainValueCache.TryGetValue(def, out var cached) ? cached : def.GetStatValueAbstract(StatDefOf.MarketValue);
            float effectiveVal = baseVal * WealthTweaksMod.Settings.GetTerrainTotalMultiplier(def.defName);

            Rect nameRect = new Rect(x, rect.y + 2f, 240f, rect.height - 4f);
            Text.Anchor = TextAnchor.MiddleLeft;
            bool oldWordWrap = Text.WordWrap;
            Text.WordWrap = true;
            Widgets.Label(nameRect, def.label.CapitalizeFirst() + " /cell");
            Text.WordWrap = oldWordWrap;
            x += 244f;

            Rect valRect = new Rect(x, rect.y, 120f, rect.height);
            string valText = "$" + baseVal.ToString("F0");
            if (effectiveVal != baseVal)
                valText += " > $" + effectiveVal.ToString("F0");
            Widgets.Label(valRect, valText);
            x += 124f;

            Rect sliderRect = new Rect(x, rect.y + 3f, 280f, rect.height - 6f);
            float newMult = DrawFineSlider(sliderRect, specificMult, 0f, 5f, 0.05f, false);

            if (newMult != specificMult)
            {
                WealthTweaksMod.Settings.SetTerrainMultiplier(def.defName, Mathf.Round(newMult * 100f) / 100f);
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override void PostClose()
        {
            WealthTweaksMod.Settings.Write();

            if (Find.CurrentMap != null)
            {
                Find.CurrentMap.wealthWatcher.ForceRecount();
            }
            base.PostClose();
        }
    }
}
