# ProvokeCounter

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that tracks how many times each party member uses **Provoke** during an instance and displays a live counter badge directly on the party list UI.

Mostly useful for watching two tanks fight over aggro.

---

## What it does

- Detects every Provoke cast in your party in real time
- Displays a small orange counter badge to the left of the caster's job icon on the party list
- Only shows a badge for members who have actually provoking — no clutter for everyone else
- Tracks party list position and scale — the badge follows wherever you move the party list in HUD Layout
- Resets automatically when you enter a new zone or instance

## Commands

| Command | Action |
|---|---|
| `/provokecounter` | Toggle the overlay on/off |
| `/provokecounter reset` | Clear all counts immediately |

## Installation

> Requires [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) with Dalamud enabled.

1. Open the Dalamud plugin installer (`/xlplugins`)
2. Search for **ProvokeCounter**
3. Click Install

### Dev / Testing

1. Build the project: `dotnet build --configuration Release`
2. In-game, open `/xlsettings` → Experimental → Dev Plugin Locations
3. Point it at the `ProvokeCounter/bin/x64/Release/ProvokeCounter/` folder
4. Enable the plugin from `/xlplugins` → Dev Tools → Installed Dev Plugins

## Notes

- The Provoke detection uses a game function hook. If a FFXIV patch changes the function signature, the hook will silently disable itself and log a warning to `/xllog`. An update will be needed to restore detection after major patches.
- The party list badge positions are based on current FFXIV UI node data. If a patch restructures the party list layout, node indices may need adjusting.

## Building

Requires the [Dalamud.NET.Sdk](https://github.com/goatcorp/DalamudPackager).

```bash
dotnet build ProvokeCounter/ProvokeCounter.csproj
```

## License

[AGPL-3.0](LICENSE.md)
