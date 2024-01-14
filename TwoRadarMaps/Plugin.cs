using System;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

using TwoRadarMaps.Patches;

namespace TwoRadarMaps
{
    [BepInPlugin(MOD_UNIQUE_NAME, MOD_NAME, MOD_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string MOD_NAME = "TwoRadarMaps";
        private const string MOD_UNIQUE_NAME = "Zaggy1024." + MOD_NAME;
        private const string MOD_VERSION = "1.0.2";

        private readonly Harmony harmony = new Harmony(MOD_UNIQUE_NAME);

        public static Plugin Instance { get; private set; }
        public new ManualLogSource Logger => base.Logger;

        public static ManualCameraRenderer terminalMapRenderer;
        public static TMPro.TextMeshProUGUI terminalMapScreenPlayerName;
        public static Canvas terminalMapScreenUICanvas;

        public void Awake()
        {
            Instance = this;

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
    }
}
