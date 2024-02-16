using System.Runtime.CompilerServices;

using UnityEngine;

using OpenBodyCams;
using BepInEx.Bootstrap;
using OpenBodyCams.API;

namespace TwoRadarMaps.Compatibility
{
    internal static class OpenBodyCamsCompatibility
    {
        internal const string MOD_ID = "Zaggy1024.OpenBodyCams";

        public static MonoBehaviour TerminalBodyCam;

        private static bool ShouldUseCompatibilityMode()
        {
            if (!Chainloader.PluginInfos.ContainsKey(MOD_ID))
                return false;
            // The "view bodycam" command was introduced in OpenBodyCams v1.2.0.
            if (Chainloader.PluginInfos[MOD_ID].Metadata.Version < new System.Version(1, 2, 0))
                return false;
            return true;
        }

        internal static void Initialize()
        {
            if (ShouldUseCompatibilityMode())
                InitializeInternal();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeInternal()
        {
            // Reinitialize the bodycam if 
            OpenBodyCams.Plugin.TerminalPiPBodyCamEnabled.SettingChanged += (_, _) => InitializeAtStartOfGameInternal();
            OpenBodyCams.Plugin.TerminalPiPPosition.SettingChanged += (_, _) => InitializeAtStartOfGameInternal();
            OpenBodyCams.Plugin.TerminalPiPWidth.SettingChanged += (_, _) => InitializeAtStartOfGameInternal();
        }

        internal static void InitializeAtStartOfGame()
        {
            if (ShouldUseCompatibilityMode())
                InitializeAtStartOfGameInternal();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeAtStartOfGameInternal()
        {
            Object.Destroy(TerminalBodyCam);
            TerminalBodyCam = null;

            if (OpenBodyCams.TerminalCommands.PiPImage == null)
                return;

            var terminalBodyCam = BodyCam.CreateBodyCam(Plugin.Terminal.gameObject, null, Plugin.terminalMapRenderer);
            TerminalBodyCam = terminalBodyCam;

            ShipObjects.MainBodyCam.OnRenderTextureCreated -= OpenBodyCams.TerminalCommands.SetRenderTexture;
            terminalBodyCam.OnRenderTextureCreated += SetBodyCamTexture;
            SetBodyCamTexture(terminalBodyCam.GetCamera().targetTexture);

            ShipObjects.MainBodyCam.OnBlankedSet -= OpenBodyCams.TerminalCommands.SetBodyCamBlanked;
            terminalBodyCam.OnBlankedSet += SetBodyCamBlanked;

            OpenBodyCams.TerminalCommands.PiPImage.GetComponent<TerminalBodyCamVisibilityTracker>().BodyCamToActivate = terminalBodyCam;
        }

        public static void UpdateBodyCamTexture()
        {
            UpdateBodyCamTextureInternal();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateBodyCamTextureInternal()
        {
            SetBodyCamTexture(((BodyCamComponent)TerminalBodyCam).GetCamera().targetTexture);
        }

        private static void SetBodyCamTexture(RenderTexture texture)
        {
            texture.filterMode = Plugin.TextureFiltering.Value;
            OpenBodyCams.TerminalCommands.PiPImage.texture = texture;
        }

        private static void SetBodyCamBlanked(bool blanked)
        {
            OpenBodyCams.TerminalCommands.PiPImage.color = blanked ? Color.black : Color.white;
        }
    }
}
