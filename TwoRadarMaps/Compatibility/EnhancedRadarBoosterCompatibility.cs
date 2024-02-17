using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using EnhancedRadarBooster;
using HarmonyLib;
using UnityEngine;

using TwoRadarMaps.Patches;

namespace TwoRadarMaps.Compatibility
{
    internal static class EnhancedRadarBoosterCompatibility
    {
        internal const string MOD_ID = "MrHydralisk.EnhancedRadarBooster";

        internal static void Initialize(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(MOD_ID))
                return;
            InitializeInternal(harmony);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeInternal(Harmony harmony)
        {
            var m_MapCameraFocusOnPosition_Postfix = typeof(HarmonyPatches).GetMethod(nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix), BindingFlags.Public | BindingFlags.Static, null, [typeof(ManualCameraRenderer)], null);
            if (m_MapCameraFocusOnPosition_Postfix == null)
            {
                Plugin.Instance.Logger.LogError($"{MOD_ID} is present, but {nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix)} was not found.");
                return;
            }
            try
            {
                harmony
                    .CreateProcessor(m_MapCameraFocusOnPosition_Postfix)
                    .AddTranspiler(AccessTools.DeclaredMethod(typeof(EnhancedRadarBoosterCompatibility), nameof(MapCameraFocusOnPosition_Postfix_Transpiler)))
                    .Patch();
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError($"Failed to transpile {MOD_ID}'s {nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix)}");
                Plugin.Instance.Logger.LogError(e);
            }

            Plugin.Instance.Logger.LogInfo($"Patched EnhancedRadarBooster for compatibility with the zoom command.");
        }

        private static IEnumerable<CodeInstruction> MapCameraFocusOnPosition_Postfix_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var m_Camera_set_orthographicSize = typeof(Camera).GetMethod($"set_{nameof(Camera.orthographicSize)}");
            var m_Plugin_GetZoomOrthographicSize = typeof(Plugin).GetMethod(nameof(Plugin.GetZoomOrthographicSize));

            var instructionsList = instructions.ToList();

            // Find:
            //   __instance.mapCamera.orthographicSize = [constant]...
            // and transform it into:
            //   __instance.mapCamera.orthographicSize = (__instance == Plugin.terminalMapRenderer ? Plugin.GetZoomOrthographicSize() : [constant])...
            var setOrthographicSize = 0;
            while (true)
            {
                setOrthographicSize = instructionsList.FindIndex(setOrthographicSize, insn => insn.Calls(m_Camera_set_orthographicSize));

                if (setOrthographicSize == -1)
                    break;

                var orthographicSizeCalculation = instructionsList.InstructionRangeForStackItems(setOrthographicSize, 0, 0);
                for (int i = orthographicSizeCalculation.Start; i < orthographicSizeCalculation.End; i++)
                {
                    if (instructionsList[i].LoadsConstant())
                    {
                        var isNotTerminalMapLabel = generator.DefineLabel();
                        instructionsList[i + 1].labels.Add(isNotTerminalMapLabel);

                        var isTerminalMapLabel = generator.DefineLabel();
                        CodeInstruction[] insertBefore = [
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldsfld, Reflection.f_Plugin_terminalMapRenderer),
                            new CodeInstruction(OpCodes.Beq_S, isTerminalMapLabel),
                        ];
                        CodeInstruction[] insertAfter = [
                            new CodeInstruction(OpCodes.Br_S, isNotTerminalMapLabel),
                            new CodeInstruction(OpCodes.Call, m_Plugin_GetZoomOrthographicSize).WithLabels(isTerminalMapLabel),
                        ];
                        instructionsList.InsertRange(i, insertBefore);
                        instructionsList.InsertRange(i + insertBefore.Length + 1, insertAfter);

                        setOrthographicSize = setOrthographicSize + insertBefore.Length + insertAfter.Length + 1;
                        break;
                    }
                }
            }

            // Transform:
            //   StartOfRound.radarCanvas
            // into:
            //   (__instance == Plugin.terminalMapRenderer ? Plugin.terminalMapScreenUICanvas : StartOfRound.radarCanvas)
            instructionsList.InsertTerminalField(generator, new(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas);

            return instructionsList;
        }
    }
}
