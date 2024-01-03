using System.Collections;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ManualCameraRenderer))]
    internal class PatchManualCameraRenderer
    {
        [HarmonyPostfix]
        [HarmonyPatch("updateMapTarget")]
        static IEnumerator updateTargetMapPostfix(IEnumerator __result)
        {
            while (__result.MoveNext())
                yield return __result.Current;
            Plugin.UpdateCurrentMonitoredPlayer();
        }

        [HarmonyPostfix]
        [HarmonyPatch("ChangeNameOfTargetTransform")]
        static void ChangeNameOfTargetTransformPostfix(ref ManualCameraRenderer __instance)
        {
            Plugin.UpdateRadarTargets();
        }

        [HarmonyPostfix]
        [HarmonyPatch("MapCameraFocusOnPosition")]
        static void MapCameraFocusOnPositionPostfix(ref ManualCameraRenderer __instance)
        {
            // This postfix will mirror the behavior of the function with regard to the
            // the radar UI canvas's plane distance, as that will be set for both maps otherwise.
            // Here we will take into account which one is actually visible at the moment.
            if (!(GameNetworkManager.Instance.localPlayerController == null))
            {
                var map = StartOfRound.Instance.mapScreen;
                if (StartOfRound.Instance.radarCanvas.worldCamera != StartOfRound.Instance.mapScreen.cam)
                    map = Plugin.terminalMapRenderer;

                if (map.targetedPlayer != null && map.targetedPlayer.isInHangarShipRoom)
                    StartOfRound.Instance.radarCanvas.planeDistance = -0.93f;
                else
                    StartOfRound.Instance.radarCanvas.planeDistance = -2.4f;
            }
        }
    }
}
