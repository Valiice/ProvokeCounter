using System;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ProvokeCounter;

public sealed class ActionEffectHook : IDisposable
{
    private const uint ProvokeActionId = 7533;

    private readonly Hook<ActionEffectHandler.Delegates.Receive> hook;
    private readonly ProvokeTracker tracker;
    private readonly IPartyList partyList;

    public unsafe ActionEffectHook(ProvokeTracker tracker, IPartyList partyList, IGameInteropProvider gameInterop)
    {
        this.tracker = tracker;
        this.partyList = partyList;

        var address = ActionEffectHandler.Addresses.Receive.Value;
        Plugin.Log.Information($"[ProvokeCounter] Hooking ReceiveActionEffect at 0x{address:X}");

        hook = gameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
            address, OnReceiveActionEffect);
        hook.Enable();

        Plugin.Log.Information("[ProvokeCounter] ActionEffectHook enabled.");
    }

    private unsafe void OnReceiveActionEffect(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        hook.Original(casterEntityId, caster, targetPos, header, effects, targetEntityIds);

        Plugin.Log.Debug($"[ProvokeCounter] Hook fired: action {header->ActionId} from {casterEntityId}");

        if (header->ActionId != ProvokeActionId) return;

        Plugin.Log.Information($"[ProvokeCounter] Provoke detected from entity {casterEntityId}");
        Plugin.Log.Information($"[ProvokeCounter] Party members ({partyList.Length}): {string.Join(", ", System.Linq.Enumerable.Select(partyList, m => $"{m.Name}={m.EntityId}"))}");

        foreach (var member in partyList)
        {
            if (member.EntityId == casterEntityId)
            {
                tracker.Increment(casterEntityId);
                Plugin.Log.Information($"[ProvokeCounter] Incremented count for {member.Name} -> {tracker.GetCount(casterEntityId)}");
                return;
            }
        }

        Plugin.Log.Warning($"[ProvokeCounter] Provoke from {casterEntityId} but no matching party member found!");
    }

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
    }
}
