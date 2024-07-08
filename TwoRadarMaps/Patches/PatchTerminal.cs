using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    internal static class PatchTerminal
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Terminal.Awake))]
        public static void AwakePostfix(Terminal __instance)
        {
            Plugin.Terminal = __instance;
            TerminalCommands.Initialize();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Terminal.Start))]
        static void StartPrefix()
        {
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
            terminalMapRenderer.cam.targetTexture = new RenderTexture(mainMapRenderer.cam.targetTexture)
            {
                name = "TerminalMapTexture",
                filterMode = Plugin.TextureFiltering.Value,
            };

            // Disable the camera until the player interacts with the terminal.
            terminalMapRenderer.cam.enabled = false;

            // Our terminal map will enable and disable the arrow UI when it is active.
            terminalMapRenderer.shipArrowUI = terminalMapShipArrowUI;
            terminalMapRenderer.shipArrowPointer = terminalMapShipArrowPointer;

            terminalMapRenderer.mapCameraAnimator = newAnimator;
            terminalMapRenderer.mapCameraLight = newLight;

            viewMonitorNode.displayTexture = terminalMapRenderer.cam.targetTexture;

            Plugin.Instance.Logger.LogInfo($"Terminal node '{viewMonitorNode.name}' will now use a separate texture.");
            Plugin.TerminalMapRenderer = terminalMapRenderer;
            Plugin.TerminalMapScreenUICanvas = terminalMapScreenUICanvas;
            Plugin.TerminalMapScreenPlayerName = terminalMapPlayerName;

            Plugin.Terminal.terminalUIScreen.gameObject.AddComponent<TerminalVisibilityTracker>();

            // Vanilla bugfix: When radar boosters are added to the targets list, the list is sorted by NetworkObject.NetworkObjectId.
            // However, players aren't initially ordered according to this field, so switching targets after activating a radar booster
            // will either select the same target again or change to a player out of order.
            //
            // Calling this will also trigger our postfix to make the terminal map renderer reference the targets list
            // in the main map.
            mainMapRenderer.SyncOrderOfRadarBoostersInList();
            Plugin.UpdateZoomFactors();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(Terminal.RunTerminalEvents))]
        [HarmonyPatch(nameof(Terminal.ParsePlayerSentence))]
        [HarmonyPatch(nameof(Terminal.CheckForPlayerNameCommand))]
        static IEnumerable<CodeInstruction> TranspileRunTerminalEvents(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();
            instructionsList.ReplaceMainMapWithTerminalMap();
            return instructionsList;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Terminal.RunTerminalEvents))]
        static bool RunTerminalEventsPrefix(TerminalNode __0)
        {
            return TerminalCommands.ProcessNode(__0);
        }
    }
}
