using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace NoLoot
{
    public class NoLootMod : Mod
    {
        public static NoLootSettings Settings;

        private Vector2 scrollPosition;
        private List<FactionDef> allFactions;
        private string searchText = "";
        private string searchBuffer = "";
        private Dictionary<string, bool> collapsedMods = new Dictionary<string, bool>();

        private const float RowHeight = 28f;
        private const float HeaderHeight = 30f;

        public NoLootMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<NoLootSettings>();
        }

        public override string SettingsCategory() => "No Loot";

        private List<FactionDef> GetAllFactions()
        {
            if (allFactions == null)
            {
                allFactions = DefDatabase<FactionDef>.AllDefs
                    .Where(f => !f.isPlayer && !f.hidden)
                    .OrderBy(f => f.LabelCap.ToString())
                    .ToList();
            }
            return allFactions;
        }

        private static string GetModName(FactionDef def)
        {
            return def.modContentPack?.Name ?? "Unknown";
        }

        private bool IsCollapsed(string modName)
        {
            return collapsedMods.TryGetValue(modName, out bool c) && c;
        }

        private struct RenderRow
        {
            public bool isHeader;
            public string modName;
            public FactionDef faction;
        }

        private List<RenderRow> BuildRenderRows()
        {
            string filter = (searchText ?? "").Trim().ToLower();
            var factions = GetAllFactions();

            if (filter.Length > 0)
            {
                factions = factions.Where(f =>
                    f.LabelCap.ToString().ToLower().Contains(filter) ||
                    GetModName(f).ToLower().Contains(filter) ||
                    f.defName.ToLower().Contains(filter)).ToList();
            }

            var groups = factions
                .GroupBy(f => GetModName(f))
                .OrderBy(g => g.Key)
                .ToList();

            bool isSearching = filter.Length > 0;
            var rows = new List<RenderRow>();

            foreach (var group in groups)
            {
                rows.Add(new RenderRow { isHeader = true, modName = group.Key });

                bool collapsed = !isSearching && IsCollapsed(group.Key);
                if (!collapsed)
                {
                    foreach (var fac in group.OrderBy(f => f.LabelCap.ToString()))
                    {
                        rows.Add(new RenderRow { isHeader = false, modName = group.Key, faction = fac });
                    }
                }
            }

            return rows;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float enableH = 30f;
            float searchH = 30f;
            float countH = 22f;
            float listTop = enableH + searchH + countH;

            Rect enableRect = new Rect(inRect.x, inRect.y, inRect.width, enableH);
            bool enabled = Settings.modEnabled;
            Widgets.CheckboxLabeled(enableRect, "Enable No Loot", ref enabled);
            Settings.modEnabled = enabled;

            Rect searchRect = new Rect(inRect.x, inRect.y + enableH, inRect.width, searchH);
            searchBuffer = Widgets.TextField(searchRect, searchBuffer);
            searchText = searchBuffer;

            var rows = BuildRenderRows();
            int shownFactions = rows.Count(r => !r.isHeader);
            int shownGroups = rows.Count(r => r.isHeader);

            Rect countRect = new Rect(inRect.x, inRect.y + enableH + searchH, inRect.width, countH);
            GUI.color = Color.gray;
            Widgets.Label(countRect, $"{shownFactions} factions in {shownGroups} mods  ({Settings.exemptedFactionDefs.Count} total exempted)");
            GUI.color = Color.white;

            Rect listRect = new Rect(inRect.x, inRect.y + listTop, inRect.width, inRect.height - listTop);

            float viewHeight = 0f;
            foreach (var row in rows)
            {
                viewHeight += row.isHeader ? HeaderHeight : RowHeight;
            }

            Widgets.BeginScrollView(listRect, ref scrollPosition, new Rect(0, 0, listRect.width - 16f, viewHeight));

            float y = 0f;
            float contentWidth = listRect.width - 16f;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (row.isHeader)
                {
                    Rect headerRect = new Rect(0, y, contentWidth, HeaderHeight);
                    bool collapsed = IsCollapsed(row.modName);

                    var bgColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
                    Widgets.DrawBoxSolid(headerRect, bgColor);

                    string arrow = collapsed ? "▶ " : "▼ ";
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    Widgets.Label(headerRect, arrow + row.modName);

                    if (Widgets.ButtonInvisible(headerRect))
                    {
                        collapsedMods[row.modName] = !collapsed;
                    }

                    y += HeaderHeight;
                }
                else
                {
                    Rect rowRect = new Rect(0, y, contentWidth, RowHeight);
                    int factionIndexInGroup = rows.Take(i).Count(r => !r.isHeader && r.modName == row.modName);
                    if (factionIndexInGroup % 2 == 0)
                        Widgets.DrawAltRect(rowRect);

                    FactionDef fac = row.faction;
                    bool isChecked = Settings.exemptedFactionDefs.Contains(fac.defName);
                    bool newVal = isChecked;

                    Rect checkRect = new Rect(rowRect.x + 20f, rowRect.y, rowRect.width - 20f, rowRect.height);
                    Widgets.CheckboxLabeled(checkRect, fac.LabelCap.ToString(), ref newVal);

                    if (newVal != isChecked)
                    {
                        if (newVal)
                            Settings.exemptedFactionDefs.Add(fac.defName);
                        else
                            Settings.exemptedFactionDefs.Remove(fac.defName);
                    }

                    y += RowHeight;
                }
            }

            Widgets.EndScrollView();
        }
    }
}
