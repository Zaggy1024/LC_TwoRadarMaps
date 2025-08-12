using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using UnityEngine;

using OpenBodyCams;
using OpenBodyCams.API;

namespace TwoRadarMaps.Compatibility;

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

        var terminalBodyCam = BodyCam.CreateBodyCam(Plugin.Terminal.gameObject, null, Plugin.TerminalMapRenderer);
        TerminalBodyCam = terminalBodyCam;

        ShipObjects.MainBodyCam.OnRenderTextureCreated -= OpenBodyCams.TerminalCommands.SetRenderTexture;
        terminalBodyCam.OnRenderTextureCreated += SetBodyCamTexture;
        SetBodyCamTexture(terminalBodyCam.GetCamera().targetTexture);

        ShipObjects.MainBodyCam.OnBlankedSet -= OpenBodyCams.TerminalCommands.SetBodyCamBlanked;
        terminalBodyCam.OnBlankedSet += OpenBodyCams.TerminalCommands.SetBodyCamBlanked;

        UpdateTerminalBodyCamSettings();
        Plugin.BodyCamHorizontalResolution.SettingChanged += (_, _) => UpdateTerminalBodyCamSettings();

        OpenBodyCams.TerminalCommands.PiPImage.GetComponent<TerminalBodyCamVisibilityTracker>().BodyCamToActivate = terminalBodyCam;
    }

    private static void UpdateTerminalBodyCamSettings()
    {
        try
        {
            UpdateTerminalBodyCamResolution();
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UpdateTerminalBodyCamResolution()
    {
        var horizontalResolution = Plugin.BodyCamHorizontalResolution.Value;
        var resolution = new Vector2Int(horizontalResolution, horizontalResolution * 3 / 4);
        ((BodyCamComponent)TerminalBodyCam).Resolution = resolution;
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
}
