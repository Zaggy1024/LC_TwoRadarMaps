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
        static IEnumerable<CodeInstruction> ChangeNameOfTargetTransformTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            // Play a transition and apply the new name when a matching transform is found.
            //   radarTargets[i].name = newName;
            // + OnTargetChanged(i);
            var storeNewName = instructionsList.FindIndex(insn => insn.StoresField(Reflection.f_TransformAndName_name));
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
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> RemoveTargetFromRadarTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var m_List_RemoveAt = typeof(List<TransformAndName>).GetMethod(nameof(List<TransformAndName>.RemoveAt), [typeof(int)]);

            var f_ManualCameraRenderer_targetTransformIndex = typeof(ManualCameraRenderer).GetField(nameof(ManualCameraRenderer.targetTransformIndex));
            var m_List_Count = typeof(List<TransformAndName>).GetMethod("get_Count");

            var m_ManualCameraRenderer_SwitchRadarTargetForward = typeof(ManualCameraRenderer).GetMethod(nameof(ManualCameraRenderer.SwitchRadarTargetForward), [typeof(bool)]);

            var instructionsList = instructions.ToList();

            var loadRadarTarget = instructionsList.FindIndexOfSequence(new System.Predicate<CodeInstruction>[]{
                insn => insn.LoadsField(f_ManualCameraRenderer_radarTargets),
                insn => insn.IsLdloc(),
                insn => insn.Calls(m_List_RemoveAt),
            });
            var loadRadarTargetIndexInsn = instructionsList[loadRadarTarget.Start + 1];

            // RemoveTargetFromRadar() calls SwitchRadarTargetForward(callRPC: false), where the 'callRPC' set to true tells it
            // not to scan through all targets for a valid target. Since a valid target was just removed, we need to do that.
            // However, if we call it with 'callRPC' set to true instead, then it will send a packet telling every client that
            // the target has changed. If we instead only run this on the host, then we will delay updating the target, and
            // clients will see the invalid target for longer.
            //
            // Instead, call a method that immediately scans for a valid target.
            //
            // Apply this patch:
            //   radarTargets.RemoveAt(i);
            // - if (targetTransformIndex >= radarTargets.Count)
            // - {
            // -     targetTransformIndex--;
            // -     SwitchRadarTargetForward(callRPC: false);
            // - }
            // + PatchManualCameraRenderer.RadarTargetWasRemoved(i);
            var targetIsOutOfBoundsCheck = instructionsList.FindIndexOfSequence(
                [
                    insn => insn.IsLdarg(0),
                    insn => insn.LoadsField(f_ManualCameraRenderer_targetTransformIndex),
                    insn => insn.IsLdarg(0),
                    insn => insn.LoadsField(f_ManualCameraRenderer_radarTargets),
                    insn => insn.Calls(m_List_Count),
                    insn => insn.opcode == OpCodes.Blt || insn.opcode == OpCodes.Blt_S,
                ]);
            var targetIsInBoundsLabel = (Label)instructionsList[targetIsOutOfBoundsCheck.End - 1].operand;
            var targetIsOutOfBoundsBlockEnd = instructionsList.FindIndex(targetIsOutOfBoundsCheck.End, insn => insn.labels.Contains(targetIsInBoundsLabel));

            instructionsList.RemoveAbsoluteRange(targetIsOutOfBoundsCheck.Start, targetIsOutOfBoundsBlockEnd);
            instructionsList.InsertRange(targetIsOutOfBoundsCheck.Start,
                [
                    new CodeInstruction(loadRadarTargetIndexInsn),
                    new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(RadarTargetWasRemoved), BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)])).WithLabels(targetIsInBoundsLabel),
                ]);

            return instructionsList;
        }

        private static void FixUpRadarTargetRemovalFor(ManualCameraRenderer mapRenderer, int removedIndex)
        {
            var targetChanged = removedIndex <= mapRenderer.targetTransformIndex;

            if (mapRenderer.targetTransformIndex >= mapRenderer.radarTargets.Count)
                mapRenderer.targetTransformIndex--;

            var validTargetIndex = Plugin.GetNextValidTarget(mapRenderer.radarTargets, mapRenderer.targetTransformIndex);
            if (validTargetIndex == -1)
            {
                mapRenderer.targetedPlayer = null;
                return;
            }

            if (targetChanged)
                Plugin.StartTargetTransition(mapRenderer, validTargetIndex);
        }

        private static void RadarTargetWasRemoved(int removedIndex)
        {
            FixUpRadarTargetRemovalFor(StartOfRound.Instance.mapScreen, removedIndex);
            FixUpRadarTargetRemovalFor(Plugin.TerminalMapRenderer, removedIndex);
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

        [HarmonyPatch(nameof(ManualCameraRenderer.MeetsCameraEnabledConditions))]
        [HarmonyPrefix]
        private static bool MeetsCameraEnabledConditionsPrefix(ManualCameraRenderer __instance, ref bool __result)
        {
            if (__instance != Plugin.TerminalMapRenderer)
                return true;
            var image = Plugin.Terminal.terminalImage;
            __result = image.isActiveAndEnabled && image.texture == __instance.cam.targetTexture;
            return false;
        }
    }
}
