using GameNetcodeStuff;
using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        static void ConnectClientToPlayerObjectPostfix()
        {
            Plugin.UpdateRadarTargets();
            Plugin.UpdateZoomFactors();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        static void SendNewPlayerValuesClientRpcPostfix(ref PlayerControllerB __instance)
        {
            Plugin.UpdateRadarTargets();
        }
    }
}
