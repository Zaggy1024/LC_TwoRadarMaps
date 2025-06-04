using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using TwoRadarMaps.Utilities.IL;

namespace TwoRadarMaps.Patches;

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

        var terminalMapScreenUI = Object.Instantiate(mainMapScreenUI, itemSystems.transform, false);
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

        var terminalMapShipArrowUI = terminalMapScreenUI.transform.Find(mainMapRenderer.shipArrowUI.name)?.gameObject;
        var terminalMapShipArrowPointer = terminalMapShipArrowUI.transform.Find(mainMapRenderer.shipArrowPointer.name);
        if (terminalMapShipArrowUI == null || terminalMapShipArrowPointer == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get cloned ship arrow pointer.");
            return;
        }

        var terminalMapSignalLostUI = terminalMapScreenUI.transform.Find(mainMapRenderer.LostSignalUI.name)?.gameObject;
        if (terminalMapSignalLostUI == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get cloned signal lost text UI.");
            return;
        }

        var terminalMonitoringPlayerContainer = terminalMapScreenUI.transform.Find("MonitoringPlayerUIContainer");

        var terminalMapPlayerName = terminalMonitoringPlayerContainer.Find("PlayerBeingMonitored")?.GetComponent<TextMeshProUGUI>();
        if (terminalMapPlayerName == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get cloned player name text UI.");
            return;
        }
        terminalMapPlayerName.enabled = true;

        var terminalMapHeadMountedCamUI = terminalMonitoringPlayerContainer.Find(mainMapRenderer.headMountedCamUI.name)?.GetComponent<RawImage>();
        if (terminalMapHeadMountedCamUI == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get cloned head mounted cam view UI.");
            return;
        }
        terminalMapHeadMountedCamUI.enabled = true;

        var terminalMapLocalPlayerPlaceholder = terminalMonitoringPlayerContainer.Find(mainMapRenderer.localPlayerPlaceholder.name)?.GetComponent<Image>();
        if (terminalMapLocalPlayerPlaceholder == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get the local player placeholder head mounted cam view UI.");
            return;
        }
        terminalMapLocalPlayerPlaceholder.enabled = true;

        var terminalMapCompass = terminalMonitoringPlayerContainer.Find(mainMapRenderer.compassRose.name)?.GetComponent<Image>();
        if (terminalMapCompass == null)
        {
            Plugin.Instance.Logger.LogError("Failed to get cloned compass UI.");
            return;
        }
        terminalMapCompass.enabled = true;

        var newAnimator = terminalMapCameraObject.GetComponentInChildren<Animator>();
        if (newAnimator == null)
        {
            Plugin.Instance.Logger.LogError("Failed to find new map flash animation.");
            return;
        }

        var terminalMapHeadMountedCam = Object.Instantiate(mainMapRenderer.headMountedCam, itemSystems.transform, false);
        if (terminalMapHeadMountedCam == null)
        {
            Plugin.Instance.Logger.LogError("Failed to create a head mounted camera for the terminal map.");
            return;
        }
        terminalMapHeadMountedCam.name = "TerminalHeadMountedCamera";
        terminalMapHeadMountedCam.targetTexture = new RenderTexture(mainMapRenderer.headMountedCam.targetTexture);
        terminalMapHeadMountedCamUI.texture = terminalMapHeadMountedCam.targetTexture;

        var terminalExitLine = Object.Instantiate(mainMapRenderer.lineFromRadarTargetToExit, itemSystems.transform, false);
        if (terminalExitLine == null)
        {
            Plugin.Instance.Logger.LogError("Failed to create a new map exit line for the terminal.");
            return;
        }
        terminalExitLine.name = "TerminalMapExitLine";

        var terminalMesh = GameObject.Find("Environment/HangarShip/Terminal")?.GetComponentInChildren<MeshRenderer>();
        if (terminalMesh == null)
        {
            Plugin.Instance.Logger.LogError("Failed to find terminal object mesh.");
            return;
        }

        var terminalMapRenderer = terminalObject.AddComponent<ManualCameraRenderer>();
        terminalMapRenderer.cameraNearPlane = mainMapRenderer.cameraNearPlane;
        terminalMapRenderer.cameraFarPlane = mainMapRenderer.cameraFarPlane;

        terminalMapRenderer.cam = terminalMapCamera;
        terminalMapRenderer.mapCamera = terminalMapCamera;
        terminalMapRenderer.cam.targetTexture = new RenderTexture(mainMapRenderer.cam.targetTexture)
        {
            name = "TerminalMapTexture",
            filterMode = Plugin.TextureFiltering.Value,
        };

        terminalMapRenderer.mapCameraStationaryUI = mainMapRenderer.mapCameraStationaryUI;

        // Our terminal map will enable and disable the arrow UI when it is active.
        terminalMapRenderer.shipArrowUI = terminalMapShipArrowUI;
        terminalMapRenderer.shipArrowPointer = terminalMapShipArrowPointer;
        terminalMapRenderer.compassRose = terminalMapCompass;

        // Head mounted cam setup:
        terminalMapRenderer.headMountedCam = terminalMapHeadMountedCam;
        terminalMapRenderer.headMountedCamPositionOffset = mainMapRenderer.headMountedCamPositionOffset;
        terminalMapRenderer.headMountedCamRotationOffset = mainMapRenderer.headMountedCamRotationOffset;
        terminalMapRenderer.headMountedCamUI = terminalMapHeadMountedCamUI;
        terminalMapRenderer.localPlayerPlaceholder = terminalMapLocalPlayerPlaceholder;
        terminalMapRenderer.LostSignalUI = terminalMapSignalLostUI;

        terminalMapRenderer.mapCameraAnimator = newAnimator;
        terminalMapRenderer.lineFromRadarTargetToExit = terminalExitLine;

        terminalMapRenderer.targetedPlayer = mainMapRenderer.targetedPlayer;

        var viewMonitorNode = Plugin.Terminal.terminalNodes
            .allKeywords.FirstOrDefault(keyword => keyword.word == "view")?
            .compatibleNouns.FirstOrDefault(noun => noun.noun.word == "monitor")?.result;
        if (viewMonitorNode != null)
        {
            viewMonitorNode.displayTexture = terminalMapRenderer.cam.targetTexture;
            Plugin.Instance.Logger.LogInfo($"Terminal node '{viewMonitorNode.name}' will now use a separate texture.");
        }

        Plugin.TerminalMapRenderer = terminalMapRenderer;
        Plugin.TerminalMapScreenUICanvas = terminalMapScreenUICanvas;
        Plugin.TerminalMapScreenPlayerName = terminalMapPlayerName;

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
        return new ILInjector(instructions)
            .ReplaceMainMapWithTerminalMap()
            .ReleaseInstructions();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Terminal.RunTerminalEvents))]
    static bool RunTerminalEventsPrefix(TerminalNode __0)
    {
        return TerminalCommands.ProcessNode(__0);
    }
}
