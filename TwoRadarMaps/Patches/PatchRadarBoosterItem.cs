using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(RadarBoosterItem))]
    internal class PatchRadarBoosterItem
    {
        [HarmonyPostfix]
        [HarmonyPatch("AddBoosterToRadar")]
        static void AddBoosterToRadarPostfix(ref RadarBoosterItem __instance)
        {
            Plugin.UpdateRadarTargets();
        }

        [HarmonyPostfix]
        [HarmonyPatch("RemoveBoosterFromRadar")]
        static void RemoveBoosterFromRadarPostfix(ref RadarBoosterItem __instance)
        {
            Plugin.UpdateRadarTargets();
        }
    }
}
