using UnityEngine;

namespace TwoRadarMaps
{
    internal class TerminalVisibilityTracker : MonoBehaviour
    {
        void OnEnable()
        {
            Plugin.Instance.Logger.LogInfo("Terminal is visible.");

            Plugin.terminalMapRenderer.cam.enabled = true;
            Plugin.terminalMapRenderer.SwitchScreenOn(true);
            Plugin.terminalMapRenderer.enabled = true;
        }

        void OnDisable()
        {
            Plugin.Instance.Logger.LogInfo("Terminal is hidden.");

            Plugin.terminalMapRenderer.SwitchScreenOn(false);
            Plugin.terminalMapRenderer.enabled = false;
            Plugin.terminalMapRenderer.cam.enabled = false;
        }
    }
}
