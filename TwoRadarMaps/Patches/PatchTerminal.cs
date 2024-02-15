using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    internal class PatchTerminal
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Terminal.Awake))]
        public static void AwakePostfix(Terminal __instance)
        {
            Commands.Initialize(__instance.terminalNodes);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Terminal.Start))]
        static void StartPrefix(ref Terminal __instance)
        {
            Plugin.Terminal = __instance;
            var terminalObject = Plugin.Terminal.gameObject;
            var viewMonitorNode = Plugin.Terminal.terminalNodes.allKeywords.First(keyword => keyword.word == "view")?.compatibleNouns.First(noun => noun.noun.word == "monitor")?.result;

            // Existing objects/components
            var itemSystems = GameObject.Find("Systems/GameSystems/ItemSystems");
            if (itemSystems == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the ItemSystems object.");
                return;
            }

            var mainMapCamera = itemSystems.transform.Find("MapCamera")?.gameObject;
            if (mainMapCamera == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the default map camera.");
                return;
            }

            var mainMapRenderer = StartOfRound.Instance?.mapScreen;
            if (mainMapRenderer == null)
            {
                Plugin.Instance.Logger.LogError("Default map camera renderer is null.");
                return;
            }

            var mainMapScreenUI = itemSystems.transform.Find("MapScreenUI")?.gameObject;
            if (mainMapScreenUI == null)
            {
                Plugin.Instance.Logger.LogError("Could not find the map screen UI.");
                return;
            }

            // New objects/components for terminal map rendering
            var terminalMapCameraObject = Object.Instantiate(mainMapCamera, itemSystems.transform, false);
            var terminalMapCamera = terminalMapCameraObject?.GetComponent<Camera>();
            if (terminalMapCamera == null)
            {
                Plugin.Instance.Logger.LogError("Failed to clone the default map camera.");
                return;
            }
            terminalMapCameraObject.name = "TerminalMapCamera";

            var terminalMapScreenUI = UnityEngine.Object.Instantiate(mainMapScreenUI, itemSystems.transform, false);
            var terminalMapScreenUICanvas = terminalMapScreenUI?.GetComponent<Canvas>();
            terminalMapScreenUICanvas.worldCamera = terminalMapCamera;
            if (terminalMapScreenUICanvas == null)
            {
                Plugin.Instance.Logger.LogError("Failed to clone the default map's UI.");
                return;
            }
            terminalMapScreenUI.name = "TerminalMapScreenUI";

            var planetVideo = terminalMapScreenUI.transform.Find("PlanetVideoReel")?.gameObject;
            var planetDescription = terminalMapScreenUI.transform.Find("PlanetDescription")?.gameObject;
            if (planetVideo == null || planetDescription == null)
            {
                Plugin.Instance.Logger.LogError("Failed to delete duplicated planet description.");
                return;
            }
            Object.Destroy(planetVideo);
            Object.Destroy(planetDescription);

            var terminalMapShipArrowUI = terminalMapScreenUI.transform.Find("ArrowUI")?.gameObject;
            var terminalMapShipArrowPointer = terminalMapShipArrowUI.transform.Find("ArrowContainer");
            if (terminalMapShipArrowUI == null || terminalMapShipArrowPointer == null)
            {
                Plugin.Instance.Logger.LogError("Failed to get cloned ship arrow pointer.");
                return;
            }

            var terminalMapPlayerName = terminalMapScreenUI.transform.Find("PlayerBeingMonitored")?.GetComponent<TextMeshProUGUI>();
            terminalMapPlayerName.enabled = true;
            if (terminalMapShipArrowUI == null || terminalMapShipArrowPointer == null)
            {
                Plugin.Instance.Logger.LogError("Failed to get cloned 'MONITORING:' text UI.");
                return;
            }

            var newAnimator = terminalMapCameraObject.GetComponentInChildren<Animator>();
            if (newAnimator == null)
            {
                Plugin.Instance.Logger.LogError("Failed to find new map flash animation.");
                return;
            }

            var newLight = terminalMapCameraObject.GetComponentInChildren<Light>();
            if (newLight == null)
            {
                Plugin.Instance.Logger.LogError("Failed to find new map night vision light.");
                return;
            }

            var terminalMesh = GameObject.Find("Environment/HangarShip/Terminal")?.GetComponentInChildren<MeshRenderer>();
            if (terminalMesh == null)
            {
                Plugin.Instance.Logger.LogError("Failed to find terminal object mesh.");
                return;
            }

            var terminalMapRenderer = terminalObject.AddComponent<ManualCameraRenderer>();
            terminalMapRenderer.enabled = false;

            terminalMapRenderer.cam = terminalMapCamera;
            terminalMapRenderer.mapCamera = terminalMapCamera;
            terminalMapRenderer.cam.targetTexture = new RenderTexture(mainMapRenderer.cam.targetTexture);

            // The map renderer only enables the camera if the mesh is visible, so set the mesh to the terminal mesh.
            // Calling SwitchScreenOn swaps the specified material on 'mesh' between on and off screen materials,
            // so make this a no-op by setting them to the existing material.
            terminalMapRenderer.mesh = terminalMesh;
            terminalMapRenderer.materialIndex = 0;
            terminalMapRenderer.onScreenMat = terminalMapRenderer.mesh.sharedMaterials[terminalMapRenderer.materialIndex];
            terminalMapRenderer.offScreenMat = terminalMapRenderer.onScreenMat;

            // Our terminal map will enable and disable the arrow UI when it is active.
            terminalMapRenderer.shipArrowUI = terminalMapShipArrowUI;
            terminalMapRenderer.shipArrowPointer = terminalMapShipArrowPointer;

            terminalMapRenderer.mapCameraAnimator = newAnimator;
            terminalMapRenderer.mapCameraLight = newLight;

            viewMonitorNode.displayTexture = terminalMapRenderer.cam.targetTexture;

            Plugin.Instance.Logger.LogInfo($"Terminal node '{viewMonitorNode.name}' will now use a separate texture.");
            Plugin.terminalMapRenderer = terminalMapRenderer;
            Plugin.terminalMapScreenUICanvas = terminalMapScreenUICanvas;
            Plugin.terminalMapScreenPlayerName = terminalMapPlayerName;

            // Disable the camera until the player interacts with the terminal.
            Plugin.terminalMapRenderer.SwitchScreenOn(false);
            Plugin.terminalMapRenderer.cam.enabled = false;

            Plugin.Terminal.terminalUIScreen.gameObject.AddComponent<TerminalVisibilityTracker>();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(Terminal.RunTerminalEvents))]
        static IEnumerable<CodeInstruction> TranspileRunTerminalEvents(IEnumerable<CodeInstruction> instructions)
        {
            return Common.TranspileReplaceMainWithTerminalMapRenderer(instructions);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(Terminal.ParsePlayerSentence))]
        static IEnumerable<CodeInstruction> TranspileParsePlayerSentence(IEnumerable<CodeInstruction> instructions)
        {
            return Common.TranspileReplaceMainWithTerminalMapRenderer(instructions);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Terminal.RunTerminalEvents))]
        static bool RunTerminalEventsPrefix(TerminalNode __0)
        {
            return Commands.ProcessNode(__0);
        }
    }
}
