using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using GameNetcodeStuff;
using HarmonyLib;

using TwoRadarMaps.Compatibility;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PatchPlayerControllerB
    {
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyAfter(OpenBodyCamsCompatibility.MOD_ID)]
        static void ConnectClientToPlayerObjectPostfix()
        {
            UpdateMonitoredPlayerNames();

            OpenBodyCamsCompatibility.InitializeAtStartOfGame();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        static IEnumerable<CodeInstruction> SendNewPlayerValuesClientRpcTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();
            
            // Correct the radar target index, otherwise the re-sorted list will cause names to be mismatched with their
            // transforms.
            // - StartOfRound.Instance.mapScreen.radarTargets[i].name = playerName;
            // + StartOfRound.Instance.mapScreen.radarTargets[GetTransformIndexForPlayerIndex(i)].name = playerName;
            var setName = instructionsList.FindIndex(insn => insn.StoresField(Reflection.f_TransformAndName_name));
            var getTransformAndName = instructionsList.InstructionRangeForStackItems(setName, 1, 1);
            var transformIndex = instructionsList.InstructionRangeForStackItems(getTransformAndName.End - 1, 0, 0);
            instructionsList.Insert(transformIndex.End, new CodeInstruction(OpCodes.Call, typeof(PatchPlayerControllerB).GetMethod(nameof(GetTransformIndexForPlayerIndex), BindingFlags.NonPublic | BindingFlags.Static)));

            // Insert a call to UpdateMonitoredPlayerNames() before the function ends.
            instructionsList.Insert(instructionsList.Count - 1, new CodeInstruction(OpCodes.Call, typeof(PatchPlayerControllerB).GetMethod(nameof(UpdateMonitoredPlayerNames), BindingFlags.NonPublic | BindingFlags.Static)));
            
            return instructionsList;
        }

        private static int GetTransformIndexForPlayerIndex(int index)
        {
            var playerTransform = StartOfRound.Instance.allPlayerScripts[index].transform;
            var targetTransforms = StartOfRound.Instance.mapScreen.radarTargets;
            for (int i = 0; i < targetTransforms.Count; i++)
            {
                if (targetTransforms[i].transform == playerTransform)
                    return i;
            }

            Plugin.Instance.Logger.LogError($"Player script #{index} was not found in the map's target list!\n{new StackTrace()}");
            return 0;
        }

        private static void UpdateMonitoredPlayerNames()
        {
            if (!StartOfRound.Instance.IsHost && !StartOfRound.Instance.IsServer)
                return;
            Plugin.EnsureAllMapRenderersHaveValidTargets();
        }
    }
}
