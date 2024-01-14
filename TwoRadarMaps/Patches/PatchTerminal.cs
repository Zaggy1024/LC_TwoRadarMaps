﻿using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using UnityEngine;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    internal class PatchTerminal
    {
        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        static void StartPrefix(ref Terminal __instance)
        {
            var terminalScript = __instance;
            var terminalObject = terminalScript.gameObject;
            var viewMonitorNode = terminalScript.terminalNodes.allKeywords.First(keyword => keyword.word == "view")?.compatibleNouns.First(noun => noun.noun.word == "monitor")?.result;

            // Existing objects/components
            var itemSystems = GameObject.Find("Systems/GameSystems/ItemSystems");
            if (itemSystems == null)
            {
                Plugin.Instance.Logger.LogError($"Could not find the ItemSystems object.");
                return;
            }

            var mainMapCamera = itemSystems.transform.Find("MapCamera")?.gameObject;
            if (mainMapCamera == null)
            {
                Plugin.Instance.Logger.LogError($"Could not find the default map camera.");
                return;
            }

            var mainMapRenderer = StartOfRound.Instance?.mapScreen;
            if (mainMapRenderer == null)
            {
                Plugin.Instance.Logger.LogError("Default map camera renderer is null.");
                return;
            }

            // New objects/components for terminal map rendering
            var terminalMapCameraObject = Object.Instantiate(mainMapCamera, itemSystems.transform, false);
            var terminalMapCamera = terminalMapCameraObject?.GetComponent<Camera>();
            if (terminalMapCamera == null)
            {
                Plugin.Instance.Logger.LogInfo("Failed to clone the default map camera.");
                return;
            }
            terminalMapCameraObject.name = "TerminalMapCamera";

            var newAnimator = terminalMapCameraObject.GetComponentInChildren<Animator>();
            if (newAnimator == null)
            {
                Plugin.Instance.Logger.LogInfo($"Failed to find new map flash animation.");
                return;
            }

            var newLight = terminalMapCameraObject.GetComponentInChildren<Light>();
            if (newLight == null)
            {
                Plugin.Instance.Logger.LogInfo($"Failed to find new map night vision light.");
                return;
            }

            var terminalMesh = GameObject.Find("Environment/HangarShip/Terminal")?.GetComponentInChildren<MeshRenderer>();
            if (terminalMesh == null)
            {
                Plugin.Instance.Logger.LogInfo($"Failed to find terminal object mesh.");
                return;
            }

            var junk = new GameObject("TerminalMapJunk");

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
            terminalMapRenderer.shipArrowUI = mainMapRenderer.shipArrowUI;
            terminalMapRenderer.shipArrowPointer = junk.transform;

            terminalMapRenderer.mapCameraStationaryUI = mainMapRenderer.mapCameraStationaryUI;

            terminalMapRenderer.mapCameraAnimator = newAnimator;
            terminalMapRenderer.mapCameraLight = newLight;

            viewMonitorNode.displayTexture = terminalMapRenderer.cam.targetTexture;

            Plugin.Instance.Logger.LogInfo($"Terminal node '{viewMonitorNode.name}' will now use a separate texture.");
            Plugin.terminalMapRenderer = terminalMapRenderer;

            // Disable the camera until the player interacts with the terminal.
            Plugin.terminalMapRenderer.SwitchScreenOn(false);
            Plugin.terminalMapRenderer.cam.enabled = false;

            terminalScript.terminalUIScreen.gameObject.AddComponent<TerminalVisibilityTracker>();

            Plugin.UpdateRadarTargets();
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetTerminalInUseLocalClient")]
        static void SetTerminalInUseLocalClientPostfix(Terminal __instance, bool __0)
        {
            bool isOn = __0;

            // Display the map overlay UI on the terminal map instead while the terminal is being accessed.
            // Ideally we would be able to display it on both, but that seems like it would require duplicating
            // the objects for the monitored player text as well as every interactable in the world.
            if (isOn)
                StartOfRound.Instance.radarCanvas.worldCamera = Plugin.terminalMapRenderer.cam;
            else
                StartOfRound.Instance.radarCanvas.worldCamera = StartOfRound.Instance.mapScreen.cam;

            Plugin.UpdateCurrentMonitoredPlayer();
        }

        [HarmonyTranspiler]
        [HarmonyPatch("RunTerminalEvents")]
        static IEnumerable<CodeInstruction> TranspileRunTerminalEvents(IEnumerable<CodeInstruction> instructions)
        {
            return Common.TranspileReplaceMainWithTerminalMapRenderer(instructions);
        }

        [HarmonyTranspiler]
        [HarmonyPatch("ParsePlayerSentence")]
        static IEnumerable<CodeInstruction> TranspileParsePlayerSentence(IEnumerable<CodeInstruction> instructions)
        {
            return Common.TranspileReplaceMainWithTerminalMapRenderer(instructions);
        }
    }
}
