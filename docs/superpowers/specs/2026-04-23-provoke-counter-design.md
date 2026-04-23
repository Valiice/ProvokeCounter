# Provoke Counter Plugin — Design Spec

**Date:** 2026-04-23  
**Project:** ProvokeCounter (Dalamud FFXIV Plugin)

---

## Overview

A Dalamud plugin that tracks how many times each party member uses Provoke (action ID 7533) during an instance, and displays a counter badge to the left of their job icon on the party list. Intended as a fun in-raid tool to see tanks competing over who provokes the most.

---

## Detection

**File:** `ProvokeCounter/ActionEffectHook.cs`

Use `IGameInterop.HookFromAddress` to hook the game's `ReceiveActionEffect` function on plugin load. This function fires every time an action resolves in the zone. On each invocation:

1. Always call the original function first (game behavior must be unaffected).
2. Check if the action ID equals 7533 (Provoke).
3. Look up the source actor's ObjectId in `IPartyList`.
4. If they are in the party, call `ProvokeTracker.Increment(objectId)`.

This approach is reliable across UI states, does not depend on chat log visibility, and is the standard Dalamud pattern for combat event detection.

---

## Counter Storage

**File:** `ProvokeCounter/ProvokeTracker.cs`

Maintains a `Dictionary<ulong, int>` keyed on actor ObjectId (64-bit, stable within a zone). An entry only exists if that actor has provooked at least once — absence means zero, which means no badge is rendered.

Public API:
- `Increment(ulong objectId)` — add 1 to the actor's count, creating the entry if needed
- `Reset()` — clear the entire dictionary
- `GetCount(ulong objectId)` — return count, or 0 if no entry exists

**Auto-reset:** Subscribe to `IClientState.TerritoryChanged` in `Plugin.cs`. When it fires, call `ProvokeTracker.Reset()`.

**Overlay visibility state** is persisted in `Configuration.cs` as `bool IsOverlayVisible` (default: `true`). The `/provokecounter` toggle writes this and calls `Configuration.Save()`.

---

## Overlay Rendering

**File:** `ProvokeCounter/PartyListOverlay.cs`

Registered on `IPluginInterface.UiBuilder.Draw`. Each frame:

1. Fetch the `_PartyList` addon via `IGameGui.GetAddonByName("_PartyList")`.
2. If the addon is null, not ready, or not visible, skip drawing entirely.
3. Read `AtkUnitBase.X`, `AtkUnitBase.Y`, and `AtkUnitBase.Scale`.
4. For each of the 8 party member slots, read the job icon node's screen position via the AtkUnitBase node list. The exact node index per slot must be determined during implementation by inspecting the `_PartyList` node tree (e.g. via Dalamud's UIDebug addon inspector).
5. Look up that slot's ObjectId → call `ProvokeTracker.GetCount(objectId)`.
6. If count > 0, draw the badge.

**ImGui window flags:** `NoBackground | NoInputs | NoDecoration | NoMove | NoScrollbar | NoBringToFrontOnFocus`. The window is pinned to full screen size so it acts as a pure draw surface with no chrome.

**Badge position:** `(jobIconScreenX - badgeWidth * scale, jobIconScreenY)` — the badge sits immediately to the left of the job icon, scaled to match the party list scale.

**Badge style:** Orange outline (`#F39C12`), transparent fill, white number text. Rendered using `ImGui.GetWindowDrawList()` for precise positioning — `DrawList.AddRect` for the border, `DrawList.AddText` for the number.

---

## Commands

Both commands registered via `ICommandManager` in `Plugin.cs`.

| Command | Behaviour |
|---|---|
| `/provokecounter` | Toggle overlay visibility on/off |
| `/provokecounter reset` | Call `ProvokeTracker.Reset()`, print confirmation to chat |

---

## File Structure

```
ProvokeCounter/
  Plugin.cs                  — entry point, service wiring, command registration
  Configuration.cs           — persisted settings (overlay visible toggle)
  ActionEffectHook.cs        — IGameInterop hook, Provoke detection
  ProvokeTracker.cs          — counter dictionary, Increment/Reset/GetCount
  PartyListOverlay.cs        — ImGui draw-surface, badge rendering
  ProvokeCounter.csproj
  ProvokeCounter.json
```

The existing `Windows/` folder and sample plugin boilerplate (MainWindow, ConfigWindow, goat image) will be removed and replaced with the above structure.

---

## Out of Scope

- Tracking actions other than Provoke (Shirk, Reprisal, etc.)
- Persisting counts across sessions or zone changes
- Any UI beyond the party list overlay badge
