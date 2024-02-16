using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        [HarmonyPatch(nameof(ManualCameraRenderer.updateMapTarget), MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> updateMapTargetTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            instructionsList.InsertTerminalField(generator, new CodeInstruction(OpCodes.Ldloc_1), Reflection.f_StartOfRound_mapScreenPlayerName, Reflection.f_Plugin_terminalMapScreenPlayerName);
            return instructionsList;
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform))]
        [HarmonyPostfix]
        static void ChangeNameOfTargetTransformPostfix(ref ManualCameraRenderer __instance)
        {
            Plugin.UpdateRadarTargets();
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.MapCameraFocusOnPosition))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MapCameraFocusOnPositionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var instructionsList = new List<CodeInstruction>(instructions);
            instructionsList.InsertTerminalField(generator, new CodeInstruction(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas);
            return instructionsList;
        }
    }
}
