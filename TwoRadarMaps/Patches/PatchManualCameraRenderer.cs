using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal static class PatchManualCameraRenderer
    {
        private static readonly FieldInfo f_ManualCameraRenderer_radarTargets = typeof(ManualCameraRenderer).GetField(nameof(ManualCameraRenderer.radarTargets));

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
            // By default, this just sets the main map's UI canvas. Instead, ensure that the UI actually attached to
            // the map renderer's camera has its plane distance set to match the near clip plane.
            instructionsList.InsertTerminalField(generator, new CodeInstruction(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas);
            return instructionsList;
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ChangeNameOfTargetTransformPostfix(IEnumerable<CodeInstruction> instructions)
        {
            var f_TransformAndName_name = typeof(TransformAndName).GetField(nameof(TransformAndName.name));

            var instructionsList = instructions.ToList();

            // Play a transition and apply the new name when a matching transform is found.
            //   radarTargets[i].name = newName;
            // + OnTargetChanged(i);
            var storeNewName = instructionsList.FindIndex(insn => insn.StoresField(f_TransformAndName_name));
            var getRadarTarget = instructionsList.InstructionRangeForStackItems(storeNewName, 1, 1);
            var targetIndex = instructionsList.InstructionRangeForStackItems(getRadarTarget.End - 1, 0, 0);
            instructionsList.InsertRange(storeNewName + 1,
                [
                    .. instructionsList.GetRangeView(targetIndex),
                    new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(OnTargetNameChanged), BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)])),
                ]);

            return instructionsList;
        }

        private static void ApplyTargetNameChange(ManualCameraRenderer mapRenderer, int targetIndex)
        {
            if (targetIndex == mapRenderer.targetTransformIndex)
                Plugin.StartTargetTransition(mapRenderer, mapRenderer.targetTransformIndex);
        }

        private static void OnTargetNameChanged(int targetIndex)
        {
            ApplyTargetNameChange(StartOfRound.Instance.mapScreen, targetIndex);
            ApplyTargetNameChange(Plugin.TerminalMapRenderer, targetIndex);
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.RemoveTargetFromRadar))]
        [HarmonyPostfix]
        static void RemoveTargetFromRadarPostfix(ManualCameraRenderer __instance)
        {
            Plugin.EnsureAllMapRenderersHaveValidTargets();
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.SyncOrderOfRadarBoostersInList))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SyncOrderOfRadarBoostersInListPostfix(IEnumerable<CodeInstruction> instructions)
        {
            // Patch the SyncOrderOfRadarBoostersInList function to both set the radarTargets field for both maps, and
            // check whether their current targets have changed to ensure that they are valid and update the displayed names.
            // - radarTargets = ...;
            // + ApplySortedTargets(...);
            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(f_ManualCameraRenderer_radarTargets))
                    yield return new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(ApplySortedTargets), BindingFlags.NonPublic | BindingFlags.Static, [typeof(ManualCameraRenderer), typeof(List<TransformAndName>)]));
                else
                    yield return instruction;
            }
        }

        private static void OnTargetListResorted(ManualCameraRenderer mapRenderer, List<TransformAndName> sortedTargets)
        {
            var initialIndex = mapRenderer.targetTransformIndex;

            if (mapRenderer.radarTargets[initialIndex] == sortedTargets[initialIndex])
                return;

            mapRenderer.radarTargets = sortedTargets;
            var validTargetIndex = Plugin.GetNextValidTarget(sortedTargets, initialIndex);
            if (validTargetIndex == -1)
                return;

            Plugin.StartTargetTransition(mapRenderer, validTargetIndex);
        }

        private static void ApplySortedTargets(ManualCameraRenderer _, List<TransformAndName> sortedTargets)
        {
            OnTargetListResorted(StartOfRound.Instance.mapScreen, sortedTargets);
            OnTargetListResorted(Plugin.TerminalMapRenderer, sortedTargets);
            StartOfRound.Instance.mapScreen.radarTargets = sortedTargets;
            Plugin.TerminalMapRenderer.radarTargets = sortedTargets;
        }
    }
}
