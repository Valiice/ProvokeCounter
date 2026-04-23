# Provoke Counter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Dalamud FFXIV plugin that tracks Provoke (action ID 7533) usage per party member and renders an orange outline counter badge to the left of their job icon on the party list, respecting the party list's position and scale, resetting per zone.

**Architecture:** Five focused files — `ProvokeTracker` owns counter state with no Dalamud dependencies, `ActionEffectHook` hooks `ReceiveActionEffect` via IGameInterop to detect Provoke casts, `PartyListOverlay` reads `_PartyList` AtkUnitBase each frame and draws ImGui badges at the correct screen position, `Configuration` persists the visibility toggle, and `Plugin` wires everything together.

**Tech Stack:** C# 12, Dalamud.NET.Sdk 14.0.2, FFXIVClientStructs (bundled with Dalamud SDK), ImGuiNET (bundled with Dalamud)

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `SamplePlugin/SamplePlugin.csproj` | Rename → `ProvokeCounter/ProvokeCounter.csproj` | Project file |
| `SamplePlugin.sln` | Modify | Solution project reference |
| `ProvokeCounter/ProvokeCounter.json` | Replace `SamplePlugin.json` | Dalamud manifest |
| `ProvokeCounter/Configuration.cs` | Replace | `IsOverlayVisible` bool + Save() |
| `ProvokeCounter/ProvokeTracker.cs` | Create | `Dictionary<uint, int>` counter state |
| `ProvokeCounter/ActionEffectHook.cs` | Create | IGameInterop hook, Provoke detection |
| `ProvokeCounter/PartyListOverlay.cs` | Create | ImGui overlay, badge rendering |
| `ProvokeCounter/Plugin.cs` | Replace | Entry point, wiring, commands, TerritoryChanged |
| `ProvokeCounter/Windows/` | Delete | Sample boilerplate no longer needed |
| `Data/goat.png` | Delete | Sample asset no longer needed |

---

## Task 1: Rename and clean up the SamplePlugin scaffold

**Files:**
- Rename: `SamplePlugin/` → `ProvokeCounter/`
- Modify: `SamplePlugin.sln`
- Replace: `ProvokeCounter/ProvokeCounter.json`

- [ ] **Step 1: Rename the folder and project files**

```bash
git mv SamplePlugin ProvokeCounter
git mv ProvokeCounter/SamplePlugin.csproj ProvokeCounter/ProvokeCounter.csproj
git mv ProvokeCounter/SamplePlugin.json ProvokeCounter/ProvokeCounter.json
```

- [ ] **Step 2: Update the solution file**

Open `SamplePlugin.sln` and replace the project entry line:

Old:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SamplePlugin", "SamplePlugin\SamplePlugin.csproj", "{...}"
```
New:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ProvokeCounter", "ProvokeCounter\ProvokeCounter.csproj", "{...}"
```
Keep the GUID unchanged.

- [ ] **Step 3: Replace the Dalamud manifest**

Replace all contents of `ProvokeCounter/ProvokeCounter.json`:

```json
{
  "Author": "Valiice",
  "Name": "ProvokeCounter",
  "Punchline": "Counts how many times tanks provoke in your party.",
  "Description": "Displays a counter badge on the party list for each party member who uses Provoke. Resets per zone. Toggle with /provokecounter, reset with /provokecounter reset.",
  "ApplicableVersion": "any",
  "Tags": ["party", "tank", "provoke", "counter", "fun"]
}
```

- [ ] **Step 4: Remove sample boilerplate files**

```bash
rm -rf ProvokeCounter/Windows
rm Data/goat.png
```

- [ ] **Step 5: Remove the goat.png reference from the csproj**

In `ProvokeCounter/ProvokeCounter.csproj`, delete this block:

```xml
<ItemGroup>
  <Content Include="..\Data\goat.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Visible>false</Visible>
  </Content>
</ItemGroup>
```

- [ ] **Step 6: Fix namespace in existing files**

In `ProvokeCounter/Configuration.cs` and `ProvokeCounter/Plugin.cs`:
- Change `namespace SamplePlugin;` → `namespace ProvokeCounter;`
- Remove all `using SamplePlugin.Windows;` references
- In `Plugin.cs`, strip out all WindowSystem, ConfigWindow, MainWindow, goat path, and TextureProvider references, leaving only a minimal compilable class (constructor and Dispose can be nearly empty for now).

- [ ] **Step 7: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded** (warnings OK, zero errors).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: rename SamplePlugin scaffold to ProvokeCounter"
```

---

## Task 2: Implement ProvokeTracker

**Files:**
- Create: `ProvokeCounter/ProvokeTracker.cs`

- [ ] **Step 1: Create `ProvokeCounter/ProvokeTracker.cs`**

```csharp
using System.Collections.Generic;

namespace ProvokeCounter;

public sealed class ProvokeTracker
{
    private readonly Dictionary<uint, int> counts = new();

    public void Increment(uint objectId)
    {
        counts.TryGetValue(objectId, out var current);
        counts[objectId] = current + 1;
    }

    public int GetCount(uint objectId) =>
        counts.TryGetValue(objectId, out var count) ? count : 0;

    public bool HasCount(uint objectId) => counts.ContainsKey(objectId);

    public void Reset() => counts.Clear();
}
```

Note: `uint` matches the type Dalamud's `IPartyMember.ObjectId` and the `sourceId` parameter in `ReceiveActionEffect` — do not change to `ulong`.

- [ ] **Step 2: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded**.

- [ ] **Step 3: Commit**

```bash
git add ProvokeCounter/ProvokeTracker.cs
git commit -m "feat: add ProvokeTracker counter dictionary"
```

---

## Task 3: Update Configuration

**Files:**
- Modify: `ProvokeCounter/Configuration.cs`

- [ ] **Step 1: Replace `Configuration.cs` contents**

```csharp
using Dalamud.Configuration;
using System;

namespace ProvokeCounter;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsOverlayVisible { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded**.

- [ ] **Step 3: Commit**

```bash
git add ProvokeCounter/Configuration.cs
git commit -m "feat: update Configuration with IsOverlayVisible"
```

---

## Task 4: Implement ActionEffectHook

**Files:**
- Create: `ProvokeCounter/ActionEffectHook.cs`

**Context:** `ReceiveActionEffect` fires every time any action resolves in the zone. Parameter `sourceId` is the caster's ObjectId (`uint`). The action ID sits at byte offset 8 in the `effectHeader` pointer — this is the well-documented `ActionEffectHeader.ActionId` field. The `[Signature]` attribute + `IGameInterop.InitializeFromAttributes` finds and patches the function automatically.

**Signature note:** The signature below targets FFXIV 7.x. If the plugin logs "Signature not found" at runtime after a game patch, get the updated signature from the Dalamud Discord `#plugin-dev` channel or from open-source combat plugins such as those in the Dalamud plugin repository.

- [ ] **Step 1: Create `ProvokeCounter/ActionEffectHook.cs`**

```csharp
using System;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace ProvokeCounter;

public sealed class ActionEffectHook : IDisposable
{
    private const uint ProvokeActionId = 7533;

    private delegate void ReceiveActionEffectDelegate(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTrail);

    [Signature(
        "40 55 53 57 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 45 0F B6 F9",
        DetourName = nameof(OnReceiveActionEffect))]
    private Hook<ReceiveActionEffectDelegate>? hook;

    private readonly ProvokeTracker tracker;
    private readonly IPartyList partyList;

    public ActionEffectHook(ProvokeTracker tracker, IPartyList partyList, IGameInterop gameInterop)
    {
        this.tracker = tracker;
        this.partyList = partyList;
        gameInterop.InitializeFromAttributes(this);
        hook?.Enable();
    }

    private void OnReceiveActionEffect(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTrail)
    {
        hook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        // ActionEffectHeader.ActionId is at byte offset 8
        var actionId = (uint)Marshal.ReadInt32(effectHeader, 8);
        if (actionId != ProvokeActionId) return;

        foreach (var member in partyList)
        {
            if (member.ObjectId == sourceId)
            {
                tracker.Increment(sourceId);
                return;
            }
        }
    }

    public void Dispose()
    {
        hook?.Disable();
        hook?.Dispose();
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded**.

- [ ] **Step 3: Commit**

```bash
git add ProvokeCounter/ActionEffectHook.cs
git commit -m "feat: add ActionEffectHook for Provoke detection"
```

---

## Task 5: Implement PartyListOverlay

**Files:**
- Create: `ProvokeCounter/PartyListOverlay.cs`

**Context:** The `_PartyList` AtkUnitBase contains one component node per party slot. Based on Dalamud community research, member slots start at node index 4 (local player = slot 0) through 11 (slot 7). The job icon image node within each slot component is at child index 15. **These indices must be verified in-game with `/xldev` → Addon Inspector → `_PartyList` before trusting badge positions — adjust the constants if they differ.**

The overlay is a full-screen ImGui window with all interaction flags disabled so it acts as a pure draw surface. Badge color is orange outline (#F3A112) with white text, matching the design.

- [ ] **Step 1: Create `ProvokeCounter/PartyListOverlay.cs`**

```csharp
using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

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

        var addonPtr = gameGui.GetAddonByName("_PartyList");
        if (addonPtr == nint.Zero) return;

        var addon = (AtkUnitBase*)addonPtr;
        if (!addon->IsVisible) return;

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
        var scale = addon->Scale;

        for (var i = 0; i < MemberNodeIndices.Length && i < partyList.Count; i++)
        {
            var member = partyList[i];
            if (member == null) continue;
            if (!tracker.HasCount(member.ObjectId)) continue;

            var slotNodeIndex = MemberNodeIndices[i];
            if (slotNodeIndex >= addon->UldManager.NodeListCount) continue;

            var slotNode = addon->UldManager.NodeList[slotNodeIndex];
            if (slotNode == null) continue;

            var componentNode = (AtkComponentNode*)slotNode;
            var component = componentNode->Component;
            if (component == null) continue;

            if (JobIconChildIndex >= component->UldManager.NodeListCount) continue;
            var jobIconNode = component->UldManager.NodeList[JobIconChildIndex];
            if (jobIconNode == null) continue;

            // GetScreenPosition accounts for all parent transforms and HUD scale
            int nodeX, nodeY;
            jobIconNode->GetScreenPosition(&nodeX, &nodeY);

            var badgeText = tracker.GetCount(member.ObjectId).ToString();
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
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded**.

- [ ] **Step 3: Commit**

```bash
git add ProvokeCounter/PartyListOverlay.cs
git commit -m "feat: add PartyListOverlay ImGui badge renderer"
```

---

## Task 6: Wire up Plugin.cs

**Files:**
- Modify: `ProvokeCounter/Plugin.cs`

- [ ] **Step 1: Replace `Plugin.cs` contents entirely**

```csharp
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ProvokeCounter;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInterop GameInterop { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/provokecounter";

    public Configuration Configuration { get; init; }

    private readonly ProvokeTracker tracker;
    private readonly ActionEffectHook actionEffectHook;
    private readonly PartyListOverlay overlay;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        tracker = new ProvokeTracker();
        actionEffectHook = new ActionEffectHook(tracker, PartyList, GameInterop);
        overlay = new PartyListOverlay(GameGui, PartyList, tracker, Configuration);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle provoke counter overlay. Use 'reset' to clear all counts."
        });

        PluginInterface.UiBuilder.Draw += overlay.Draw;
        ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        PluginInterface.UiBuilder.Draw -= overlay.Draw;
        CommandManager.RemoveHandler(CommandName);
        actionEffectHook.Dispose();
        overlay.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("reset", System.StringComparison.OrdinalIgnoreCase))
        {
            tracker.Reset();
            ChatGui.Print("[ProvokeCounter] Counts reset.");
            return;
        }

        Configuration.IsOverlayVisible = !Configuration.IsOverlayVisible;
        Configuration.Save();
        ChatGui.Print(Configuration.IsOverlayVisible
            ? "[ProvokeCounter] Overlay shown."
            : "[ProvokeCounter] Overlay hidden.");
    }

    private void OnTerritoryChanged(ushort territory) => tracker.Reset();
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

Expected: **Build succeeded, zero errors**.

- [ ] **Step 3: Commit**

```bash
git add ProvokeCounter/Plugin.cs
git commit -m "feat: wire up Plugin with hook, overlay, commands, and zone reset"
```

---

## Task 7: In-game verification

**No code changes — manual testing inside FFXIV.**

Load the plugin via Dalamud dev plugin loader: `/xlsettings` → `Experimental` → `Dev Plugin Locations` → point to the `bin/Debug/` output folder of `ProvokeCounter.csproj`. Enable the plugin.

- [ ] **Step 1: Verify node indices with UIDebug**

In-game, open `/xldev` → `Addon Inspector`. Search for `_PartyList`. Expand the node tree:
- Confirm which node indices correspond to party member slot components (expected: 4–11)
- Expand one member component node and find the job icon image node index (expected: 15)

If either index differs, update `MemberNodeIndices` or `JobIconChildIndex` in `PartyListOverlay.cs`, rebuild, and reload.

- [ ] **Step 2: Verify no badges appear before any Provoke**

Join a party. The party list should look completely normal — no badge visible for any member.

- [ ] **Step 3: Verify Provoke detection and badge rendering**

Have a tank use Provoke. A badge reading `1` should appear to the left of their job icon. Each additional Provoke increments the number.

- [ ] **Step 4: Verify position and scale tracking**

Open HUD Layout (`/hudlayout`), move the party list to a corner, change its size. Exit HUD Layout. Use Provoke — confirm the badge still appears correctly positioned and sized relative to the relocated/rescaled party list.

- [ ] **Step 5: Verify zone reset**

Travel to a different zone or enter a duty. All badges should disappear (tracker cleared by `TerritoryChanged`).

- [ ] **Step 6: Verify `/provokecounter reset`**

While badges are showing, type `/provokecounter reset`. All badges should disappear and the chat log should show `[ProvokeCounter] Counts reset.`

- [ ] **Step 7: Verify `/provokecounter` toggle**

Type `/provokecounter`. Overlay hides, chat shows `[ProvokeCounter] Overlay hidden.` Type again — overlay returns.

- [ ] **Step 8: Commit any fixes from in-game testing**

```bash
git add -A
git commit -m "fix: adjust node indices / badge offsets from in-game verification"
```
