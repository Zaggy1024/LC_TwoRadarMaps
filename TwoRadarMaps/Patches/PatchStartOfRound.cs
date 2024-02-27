using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class PatchStartOfRound
    {
        [HarmonyPatch(nameof(StartOfRound.OnClientConnect))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnClientConnectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_OnPlayerConnectedClientRpc = typeof(StartOfRound).GetMethod(nameof(StartOfRound.OnPlayerConnectedClientRpc), BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(m_OnPlayerConnectedClientRpc))
                    yield return new CodeInstruction(OpCodes.Call, typeof(PatchStartOfRound).GetMethod(nameof(SynchronizeMapTargetsToNewClient), BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        private static void SynchronizeMapTargetsToNewClient()
        {
            StartOfRound.Instance.mapScreen.SwitchRadarTargetClientRpc(StartOfRound.Instance.mapScreen.targetTransformIndex);
            Plugin.TerminalMapRenderer.SwitchRadarTargetClientRpc(Plugin.TerminalMapRenderer.targetTransformIndex);
        }
    }
}
