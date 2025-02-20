using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using BepInEx.Bootstrap;

using TwoRadarMaps.Compatibility;
using TwoRadarMaps.Utilities.IL;

namespace TwoRadarMaps.Patches;

internal static class PatchVanillaBugs
{
    // Order of operations for host:
    // - Terminal.Start()
    //   - Re-sort radar map targets are based on their network ID to match what happens after a radar booster is activated.
    // - PlayerControllerB.ConnectClientToPlayerObject(): Attaches the local player to a PlayerControllerB instance.
    //   - Update the displayed username on the map. Initially, it will just be "Player".
    // - PlayerControllerB.SendNewPlayerValuesClientRpc(): Sets the player's username based on their Steam name.
    //   - Update the displayed username on the map once again. This only happens on Online play, so we have to update it twice to ensure LAN is consistent.
    // - StartOfRound.OnClientConnect(): Another client connects to the host.
    //   - Send the selected target for each map to all clients. Ideally, this would send only to the joining client, but there's no easy way to do that.

    // Order of operations for client:
    // - Terminal.Start()
    //   - Same as host.
    // - PlayerControllerB.ConnectClientToPlayerObject():
    // - PlayerControllerB.SendNewPlayerValuesClientRpc():
    //   - Skip updating the displayed username, as that will override the map target sent by the host.
    // - StartOfRound.OnClientConnect(): Not called on the client.

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyAfter(OpenBodyCamsCompatibility.MOD_ID)]
    static void ConnectClientToPlayerObjectPostfix()
    {
        UpdateMonitoredPlayerNames();

        OpenBodyCamsCompatibility.InitializeAtStartOfGame();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
    static IEnumerable<CodeInstruction> SendNewPlayerValuesClientRpcTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // Correct the radar target index, otherwise the re-sorted list will cause names to be mismatched with their
        // transforms.
        // - StartOfRound.Instance.mapScreen.radarTargets[i].name = playerName;
        // + StartOfRound.Instance.mapScreen.ChangeNameOfTargetTransform(StartOfRound.Instance.allPlayerScripts[i].transform, playerName);
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Call(Reflection.m_StartOfRound_Instance),
                ILMatcher.Ldfld(Reflection.f_StartOfRound_mapScreen),
                ILMatcher.Ldfld(Reflection.f_ManualCameraRenderer_radarTargets),
                ILMatcher.Ldloc().CaptureAs(out var loadIndex),
                ILMatcher.Callvirt(Reflection.m_List_TransformAndName_get_Item),
                ILMatcher.Ldloc().CaptureAs(out var loadName),
                ILMatcher.Stfld(Reflection.f_TransformAndName_name),
            ]);

        if (!injector.IsValid)
        {
            if (!Chainloader.PluginInfos.ContainsKey(LobbyControlCompatibility.MOD_ID))
            {
                Plugin.Instance.Logger.LogWarning("Failed to patch SendNewPlayerValuesClientRpc to set the correct transform index's name.");
                Plugin.Instance.Logger.LogWarning("LobbyControl is not installed, but another mod may have fixed this vanilla bug.");
            }

            return instructions;
        }

        return injector
            .ReplaceLastMatch([
                new(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                new(OpCodes.Ldfld, Reflection.f_StartOfRound_mapScreen),
                new(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                new(OpCodes.Ldfld, Reflection.f_StartOfRound_allPlayerScripts),
                loadIndex,
                new(OpCodes.Ldelem, typeof(PlayerControllerB)),
                new(OpCodes.Call, Reflection.m_Component_Transform),
                loadName,
                new(OpCodes.Call, typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform), [typeof(Transform), typeof(string)]))
            ])
            .ReleaseInstructions();
    }

    private static void UpdateMonitoredPlayerNames()
    {
        Plugin.EnsureAllMapRenderersHaveValidTargets();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientConnect))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnClientConnectTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Call(typeof(StartOfRound).GetMethod(nameof(StartOfRound.OnPlayerConnectedClientRpc), BindingFlags.NonPublic | BindingFlags.Instance)),
            ])
            .GoToMatchEnd();

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find the call to {nameof(StartOfRound.OnPlayerConnectedClientRpc)} in {nameof(StartOfRound)}.{nameof(StartOfRound.OnClientConnect)}().");
            return instructions;
        }

        return injector
            .Insert([
                new(OpCodes.Call, typeof(PatchVanillaBugs).GetMethod(nameof(SynchronizeMapTargetsToNewClient), BindingFlags.NonPublic | BindingFlags.Static)),
            ])
            .ReleaseInstructions();
    }

    private static void SynchronizeMapTargetsToNewClient()
    {
        StartOfRound.Instance.mapScreen.SwitchRadarTargetClientRpc(StartOfRound.Instance.mapScreen.targetTransformIndex);
        Plugin.TerminalMapRenderer.SwitchRadarTargetClientRpc(Plugin.TerminalMapRenderer.targetTransformIndex);
    }
}
