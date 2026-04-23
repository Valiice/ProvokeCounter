using System;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace ProvokeCounter;

public sealed class ActionEffectHook : IDisposable
{
    private const uint ProvokeActionId = 7533;

    private delegate void ReceiveActionEffectDelegate(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTrail);

    [Signature(
        "40 55 53 57 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 45 0F B6 F9",
        DetourName = nameof(OnReceiveActionEffect))]
#pragma warning disable CS0649 // Field assigned via reflection by InitializeFromAttributes
    private Hook<ReceiveActionEffectDelegate>? hook;
#pragma warning restore CS0649

    private readonly ProvokeTracker tracker;
    private readonly IPartyList partyList;

    public ActionEffectHook(ProvokeTracker tracker, IPartyList partyList, IGameInteropProvider gameInterop)
    {
        this.tracker = tracker;
        this.partyList = partyList;
        gameInterop.InitializeFromAttributes(this);
        hook?.Enable();
    }

    private void OnReceiveActionEffect(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTrail)
    {
        hook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        // ActionEffectHeader.ActionId is at byte offset 8
        var actionId = (uint)Marshal.ReadInt32(effectHeader, 8);
        if (actionId != ProvokeActionId) return;

        foreach (var member in partyList)
        {
            if (member.EntityId == sourceId)
            {
                tracker.Increment(sourceId);
                return;
            }
        }
    }

    public void Dispose()
    {
        hook?.Disable();
        hook?.Dispose();
    }
}
