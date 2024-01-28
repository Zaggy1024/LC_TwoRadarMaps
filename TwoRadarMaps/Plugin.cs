using System;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

using TwoRadarMaps.Patches;
using System.Globalization;

namespace TwoRadarMaps
{
    [BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string MOD_NAME = "TwoRadarMaps";
        private const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        private const string MOD_VERSION = "1.1.2";

        private readonly Harmony harmony = new Harmony(MOD_UNIQUE_NAME);

        public static Plugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;

        public static ConfigEntry<bool> EnableZoom;
        public static ConfigEntry<string> ZoomLevels;
        public static ConfigEntry<int> DefaultZoomLevel;

        const string DEFAULT_ZOOM_LEVELS = "19.7, 29.55, 39.4";

        public static ManualCameraRenderer terminalMapRenderer;
        public static TMPro.TextMeshProUGUI terminalMapScreenPlayerName;
        public static Canvas terminalMapScreenUICanvas;
        public static float[] terminalMapZoomLevelOptions;
        public static int terminalMapZoomLevel;

        public void Awake()
        {
            Instance = this;

            EnableZoom = Config.Bind("Zoom", "Enabled", false, "Enable 'zoom in' and 'zoom out' commands in the terminal to zoom in and out of the terminal radar map.");
            ZoomLevels = Config.Bind("Zoom", "Sizes", DEFAULT_ZOOM_LEVELS, "The orthographic sizes to use for each zoom level. A list of comma-separated numbers. Lower values indicate a smaller field of view. 100% zoom is 19.7.");
            ZoomLevels.SettingChanged += (s, e) => UpdateZoomFactors();
            DefaultZoomLevel = Config.Bind("Zoom", "DefaultLevel", 0, "The zoom factor to select by default. The first zoom level is 0.");

            harmony.PatchAll(typeof(PatchTerminal));
            harmony.PatchAll(typeof(PatchManualCameraRenderer));
            harmony.PatchAll(typeof(PatchPlayerControllerB));
            harmony.PatchAll(typeof(PatchRadarBoosterItem));

            // Enable each map's night vision light only when rendering that map camera.
            // This allows night vision to work outside the facility.
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        public static void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            var mainMapRenderer = StartOfRound.Instance?.mapScreen;
            if (mainMapRenderer == null)
                return;

            mainMapRenderer.mapCameraLight.enabled = false;
            if (terminalMapRenderer != null)
                terminalMapRenderer.mapCameraLight.enabled = false;

            ManualCameraRenderer currentMapRenderer = null;
            if (camera == mainMapRenderer.cam)
                currentMapRenderer = mainMapRenderer;
            else if (camera == terminalMapRenderer.cam)
                currentMapRenderer = terminalMapRenderer;

            if (currentMapRenderer == null)
                return;

            // Enable the night vision light only for this camera.
            currentMapRenderer.mapCameraLight.enabled = true;
        }

        public static void UpdateRadarTargets()
        {
            var mainMapRenderer = StartOfRound.Instance.mapScreen;
            terminalMapRenderer.radarTargets = mainMapRenderer.radarTargets;

            // Bug fix for vanilla: Update both map targets to their current target to force checking
            // for the validity of the selected player when the lobby is not full.
            if (StartOfRound.Instance.IsServer)
            {
                mainMapRenderer.SwitchRadarTargetAndSync(Math.Min(mainMapRenderer.targetTransformIndex, mainMapRenderer.radarTargets.Count - 1));
                terminalMapRenderer.SwitchRadarTargetAndSync(Math.Min(terminalMapRenderer.targetTransformIndex, terminalMapRenderer.radarTargets.Count - 1));
            }
        }

        public static void UpdateZoomFactors(string factors)
        {
            try
            {
                terminalMapZoomLevelOptions = factors
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

        public static void SetZoomLevel(int level)
        {
            if (terminalMapRenderer is null)
                return;

            terminalMapRenderer.mapCameraAnimator.SetTrigger("Transition");

            terminalMapZoomLevel = Math.Max(0, Math.Min(level, terminalMapZoomLevelOptions.Length - 1));
            terminalMapRenderer.cam.orthographicSize = terminalMapZoomLevelOptions[terminalMapZoomLevel];
        }

        public static void CycleTerminalMapZoom()
        {
            SetZoomLevel((terminalMapZoomLevel + 1) % terminalMapZoomLevelOptions.Length);
        }

        public static void ZoomTerminalMapIn()
        {
            SetZoomLevel(terminalMapZoomLevel - 1);
        }

        public static void ZoomTerminalMapOut()
        {
            SetZoomLevel(terminalMapZoomLevel + 1);
        }
    }
}
