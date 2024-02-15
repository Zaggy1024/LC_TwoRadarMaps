using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(RadarBoosterItem))]
    internal class PatchRadarBoosterItem
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(RadarBoosterItem.AddBoosterToRadar))]
        static void AddBoosterToRadarPostfix(ref RadarBoosterItem __instance)
        {
            Plugin.UpdateRadarTargets();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(RadarBoosterItem.RemoveBoosterFromRadar))]
        static void RemoveBoosterFromRadarPostfix(ref RadarBoosterItem __instance)
        {
            Plugin.UpdateRadarTargets();
        }
    }
}
