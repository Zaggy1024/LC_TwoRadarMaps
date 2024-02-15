﻿using GameNetcodeStuff;
using HarmonyLib;

using TwoRadarMaps.Compatibility;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PatchPlayerControllerB
    {
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        [HarmonyAfter(OpenBodyCamsCompatibility.MOD_ID)]
        static void ConnectClientToPlayerObjectPostfix()
        {
            Plugin.UpdateRadarTargets();
            Plugin.UpdateZoomFactors();

            OpenBodyCamsCompatibility.InitializeAtStartOfGame();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        static void SendNewPlayerValuesClientRpcPostfix(ref PlayerControllerB __instance)
        {
            Plugin.UpdateRadarTargets();
        }
    }
}
