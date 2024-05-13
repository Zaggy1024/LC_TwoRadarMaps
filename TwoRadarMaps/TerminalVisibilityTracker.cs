using UnityEngine;

namespace TwoRadarMaps
{
    internal class TerminalVisibilityTracker : MonoBehaviour
    {
        void OnEnable()
        {
            Plugin.TerminalMapRenderer.cam.enabled = true;
            Plugin.TerminalMapRenderer.SwitchScreenOn(true);
            Plugin.TerminalMapRenderer.enabled = true;
        }

        void OnDisable()
        {
            Plugin.TerminalMapRenderer.SwitchScreenOn(false);
            Plugin.TerminalMapRenderer.enabled = false;
            if (Plugin.TerminalMapRenderer.cam != null)
                Plugin.TerminalMapRenderer.cam.enabled = false;
        }
    }
}
