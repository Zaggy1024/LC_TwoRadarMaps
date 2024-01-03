using GameNetcodeStuff;
using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        [HarmonyPostfix]
        [HarmonyPatch("SendNewPlayerValuesClientRpc")]
        static void SendNewPlayerValuesClientRpcPostfix(ref PlayerControllerB __instance)
        {
            Plugin.UpdateRadarTargets();
        }
    }
}
