using System;
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

namespace TwoRadarMaps
{
    [BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
    [BepInDependency(OpenBodyCamsCompatibility.MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EnhancedRadarBoosterCompatibility.MOD_ID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private const string MOD_NAME = "TwoRadarMaps";
        private const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        private const string MOD_VERSION = "1.2.3";

        internal static readonly Harmony Harmony = new(MOD_UNIQUE_NAME);

        public static Plugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;

        public static ConfigEntry<bool> EnableZoom;
        public static ConfigEntry<string> ZoomLevels;
        public static ConfigEntry<int> DefaultZoomLevel;
        public static ConfigEntry<FilterMode> TextureFiltering;

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
            Harmony.PatchAll(typeof(PatchManualCameraRenderer));
            Harmony.PatchAll(typeof(PatchPlayerControllerB));
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

            mainMapRenderer.mapCameraLight.enabled = false;
            if (TerminalMapRenderer != null)
                TerminalMapRenderer.mapCameraLight.enabled = false;

            ManualCameraRenderer currentMapRenderer = null;
            if (camera == mainMapRenderer.cam)
                currentMapRenderer = mainMapRenderer;
            else if (camera == TerminalMapRenderer.cam)
                currentMapRenderer = TerminalMapRenderer;

            if (currentMapRenderer == null)
                return;

            // Enable the night vision light only for this camera.
            currentMapRenderer.mapCameraLight.enabled = true;
        }

        private static bool TargetIsValid(Transform targetTransform)
        {
            if (targetTransform == null)
                return false;
            var player = targetTransform.transform.GetComponent<PlayerControllerB>();
            if (player == null)
                return true;
            return player.isPlayerControlled || player.isPlayerDead || player.redirectToEnemy != null;
        }

        internal static void SetTargetIndex(ManualCameraRenderer mapRenderer, int targetIndex, bool setText = true)
        {
            if (targetIndex >= mapRenderer.radarTargets.Count)
                return;

            Instance.Logger.LogInfo($"Set {mapRenderer.name}'s target index to {targetIndex} ({mapRenderer.radarTargets[targetIndex].name}) / {mapRenderer.radarTargets.Count}.");
            var stack = new System.Diagnostics.StackTrace();
            foreach (var frame in stack.GetFrames())
                Instance.Logger.LogInfo($"  {frame.GetMethod().DeclaringType.FullName}.{frame.GetMethod()}");
            mapRenderer.targetTransformIndex = targetIndex;
            mapRenderer.targetedPlayer = mapRenderer.radarTargets[targetIndex].transform.GetComponent<PlayerControllerB>();

            if (!setText)
                return;

            var monitoringText = "MONITORING: " + mapRenderer.radarTargets[targetIndex].name;
            if (mapRenderer == TerminalMapRenderer)
                TerminalMapScreenPlayerName.text = monitoringText;
            else
                StartOfRound.Instance.mapScreenPlayerName.text = monitoringText;
        }

        internal static void EnsureMapRendererHasValidTarget(ManualCameraRenderer mapRenderer)
        {
            var targetCount = mapRenderer.radarTargets.Count;
            for (int i = 0; i < targetCount; i++)
            {
                var targetIndex = (mapRenderer.targetTransformIndex + i) % targetCount;
                if (TargetIsValid(mapRenderer.radarTargets[targetIndex]?.transform))
                {
                    SetTargetIndex(mapRenderer, targetIndex);
                    return;
                }
            }
            Instance.Logger.LogInfo($"Failed to find valid target for {mapRenderer.name}.");
            var stack = new System.Diagnostics.StackTrace();
            foreach (var frame in stack.GetFrames())
                Instance.Logger.LogInfo($"  {frame.GetMethod().DeclaringType.FullName}.{frame.GetMethod()}");
        }

        public static void EnsureAllRenderersHaveValidTargets()
        {
            EnsureMapRendererHasValidTarget(StartOfRound.Instance.mapScreen);
            EnsureMapRendererHasValidTarget(TerminalMapRenderer);
        }

        public static void UpdateTerminalMapTargetList()
        {
            TerminalMapRenderer.radarTargets = StartOfRound.Instance.mapScreen.radarTargets;
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

            var oldIndex = mapRenderer.targetTransformIndex;
            SetTargetIndex(mapRenderer, targetIndex, setText: false);
            Teleporter.PressTeleportButtonOnLocalClient();
            SetTargetIndex(mapRenderer, oldIndex, setText: false);
        }
    }
}
