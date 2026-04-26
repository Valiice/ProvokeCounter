using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace ProvokeCounter;

public sealed class StatsWindow : Window
{
    private readonly ProvokeTracker tracker;
    private readonly AllTimeStats allTimeStats;
    private readonly IObjectTable objectTable;

    public StatsWindow(ProvokeTracker tracker, AllTimeStats allTimeStats, IObjectTable objectTable)
        : base("Provoke Counter")
    {
        this.tracker = tracker;
        this.allTimeStats = allTimeStats;
        this.objectTable = objectTable;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 140),
            MaximumSize = new Vector2(500, 600),
        };
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##provokeTabs"))
            return;

        if (ImGui.BeginTabItem("This Zone"))
        {
            DrawCurrentZone();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("All-Time"))
        {
            DrawAllTime();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawCurrentZone()
    {
        var counts = tracker.GetAllCounts()
            .OrderByDescending(x => x.Value)
            .ToList();

        if (counts.Count == 0)
        {
            ImGui.TextDisabled("No provokes recorded this zone.");
        }
        else
        {
            foreach (var (entityId, count) in counts)
            {
                var name = objectTable.FirstOrDefault(o => o?.EntityId == entityId)?.Name.ToString() ?? "Unknown";
                ImGui.Text($"{name}: {count}");
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Reset##zone"))
            tracker.Reset();
    }

    private void DrawAllTime()
    {
        var all = allTimeStats.GetAllSorted().ToList();

        if (all.Count == 0)
        {
            ImGui.TextDisabled("No history yet.");
        }
        else
        {
            foreach (var (name, count) in all)
                ImGui.Text($"{name}: {count}");
        }

        ImGui.Spacing();
        if (ImGui.Button("Reset##alltime"))
            allTimeStats.ResetAllTime();
    }
}
