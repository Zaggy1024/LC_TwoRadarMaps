using UnityEngine;

namespace TwoRadarMaps
{
    internal class TerminalVisibilityTracker : MonoBehaviour
    {
        void OnEnable()
        {
            Plugin.Instance.Logger.LogInfo("Terminal is visible.");

            Plugin.TerminalMapRenderer.cam.enabled = true;
            Plugin.TerminalMapRenderer.SwitchScreenOn(true);
            Plugin.TerminalMapRenderer.enabled = true;
        }

        void OnDisable()
        {
            Plugin.Instance.Logger.LogInfo("Terminal is hidden.");

            Plugin.TerminalMapRenderer.SwitchScreenOn(false);
            Plugin.TerminalMapRenderer.enabled = false;
            Plugin.TerminalMapRenderer.cam.enabled = false;
        }
    }
}
