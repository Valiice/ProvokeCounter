using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ProvokeCounter;

public sealed class ConfigWindow : Window
{
    private readonly Configuration config;

    public ConfigWindow(Configuration config) : base("Provoke Counter Settings")
    {
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 80),
            MaximumSize = new Vector2(400, 200),
        };
    }

    public override void Draw()
    {
        var visible = config.IsOverlayVisible;
        if (ImGui.Checkbox("Show party list overlay", ref visible))
        {
            config.IsOverlayVisible = visible;
            config.Save();
        }
    }
}
