using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        static IEnumerable<CodeInstruction> InsertTerminalField(IEnumerable<CodeInstruction> instructions, ILGenerator generator, CodeInstruction loadRenderer, FieldInfo vanillaStartOfRoundField, FieldInfo pluginField)
        {
            var instructionsList = instructions.ToList();

            var searchIndex = 0;
            while (true)
            {
                var loadVanillaField = instructionsList.FindIndexOfSequence(searchIndex, new Predicate<CodeInstruction>[]
                {
                    insn => insn.Calls(Reflection.m_StartOfRound_Instance),
                    insn => insn.LoadsField(vanillaStartOfRoundField),
                });
                if (loadVanillaField == null)
                    break;

                var isNotTerminalMapLabel = generator.DefineLabel();
                instructionsList[loadVanillaField.End].labels.Add(isNotTerminalMapLabel);

                var isTerminalMapLabel = generator.DefineLabel();
                var injectBefore = new CodeInstruction[]
                {
                    new CodeInstruction(loadRenderer),
                    new CodeInstruction(OpCodes.Ldsfld, Reflection.f_Plugin_terminalMapRenderer),
                    new CodeInstruction(OpCodes.Beq_S, isTerminalMapLabel),
                };
                instructionsList.InsertRange(loadVanillaField.Start, injectBefore);

                var injectAfter = new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Br_S, isNotTerminalMapLabel),
                    new CodeInstruction(OpCodes.Ldsfld, pluginField).WithLabels(isTerminalMapLabel),
                };
                instructionsList.InsertRange(loadVanillaField.End + injectBefore.Length, injectAfter);

                searchIndex = loadVanillaField.End + injectBefore.Length + injectAfter.Length;
            }

            return instructionsList;
        }

        [HarmonyPatch(nameof(ManualCameraRenderer.updateMapTarget), MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> updateMapTargetTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return InsertTerminalField(instructions, generator, new CodeInstruction(OpCodes.Ldloc_1), Reflection.f_StartOfRound_mapScreenPlayerName, Reflection.f_Plugin_terminalMapScreenPlayerName);
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
            return InsertTerminalField(instructions, generator, new CodeInstruction(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas);
        }
    }
}
