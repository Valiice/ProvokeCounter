using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ProvokeCounter;

public sealed class PartyListOverlay : IDisposable
{
    // Component node indices within _PartyList for each UI slot (slot 0 = top).
    // Verify with /xldev -> Addon Inspector -> _PartyList if badges appear at wrong positions.
    private static readonly int[] MemberNodeIndices = [7, 8, 9, 10, 11, 12, 13, 14];

    // Child node index of the job icon image node within each member's component node.
    // Verify with /xldev -> Addon Inspector -> _PartyList -> expand a member component node.
    private const int JobIconChildIndex = 15;

    // ImGui colors in ABGR format
    private const uint BadgeBorderColor = 0xFF12A1F3; // orange #F3A112, fully opaque
    private const uint BadgeTextColor   = 0xFFFFFFFF; // white, fully opaque

    private readonly IGameGui gameGui;
    private readonly ProvokeTracker tracker;
    private readonly Configuration config;
    private bool hasLoggedNodes;

    public PartyListOverlay(IGameGui gameGui, ProvokeTracker tracker, Configuration config)
    {
        this.gameGui = gameGui;
        this.tracker = tracker;
        this.config = config;
    }

    public unsafe void Draw()
    {
        if (!config.IsOverlayVisible) return;

        var addonWrapper = gameGui.GetAddonByName("_PartyList");
        if (addonWrapper.IsNull || !addonWrapper.IsVisible) return;

        var addon = (AtkUnitBase*)addonWrapper.Address;
        var scale = addonWrapper.Scale;

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.SetNextWindowBgAlpha(0f);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration      |
            ImGuiWindowFlags.NoInputs          |
            ImGuiWindowFlags.NoNav             |
            ImGuiWindowFlags.NoMove            |
            ImGuiWindowFlags.NoSavedSettings   |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoFocusOnAppearing;

        if (!ImGui.Begin("##ProvokeCounterOverlay", flags))
        {
            ImGui.End();
            return;
        }

        var agentHud = AgentHUD.Instance();
        if (agentHud == null)
        {
            ImGui.End();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();

        if (!hasLoggedNodes)
        {
            hasLoggedNodes = true;
            Plugin.Log.Information($"[ProvokeCounter] _PartyList total nodes: {addon->UldManager.NodeListCount}");
            for (var n = 0; n < System.Math.Min((int)addon->UldManager.NodeListCount, 20); n++)
            {
                var nd = addon->UldManager.NodeList[n];
                if (nd != null)
                    Plugin.Log.Information($"[ProvokeCounter]   node[{n}] type={(int)nd->Type} id={nd->NodeId}");
            }
            // Log children of node[7] (first member slot) to find job icon child index
            var slot0 = addon->UldManager.NodeList[7];
            if (slot0 != null && slot0->Type >= NodeType.Component)
            {
                var comp0 = ((AtkComponentNode*)slot0)->Component;
                if (comp0 != null)
                {
                    Plugin.Log.Information($"[ProvokeCounter] node[7] children: {comp0->UldManager.NodeListCount}");
                    for (var c = 0; c < comp0->UldManager.NodeListCount; c++)
                    {
                        var cn = comp0->UldManager.NodeList[c];
                        if (cn != null)
                            Plugin.Log.Information($"[ProvokeCounter]   child[{c}] type={(int)cn->Type} id={cn->NodeId}");
                    }
                }
            }
        }

        foreach (var member in agentHud->PartyMembers)
        {
            // Skip empty slots
            if (member.EntityId is 0 or 0xE0000000) continue;
            if (!tracker.TryGetCount(member.EntityId, out var count)) continue;

            var slotIndex = (int)member.Index;
            Plugin.Log.Debug($"[ProvokeCounter] Drawing badge for slot {slotIndex}, nodeIndex={MemberNodeIndices[slotIndex]}, count={count}");
            if (slotIndex >= MemberNodeIndices.Length) continue;

            var nodeIndex = MemberNodeIndices[slotIndex];
            if (nodeIndex >= addon->UldManager.NodeListCount) continue;

            var slotNode = addon->UldManager.NodeList[nodeIndex];
            if (slotNode == null || slotNode->Type < NodeType.Component) continue;

            var componentNode = (AtkComponentNode*)slotNode;
            var component = componentNode->Component;
            if (component == null) continue;

            if (JobIconChildIndex >= component->UldManager.NodeListCount) continue;
            var jobIconNode = component->UldManager.NodeList[JobIconChildIndex];
            if (jobIconNode == null) continue;

            var nodeX = jobIconNode->ScreenX;
            var nodeY = jobIconNode->ScreenY;

            var badgeText = count.ToString();
            var textSize = ImGui.CalcTextSize(badgeText);
            var padding = new Vector2(3f * scale, 1f * scale);
            var badgeSize = textSize + padding * 2f;

            var badgePos = new Vector2(nodeX - badgeSize.X - 2f * scale, nodeY);
            var badgeEnd = badgePos + badgeSize;

            drawList.AddRect(badgePos, badgeEnd, BadgeBorderColor, 2f);
            drawList.AddText(badgePos + padding, BadgeTextColor, badgeText);
        }

        ImGui.End();
    }

    public void Dispose() { }
}
