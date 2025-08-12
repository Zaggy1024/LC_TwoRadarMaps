using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using TwoRadarMaps.Utilities.IL;

namespace TwoRadarMaps.Patches;

[HarmonyPatch(typeof(ManualCameraRenderer))]
internal static class PatchManualCameraRenderer
{
    [HarmonyPatch(nameof(ManualCameraRenderer.updateMapTarget), MethodType.Enumerator)]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> updateMapTargetTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return new ILInjector(instructions, generator)
            .InsertTerminalField(new CodeInstruction(OpCodes.Ldloc_1), Reflection.f_StartOfRound_mapScreenPlayerName, Reflection.f_Plugin_terminalMapScreenPlayerName)
            .ReleaseInstructions();
    }

    [HarmonyPatch(nameof(ManualCameraRenderer.MapCameraFocusOnPosition))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> MapCameraFocusOnPositionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        // By default, this just sets the main map's UI canvas. Instead, ensure that the UI actually attached to
        // the map renderer's camera has its plane distance set to match the near clip plane.
        return new ILInjector(instructions, generator)
            .InsertTerminalField(new CodeInstruction(OpCodes.Ldarg_0), Reflection.f_StartOfRound_radarCanvas, Reflection.f_Plugin_terminalMapScreenUICanvas)
            .ReleaseInstructions();
    }

    [HarmonyPatch(nameof(ManualCameraRenderer.ChangeNameOfTargetTransform))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ChangeNameOfTargetTransformTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // Play a transition and apply the new name when a matching transform is found.
        //   radarTargets[i].name = newName;
        // + OnTargetChanged(i);
        var injector = new ILInjector(instructions)
            .Find([
                ILMatcher.Ldarg(0),
                ILMatcher.Ldfld(Reflection.f_ManualCameraRenderer_radarTargets),
                ILMatcher.Ldloc().CaptureAs(out var loadIndex),
                ILMatcher.Callvirt(Reflection.m_List_TransformAndName_get_Item),
                ILMatcher.Ldarg(),
                ILMatcher.Stfld(Reflection.f_TransformAndName_name),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Could not find setting of a radar target name in {nameof(ManualCameraRenderer)}.{nameof(ManualCameraRenderer.ChangeNameOfTargetTransform)}().");
            return instructions;
        }

        return injector
            .Insert([
                loadIndex,
                new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(OnTargetNameChanged), BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)])),
            ])
            .ReleaseInstructions();
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
        var injector = new ILInjector(instructions, generator)
            .Find([
                ILMatcher.Ldarg(0),
                ILMatcher.Ldfld(Reflection.f_ManualCameraRenderer_radarTargets),
                ILMatcher.Ldloc().CaptureAs(out var loadRadarTargetIndex),
                ILMatcher.Callvirt(typeof(List<TransformAndName>).GetMethod(nameof(List<TransformAndName>.RemoveAt), [typeof(int)])),
            ]);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"Failed to find the removal of a target from the list in {nameof(ManualCameraRenderer)}.{nameof(ManualCameraRenderer.RemoveTargetFromRadar)}.");
            return instructions;
        }

        injector
            .Find([
                ILMatcher.Ldarg(0),
                ILMatcher.Ldfld(typeof(ManualCameraRenderer).GetField(nameof(ManualCameraRenderer.targetTransformIndex))),
                ILMatcher.Ldarg(0),
                ILMatcher.Ldfld(Reflection.f_ManualCameraRenderer_radarTargets),
                ILMatcher.Callvirt(typeof(List<TransformAndName>).GetMethod($"get_{nameof(List<TransformAndName>.Count)}")),
                ILMatcher.Opcode(OpCodes.Blt).CaptureOperandAs(out Label targetIsInBoundsLabel),
            ])
            .FindLabel(targetIsInBoundsLabel);

        if (!injector.IsValid)
        {
            Plugin.Instance.Logger.LogError($"No check for out-of-bounds target was found in {nameof(ManualCameraRenderer)}.{nameof(ManualCameraRenderer.RemoveTargetFromRadar)}.");
            return instructions;
        }

        return injector
            .ReplaceLastMatch([
                new CodeInstruction(loadRadarTargetIndex),
                new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(RadarTargetWasRemoved), BindingFlags.NonPublic | BindingFlags.Static, [typeof(int)])),
            ])
            .ReleaseInstructions();
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
            if (instruction.StoresField(Reflection.f_ManualCameraRenderer_radarTargets))
                yield return new CodeInstruction(OpCodes.Call, typeof(PatchManualCameraRenderer).GetMethod(nameof(ApplySortedTargets), BindingFlags.NonPublic | BindingFlags.Static, [typeof(ManualCameraRenderer), typeof(List<TransformAndName>)]));
            else
                yield return instruction;
        }
    }

    private static void OnTargetListResorted(ManualCameraRenderer mapRenderer, List<TransformAndName> sortedTargets)
    {
        var initialIndex = mapRenderer.targetTransformIndex;
        var initialTarget = mapRenderer.radarTargets[initialIndex].transform;

        if (mapRenderer.radarTargets[initialIndex] == sortedTargets[initialIndex])
            return;

        for (var i = 0; i < sortedTargets.Count; i++)
        {
            if (sortedTargets[i].transform == initialTarget)
            {
                Plugin.SetTargetIndex(mapRenderer, i);
                return;
            }
        }

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
    [HarmonyPostfix]
    private static void MeetsCameraEnabledConditionsPostfix(ManualCameraRenderer __instance, ref bool __result)
    {
        if (__instance != Plugin.TerminalMapRenderer)
            return;
        if (!__result)
            return;
        var image = Plugin.Terminal.terminalImage;
        __result = image.isActiveAndEnabled && image.texture == __instance.cam.targetTexture;
    }
}
