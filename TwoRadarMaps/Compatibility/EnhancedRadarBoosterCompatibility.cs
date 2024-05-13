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
using ImmersiveCompany.Patches;

namespace TwoRadarMaps.Compatibility
{
    internal static class EnhancedRadarBoosterCompatibility
    {
        internal const string ENHANCED_RADAR_BOOSTER_MOD_ID = "MrHydralisk.EnhancedRadarBooster";
        internal const string IMMERSIVE_COMPANY_MOD_ID = "ImmersiveCompany";

        private static MethodInfo m_MapCameraFocusOnPositionPostfixTranspiler;

        internal static void Initialize(Harmony harmony)
        {
            m_MapCameraFocusOnPositionPostfixTranspiler = typeof(EnhancedRadarBoosterCompatibility).GetMethod(nameof(MapCameraFocusOnPositionPostfixTranspiler), BindingFlags.NonPublic | BindingFlags.Static, [typeof(IEnumerable<CodeInstruction>), typeof(ILGenerator), typeof(MethodBase)]);

            if (m_MapCameraFocusOnPositionPostfixTranspiler == null)
            {
                Plugin.Instance.Logger.LogError($"{nameof(MapCameraFocusOnPositionPostfixTranspiler)} was not found.");
                Plugin.Instance.Logger.LogError($"Patches for {ENHANCED_RADAR_BOOSTER_MOD_ID} and {IMMERSIVE_COMPANY_MOD_ID} cannot be applied.");
                return;
            }

            if (Chainloader.PluginInfos.ContainsKey(ENHANCED_RADAR_BOOSTER_MOD_ID))
                InitializeForEnhancedRadarBooster(harmony);

            if (Chainloader.PluginInfos.ContainsKey(IMMERSIVE_COMPANY_MOD_ID))
                InitializeForImmersiveCompany(harmony);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeForEnhancedRadarBooster(Harmony harmony)
        {
            var m_MapCameraFocusOnPosition_Postfix = typeof(HarmonyPatches).GetMethod(nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(ManualCameraRenderer)], null);
            if (m_MapCameraFocusOnPosition_Postfix == null)
            {
                Plugin.Instance.Logger.LogError($"{ENHANCED_RADAR_BOOSTER_MOD_ID} is present, but {nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix)} was not found.");
                return;
            }
            try
            {
                harmony
                    .CreateProcessor(m_MapCameraFocusOnPosition_Postfix)
                    .AddTranspiler(m_MapCameraFocusOnPositionPostfixTranspiler)
                    .Patch();
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError($"Failed to transpile {ENHANCED_RADAR_BOOSTER_MOD_ID}'s {nameof(HarmonyPatches.MCR_MapCameraFocusOnPosition_Postfix)}");
                Plugin.Instance.Logger.LogError(e);
            }

            Plugin.Instance.Logger.LogInfo($"Finished patching EnhancedRadarBooster for compatibility with the zoom command.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeForImmersiveCompany(Harmony harmony)
        {
            var m_ManualCameraRendererPatch_MapCameraFocusPatch = typeof(ManualCameraRendererPatch).GetMethod(nameof(ManualCameraRendererPatch.MapCameraFocusPatch), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, [typeof(ManualCameraRenderer)], null);
            if (m_ManualCameraRendererPatch_MapCameraFocusPatch == null)
            {
                Plugin.Instance.Logger.LogError($"{IMMERSIVE_COMPANY_MOD_ID} is present, but {nameof(ManualCameraRendererPatch.MapCameraFocusPatch)} was not found.");
                return;
            }
            try
            {
                harmony
                    .CreateProcessor(m_ManualCameraRendererPatch_MapCameraFocusPatch)
                    .AddTranspiler(m_MapCameraFocusOnPositionPostfixTranspiler)
                    .Patch();
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.LogError($"Failed to transpile {IMMERSIVE_COMPANY_MOD_ID}'s {nameof(ManualCameraRendererPatch.MapCameraFocusPatch)}");
                Plugin.Instance.Logger.LogError(e);
            }

            Plugin.Instance.Logger.LogInfo($"Finished patching ImmersiveCompany for compatibility with the zoom command.");
        }

        private static IEnumerable<CodeInstruction> MapCameraFocusOnPositionPostfixTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
        {
            var m_Camera_set_orthographicSize = typeof(Camera).GetMethod($"set_{nameof(Camera.orthographicSize)}");
            var f_TerminalCommands_CycleZoomNode = typeof(TerminalCommands).GetField(nameof(TerminalCommands.CycleZoomNode));

            var instructionsList = instructions.ToList();

            // Find:
            //   __instance.mapCamera.orthographicSize = [value];
            // and transform it into:
            //   if (__instance == StartOfRound.Instance.mapScreen)
            //     __instance.mapCamera.orthographicSize = [value];
            var setOrthographicSize = 0;
            while (true)
            {
                setOrthographicSize = instructionsList.FindIndex(setOrthographicSize, insn => insn.Calls(m_Camera_set_orthographicSize));

                if (setOrthographicSize == -1)
                    break;

                var skipPopsLabel = generator.DefineLabel();
                instructionsList[setOrthographicSize].labels.Add(skipPopsLabel);

                var skipAssignmentLabel = generator.DefineLabel();
                instructionsList[setOrthographicSize + 1].labels.Add(skipAssignmentLabel);

                CodeInstruction[] instructionsToInsert = [
                    new(OpCodes.Ldsfld, f_TerminalCommands_CycleZoomNode),
                    new(OpCodes.Brfalse_S, skipPopsLabel),
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                    new(OpCodes.Ldfld, Reflection.f_StartOfRound_mapScreen),
                    new(OpCodes.Beq_S, skipPopsLabel),
                    new(OpCodes.Pop),
                    new(OpCodes.Pop),
                    new(OpCodes.Br_S, skipAssignmentLabel),
                ];

                instructionsList.InsertRange(setOrthographicSize, instructionsToInsert);
                setOrthographicSize += instructionsToInsert.Length + 1;
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
