using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using TwoRadarMaps.Compatibility;

namespace TwoRadarMaps.Patches
{
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
            var m_ManualCameraRenderer_ChangeNameOfTargetTransform = typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform), [typeof(Transform), typeof(string)]);

            var instructionsList = instructions.ToList();

            // Correct the radar target index, otherwise the re-sorted list will cause names to be mismatched with their
            // transforms.
            // - StartOfRound.Instance.mapScreen.radarTargets[i].name = playerName;
            // + StartOfRound.Instance.mapScreen.ChangeNameOfTargetTransform(StartOfRound.Instance.allPlayerScripts[i].transform, playerName);
            var setName = instructionsList.FindIndex(insn => insn.StoresField(Reflection.f_TransformAndName_name));
            var getTransformAndName = instructionsList.InstructionRangeForStackItems(setName, 1, 1);

            var loadTransformIndex = instructionsList.InstructionRangeForStackItems(getTransformAndName.End - 1, 0, 0);

            instructionsList.RemoveAt(setName);
            instructionsList.RemoveAbsoluteRange(loadTransformIndex.End, getTransformAndName.End);
            instructionsList.RemoveAbsoluteRange(getTransformAndName.Start, loadTransformIndex.Start);

            var insertionIndex = getTransformAndName.Start;
            CodeInstruction[] loadPlayerScriptsInstructions = [
                new CodeInstruction(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                new CodeInstruction(OpCodes.Ldfld, Reflection.f_StartOfRound_allPlayerScripts),
            ];
            instructionsList.InsertRange(insertionIndex, loadPlayerScriptsInstructions);
            insertionIndex += loadPlayerScriptsInstructions.Length;
            insertionIndex += loadTransformIndex.Size;

            CodeInstruction[] loadPlayerTransformInstructions = [
                new CodeInstruction(OpCodes.Ldelem, typeof(PlayerControllerB)),
                new CodeInstruction(OpCodes.Call, Reflection.m_Component_Transform),
            ];
            instructionsList.InsertRange(insertionIndex, loadPlayerTransformInstructions);
            insertionIndex += loadPlayerTransformInstructions.Length;
            insertionIndex += setName - getTransformAndName.End;

            instructionsList.InsertRange(insertionIndex, [
                new CodeInstruction(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                new CodeInstruction(OpCodes.Ldfld, Reflection.f_StartOfRound_mapScreen),
                new CodeInstruction(OpCodes.Call, m_ManualCameraRenderer_ChangeNameOfTargetTransform)
            ]);

            return instructionsList;
        }

        private static void UpdateMonitoredPlayerNames()
        {
            if (!StartOfRound.Instance.IsHost && !StartOfRound.Instance.IsServer)
                return;
            Plugin.EnsureAllMapRenderersHaveValidTargets();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientConnect))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnClientConnectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_OnPlayerConnectedClientRpc = typeof(StartOfRound).GetMethod(nameof(StartOfRound.OnPlayerConnectedClientRpc), BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(m_OnPlayerConnectedClientRpc))
                    yield return new CodeInstruction(OpCodes.Call, typeof(PatchVanillaBugs).GetMethod(nameof(SynchronizeMapTargetsToNewClient), BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        private static void SynchronizeMapTargetsToNewClient()
        {
            StartOfRound.Instance.mapScreen.SwitchRadarTargetClientRpc(StartOfRound.Instance.mapScreen.targetTransformIndex);
            Plugin.TerminalMapRenderer.SwitchRadarTargetClientRpc(Plugin.TerminalMapRenderer.targetTransformIndex);
        }
    }
}
