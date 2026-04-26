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

        hook = gameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
            ActionEffectHandler.Addresses.Receive.Value,
            OnReceiveActionEffect);
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

        if (header->ActionId != ProvokeActionId) return;

        foreach (var member in partyList)
        {
            if (member.EntityId == casterEntityId)
            {
                tracker.Increment(casterEntityId);
                return;
            }
        }
    }

    public void Dispose()
    {
        hook.Disable();
        hook.Dispose();
    }
}
