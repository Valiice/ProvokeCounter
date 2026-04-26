using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ProvokeCounter;

public sealed class PartyListOverlay : IDisposable
{
    // ImGui colors in ABGR format
    private const uint BadgeBorderColor = 0xFF12A1F3; // orange #F3A112, fully opaque
    private const uint BadgeTextColor   = 0xFFFFFFFF; // white, fully opaque

    private readonly IGameGui gameGui;
    private readonly ProvokeTracker tracker;
    private readonly Configuration config;

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

        var agentHud = AgentHUD.Instance();
        if (agentHud == null) return;

        var addon = (AddonPartyList*)addonWrapper.Address;
        var scale = addonWrapper.Scale;
        var partyMembers = addon->PartyMembers;

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

        foreach (var member in agentHud->PartyMembers)
        {
            if (member.EntityId is 0 or 0xE0000000) continue;
            if (!tracker.TryGetCount(member.EntityId, out var count)) continue;

            var slotIndex = (int)member.Index;
            if (slotIndex >= partyMembers.Length) continue;

            var jobIcon = partyMembers[slotIndex].ClassJobIcon;
            if (jobIcon == null) continue;

            var nodeX = jobIcon->ScreenX;
            var nodeY = jobIcon->ScreenY;
            if (nodeX == 0 && nodeY == 0) continue;

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
