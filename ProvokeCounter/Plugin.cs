using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ProvokeCounter;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

    private const string CommandName = "/provokecounter";

    public Configuration Configuration { get; init; }

    private readonly ProvokeTracker tracker;
    private readonly AllTimeStats allTimeStats;
    private readonly ActionEffectHook actionEffectHook;
    private readonly PartyListOverlay overlay;
    private readonly WindowSystem windowSystem = new("ProvokeCounter");
    private readonly StatsWindow statsWindow;
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        tracker = new ProvokeTracker();
        allTimeStats = new AllTimeStats(PluginInterface);
        actionEffectHook = new ActionEffectHook(tracker, GameInterop);
        overlay = new PartyListOverlay(GameGui, tracker, Configuration);
        statsWindow = new StatsWindow(tracker, allTimeStats, ObjectTable);
        configWindow = new ConfigWindow(Configuration);

        windowSystem.AddWindow(statsWindow);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open provoke counter stats. Use 'reset' to clear current zone counts."
        });

        PluginInterface.UiBuilder.Draw += UpdateNameCache;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.Draw += overlay.Draw;
        PluginInterface.UiBuilder.OpenMainUi += statsWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= statsWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= overlay.Draw;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= UpdateNameCache;
        CommandManager.RemoveHandler(CommandName);
        actionEffectHook.Dispose();
        overlay.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            tracker.Reset();
            ChatGui.Print("[ProvokeCounter] Zone counts reset.");
            return;
        }

        statsWindow.Toggle();
    }

    private unsafe void UpdateNameCache()
    {
        var agentHud = AgentHUD.Instance();
        if (agentHud == null) return;
        foreach (var member in agentHud->PartyMembers)
        {
            if (member.EntityId is 0 or 0xE0000000) continue;
            if (allTimeStats.HasCachedName(member.EntityId)) continue;
            var obj = ObjectTable.FirstOrDefault(o => o?.EntityId == member.EntityId);
            if (obj != null)
                allTimeStats.TryCacheName(member.EntityId, obj.Name.ToString());
        }
    }

    private void OnTerritoryChanged(ushort _)
    {
        allTimeStats.Merge(tracker.GetAllCounts());
        allTimeStats.ClearNameCache();
        tracker.Reset();
    }
}
