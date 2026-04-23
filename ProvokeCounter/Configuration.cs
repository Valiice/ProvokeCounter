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
