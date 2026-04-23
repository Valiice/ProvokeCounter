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
