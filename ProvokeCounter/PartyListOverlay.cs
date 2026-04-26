using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ProvokeCounter;

public sealed class PartyListOverlay : IDisposable
{
    // Component node indices within _PartyList for each member slot (slot 0 = local player).
    // Verify with /xldev -> Addon Inspector -> _PartyList if badges appear at wrong positions.
    private static readonly int[] MemberNodeIndices = [4, 5, 6, 7, 8, 9, 10, 11];

    // Child node index of the job icon image node within each member's component node.
    // Verify with /xldev -> Addon Inspector -> _PartyList -> expand a member component node.
    private const int JobIconChildIndex = 15;

    // ImGui colors in ABGR format
    private const uint BadgeBorderColor = 0xFF12A1F3; // orange #F3A112, fully opaque
    private const uint BadgeTextColor   = 0xFFFFFFFF; // white, fully opaque

    private readonly IGameGui gameGui;
    private readonly IPartyList partyList;
    private readonly ProvokeTracker tracker;
    private readonly Configuration config;

    public PartyListOverlay(IGameGui gameGui, IPartyList partyList, ProvokeTracker tracker, Configuration config)
    {
        this.gameGui = gameGui;
        this.partyList = partyList;
        this.tracker = tracker;
        this.config = config;
    }

    public unsafe void Draw()
    {
        if (!config.IsOverlayVisible) return;

        var addonWrapper = gameGui.GetAddonByName("_PartyList");
        if (addonWrapper.IsNull) return;
        if (!addonWrapper.IsVisible) return;

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

        var drawList = ImGui.GetWindowDrawList();

        for (var i = 0; i < MemberNodeIndices.Length && i < partyList.Length; i++)
        {
            var member = partyList[i];
            if (member == null) continue;
            if (!tracker.TryGetCount(member.EntityId, out var count)) continue;

            var slotNodeIndex = MemberNodeIndices[i];
            if (slotNodeIndex >= addon->UldManager.NodeListCount) continue;

            var slotNode = addon->UldManager.NodeList[slotNodeIndex];
            if (slotNode == null) continue;
            if (slotNode->Type < NodeType.Component) continue; // must be a component node before casting

            var componentNode = (AtkComponentNode*)slotNode;
            var component = componentNode->Component;
            if (component == null) continue;

            if (JobIconChildIndex >= component->UldManager.NodeListCount) continue;
            var jobIconNode = component->UldManager.NodeList[JobIconChildIndex];
            if (jobIconNode == null) continue;

            // ScreenX/ScreenY on AtkResNode are updated each frame by the game engine
            // and already account for all parent transforms and HUD scale.
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
