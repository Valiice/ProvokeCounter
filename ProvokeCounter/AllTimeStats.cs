using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin;

namespace ProvokeCounter;

public sealed class AllTimeStats
{
    private readonly string filePath;
    private readonly Dictionary<uint, string> nameCache = [];
    private Dictionary<string, int> totals;

    public AllTimeStats(IDalamudPluginInterface pluginInterface)
    {
        filePath = Path.Combine(pluginInterface.ConfigDirectory.FullName, "alltime.json");
        totals = Load();
    }

    public bool HasCachedName(uint entityId) => nameCache.ContainsKey(entityId);

    public void TryCacheName(uint entityId, string name)
    {
        if (!string.IsNullOrEmpty(name))
            nameCache[entityId] = name;
    }

    public void ClearNameCache() => nameCache.Clear();

    public void Merge(IReadOnlyDictionary<uint, int> sessionCounts)
    {
        foreach (var (entityId, count) in sessionCounts)
        {
            if (!nameCache.TryGetValue(entityId, out var name)) continue;
            totals.TryGetValue(name, out var existing);
            totals[name] = existing + count;
        }
        Save();
    }

    public IEnumerable<(string Name, int Count)> GetAllSorted() =>
        totals.OrderByDescending(x => x.Value).Select(x => (x.Key, x.Value));

    public void ResetAllTime()
    {
        totals.Clear();
        Save();
    }

    private Dictionary<string, int> Load()
    {
        if (!File.Exists(filePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(filePath)) ?? [];
        }
        catch { return []; }
    }

    private void Save() =>
        File.WriteAllText(filePath, JsonSerializer.Serialize(totals, new JsonSerializerOptions { WriteIndented = true }));
}
