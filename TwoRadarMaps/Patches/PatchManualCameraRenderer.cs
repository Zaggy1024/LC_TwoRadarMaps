using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal static class PatchManualCameraRenderer
    {
        [HarmonyPatch(nameof(ManualCameraRenderer.updateMapTarget), MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> updateMapTargetTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var instructionsList = instructions.ToList();
            instructionsList.InsertTerminalField(generator, new CodeInstruction(OpCodes.Ldloc_1), Reflection.f_StartOfRound_mapScreenPlayerName, Reflection.f_Plugin_terminalMapScreenPlayerName);
            return instructionsList;
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.MapCameraFocusOnPosition))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MapCameraFocusOnPositionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var instructionsList = instructions.ToList();
            instructionsList.InsertTerminalField(generator, new CodeInstruction(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas);
            return instructionsList;
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform))]
        [HarmonyPostfix]
        static void ChangeNameOfTargetTransformPostfix(ManualCameraRenderer __instance)
        {
            Plugin.EnsureAllRenderersHaveValidTargets();
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.AddTransformAsTargetToRadar))]
        [HarmonyPostfix]
        static void AddTransformAsTargetToRadarPostfix(ManualCameraRenderer __instance)
        {
            Plugin.EnsureAllRenderersHaveValidTargets();
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
        [HarmonyPostfix]
        static void RemoveTargetFromRadarPostfix(ManualCameraRenderer __instance)
        {
            Plugin.EnsureAllRenderersHaveValidTargets();
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.SyncOrderOfRadarBoostersInList))]
        [HarmonyPostfix]
        static void SyncOrderOfRadarBoostersInListPostfix(ManualCameraRenderer __instance)
        {
            Plugin.UpdateTerminalMapTargetList();
        }
    }
}
