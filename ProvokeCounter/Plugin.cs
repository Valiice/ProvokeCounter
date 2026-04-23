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
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
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
