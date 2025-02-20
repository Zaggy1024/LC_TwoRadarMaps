using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

using TwoRadarMaps.Patches;
using TwoRadarMaps.Compatibility;

namespace TwoRadarMaps;

[BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
[BepInDependency(OpenBodyCamsCompatibility.MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(EnhancedRadarBoosterCompatibility.ENHANCED_RADAR_BOOSTER_MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(EnhancedRadarBoosterCompatibility.IMMERSIVE_COMPANY_MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(LobbyControlCompatibility.MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    private const string MOD_NAME = "TwoRadarMaps";
    private const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
    private const string MOD_VERSION = "1.4.3";

    internal static readonly Harmony Harmony = new(MOD_UNIQUE_NAME);

    public static Plugin Instance { get; private set; }
    public new ManualLogSource Logger => base.Logger;

    public static ConfigEntry<FilterMode> TextureFiltering;

    public static ConfigEntry<int> BodyCamHorizontalResolution;

    public static ConfigEntry<bool> EnableZoom;
    public static ConfigEntry<string> ZoomLevels;
    public static ConfigEntry<int> DefaultZoomLevel;

    public static ConfigEntry<bool> EnableTeleportCommand;
    public static ConfigEntry<bool> EnableTeleportCommandShorthand;

    const string DEFAULT_ZOOM_LEVELS = "19.7, 29.55, 39.4";

    internal static Terminal Terminal;
    internal static ShipTeleporter Teleporter;

    public static ManualCameraRenderer TerminalMapRenderer;
    public static TMPro.TextMeshProUGUI TerminalMapScreenPlayerName;
    public static Canvas TerminalMapScreenUICanvas;
    public static float[] TerminalMapZoomLevelOptions;
    public static int TerminalMapZoomLevel;

    public void Awake()
    {
        Instance = this;

        TextureFiltering = Config.Bind("Rendering", "TextureFiltering", FilterMode.Point,
            "The filtering mode to apply to the map (and the body cam if present).\n\n" +
            "Point will result in sharp edges on pixels.\n" +
            "Bilinear and Trilinear will result in smooth transitions between pixels.");
        TextureFiltering.SettingChanged += (_, _) =>
        {
            TerminalMapRenderer.cam.targetTexture.filterMode = TextureFiltering.Value;
            OpenBodyCamsCompatibility.UpdateBodyCamTexture();
        };

        BodyCamHorizontalResolution = Config.Bind("Compatibility", "BodyCamHorizontalResolution", 170,
            "The horizontal resolution to use for the picture-in-picture body cam when it is enabled in OpenBodyCams 2.1.0+.\n\n" +
            "The vertical resolution will be calculated based on a 4:3 aspect ratio.");

        EnableZoom = Config.Bind("Zoom", "Enabled", false, "Enable 'zoom in' and 'zoom out' commands in the terminal to zoom in and out of the terminal radar map.");
        EnableZoom.SettingChanged += (_, _) => TerminalCommands.Initialize();
        ZoomLevels = Config.Bind("Zoom", "Sizes", DEFAULT_ZOOM_LEVELS,
            "The orthographic sizes to use for each zoom level.\n" +
            "A list of comma-separated numbers.\n" +
            "Lower values indicate a smaller field of view.\n" +
            "100% zoom is 19.7.");
        ZoomLevels.SettingChanged += (_, _) => UpdateZoomFactors();
        DefaultZoomLevel = Config.Bind("Zoom", "DefaultLevel", 0, "The zoom factor to select by default. The first zoom level is 0.");

        EnableTeleportCommand = Config.Bind("TeleportCommand", "Enabled", false, "Enable an 'activate teleport' command in the terminal. A player can be specified to teleport them instead of the target of the terminal's map.");
        EnableTeleportCommand.SettingChanged += (_, _) => TerminalCommands.Initialize();
        EnableTeleportCommandShorthand = Config.Bind("TeleportCommand", "ShorthandEnabled", true, "Enable a 'tp' shorthand for the 'activate teleport' command. Will only function if the longhand command is enabled.");
        EnableTeleportCommandShorthand.SettingChanged += (_, _) => TerminalCommands.Initialize();

        Harmony.PatchAll(typeof(PatchTerminal));
        Harmony.PatchAll(typeof(PatchVanillaBugs));
        Harmony.PatchAll(typeof(PatchManualCameraRenderer));
        Harmony.PatchAll(typeof(PatchShipTeleporter));

        // Enable each map's night vision light only when rendering that map camera.
        // This allows night vision to work outside the facility.
        RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;

        OpenBodyCamsCompatibility.Initialize();
        EnhancedRadarBoosterCompatibility.Initialize(Harmony);
    }

    public static void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        var mainMapRenderer = StartOfRound.Instance?.mapScreen;
        if (mainMapRenderer == null)
            return;

        ManualCameraRenderer currentMapRenderer = null;

        mainMapRenderer.mapCameraLight.enabled = false;
        if (camera == mainMapRenderer.cam)
            currentMapRenderer = mainMapRenderer;

        if (TerminalMapRenderer != null)
        {
            TerminalMapRenderer.mapCameraLight.enabled = false;

            if (camera == TerminalMapRenderer.cam)
                currentMapRenderer = TerminalMapRenderer;
        }

        if (currentMapRenderer == null)
            return;

        // Enable the night vision light only for this camera.
        currentMapRenderer.mapCameraLight.enabled = true;
    }

    private static bool TargetIsValid(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;
        if (!targetTransform.transform.TryGetComponent(out PlayerControllerB player))
            return true;
        return player.isPlayerControlled || player.isPlayerDead || player.redirectToEnemy != null;
    }

    internal static void SetTargetIndex(ManualCameraRenderer mapRenderer, int targetIndex)
    {
        if (targetIndex < 0)
            return;
        if (targetIndex >= mapRenderer.radarTargets.Count)
            return;

        mapRenderer.targetTransformIndex = targetIndex;
        mapRenderer.targetedPlayer = mapRenderer.radarTargets[targetIndex].transform.GetComponent<PlayerControllerB>();
    }

    internal static int GetNextValidTarget(List<TransformAndName> targets, int initialIndex)
    {
        var targetCount = targets.Count;
        for (int i = 0; i < targetCount; i++)
        {
            var targetIndex = (initialIndex + i) % targetCount;
            if (TargetIsValid(targets[targetIndex]?.transform))
                return targetIndex;
        }
        return -1;
    }

    internal static void StartTargetTransition(ManualCameraRenderer mapRenderer, int targetIndex)
    {
        if (mapRenderer.updateMapCameraCoroutine != null)
            mapRenderer.StopCoroutine(mapRenderer.updateMapCameraCoroutine);
        mapRenderer.updateMapCameraCoroutine = mapRenderer.StartCoroutine(mapRenderer.updateMapTarget(targetIndex));
    }

    internal static void EnsureMapRendererHasValidTarget(ManualCameraRenderer mapRenderer)
    {
        var targetIndex = GetNextValidTarget(mapRenderer.radarTargets, mapRenderer.targetTransformIndex);
        if (targetIndex == -1)
            return;
        StartTargetTransition(mapRenderer, targetIndex);
    }

    public static void EnsureAllMapRenderersHaveValidTargets()
    {
        if (StartOfRound.Instance.mapScreen.IsOwner)
            EnsureMapRendererHasValidTarget(StartOfRound.Instance.mapScreen);
        EnsureMapRendererHasValidTarget(TerminalMapRenderer);
    }

    public static void UpdateZoomFactors(string factors)
    {
        try
        {
            TerminalMapZoomLevelOptions = factors
                .Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
        }
        catch (Exception e)
        {
            Instance.Logger.LogError($"Failed to parse terminal map radar zoom levels: {e}");
            UpdateZoomFactors(DEFAULT_ZOOM_LEVELS);
        }
        SetZoomLevel(DefaultZoomLevel.Value);
    }

    public static void UpdateZoomFactors()
    {
        UpdateZoomFactors(ZoomLevels.Value);
    }

    public static float GetZoomOrthographicSize()
    {
        return TerminalMapZoomLevelOptions[TerminalMapZoomLevel];
    }

    public static void SetZoomLevel(int level)
    {
        if (TerminalMapRenderer == null)
            return;

        TerminalMapRenderer.mapCameraAnimator.SetTrigger("Transition");

        TerminalMapZoomLevel = Math.Max(0, Math.Min(level, TerminalMapZoomLevelOptions.Length - 1));
        TerminalMapRenderer.cam.orthographicSize = GetZoomOrthographicSize();
    }

    public static void CycleTerminalMapZoom()
    {
        SetZoomLevel((TerminalMapZoomLevel + 1) % TerminalMapZoomLevelOptions.Length);
    }

    public static void ZoomTerminalMapIn()
    {
        SetZoomLevel(TerminalMapZoomLevel - 1);
    }

    public static void ZoomTerminalMapOut()
    {
        SetZoomLevel(TerminalMapZoomLevel + 1);
    }

    public static void TeleportTarget(int targetIndex)
    {
        var mapRenderer = StartOfRound.Instance.mapScreen;
        if (mapRenderer.targetTransformIndex >= mapRenderer.radarTargets.Count)
        {
            Instance.Logger.LogError($"Attempted to teleport target #{targetIndex} which is out of bounds of the {mapRenderer.radarTargets.Count} targets available.");
            return;
        }
        if (Teleporter == null)
        {
            Instance.Logger.LogError($"Attempted to teleport target #{targetIndex} ({mapRenderer.radarTargets[targetIndex].name}) with no teleporter.");
            return;
        }
        if (Teleporter.cooldownTime > 0)
            return;

        var oldIndex = mapRenderer.targetTransformIndex;
        SetTargetIndex(mapRenderer, targetIndex);
        Teleporter.PressTeleportButtonOnLocalClient();
        SetTargetIndex(mapRenderer, oldIndex);
    }
}
