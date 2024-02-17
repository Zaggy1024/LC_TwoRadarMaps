using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace TwoRadarMaps
{
    public static class TerminalCommands
    {
        public static TerminalNode CycleZoomNode = null;
        public static TerminalNode ZoomInNode = null;
        public static TerminalNode ZoomOutNode = null;
        public static TerminalNode ResetZoomNode = null;

        public static TerminalNode TeleportNode = null;

        private static readonly List<TerminalKeyword> newTerminalKeywords = [];
        private static readonly List<TerminalKeyword> modifiedTerminalKeywords = [];

        public static void Initialize()
        {
            if (Plugin.Terminal == null)
                return;

            RemoveAddedKeywords();
            Plugin.UpdateZoomFactors();

            var keywordsToAppend = new List<TerminalKeyword>();

            if (Plugin.EnableZoom.Value)
            {
                CycleZoomNode = ScriptableObject.CreateInstance<TerminalNode>();
                CycleZoomNode.name = "CycleZoomNode";
                CycleZoomNode.displayText = "";
                CycleZoomNode.clearPreviousText = true;

                ZoomInNode = ScriptableObject.CreateInstance<TerminalNode>();
                ZoomInNode.name = "ZoomIn";
                ZoomInNode.displayText = "";
                ZoomInNode.clearPreviousText = true;

                ZoomOutNode = ScriptableObject.CreateInstance<TerminalNode>();
                ZoomOutNode.name = "ZoomOut";
                ZoomOutNode.displayText = "";
                ZoomOutNode.clearPreviousText = true;

                ResetZoomNode = ScriptableObject.CreateInstance<TerminalNode>();
                ResetZoomNode.name = "ResetZoom";
                ResetZoomNode.displayText = "";
                ResetZoomNode.clearPreviousText = true;

                TerminalKeyword inKeyword = FindOrCreateKeyword("In", "in", false);
                TerminalKeyword outKeyword = FindOrCreateKeyword("Out", "out", false);
                var zoomVerbKeyword = FindOrCreateKeyword("Zoom", "zoom", true,
                    [
                        new CompatibleNoun()
                        {
                            noun = inKeyword,
                            result = ZoomInNode,
                        },
                        new CompatibleNoun()
                        {
                            noun = outKeyword,
                            result = ZoomOutNode,
                        },
                    ]);
                zoomVerbKeyword.specialKeywordResult = CycleZoomNode;

                TerminalKeyword zoomNounKeyword = FindOrCreateKeyword("Zoom", "zoom", false);
                var resetVerbKeyword = FindOrCreateKeyword("Reset", "reset", true,
                    [
                        new CompatibleNoun()
                        {
                            noun = zoomNounKeyword,
                            result = ResetZoomNode,
                        },
                    ]);
            }

            if (Plugin.EnableTeleportCommand.Value)
            {
                TeleportNode = ScriptableObject.CreateInstance<TerminalNode>();
                TeleportNode.name = "TeleportNode";
                TeleportNode.clearPreviousText = true;

                TerminalKeyword teleporterNounKeyword = FindOrCreateKeyword("Teleporter", "teleporter", false);
                FindOrCreateKeyword("Activate", "activate", true,
                    [
                        new CompatibleNoun()
                        {
                            noun = teleporterNounKeyword,
                            result = TeleportNode,
                        }
                    ]);
            }

            AddNewlyCreatedCommands();
        }

        public static bool ProcessNode(TerminalNode node)
        {
            if (node == CycleZoomNode)
            {
                Plugin.CycleTerminalMapZoom();
                return false;
            }
            if (node == ZoomInNode)
            {
                Plugin.ZoomTerminalMapIn();
                return false;
            }
            if (node == ZoomOutNode)
            {
                Plugin.ZoomTerminalMapOut();
                return false;
            }
            if (node == ResetZoomNode)
            {
                Plugin.SetZoomLevel(Plugin.DefaultZoomLevel.Value);
                return false;
            }
            if (node == TeleportNode)
            {
                if (Plugin.Teleporter == null)
                {
                    TeleportNode.displayText = "Teleporter is not installed.\n\n";
                    return true;
                }
                TeleportNode.displayText = $"Teleporting {StartOfRound.Instance.mapScreen.radarTargets[Plugin.terminalMapRenderer.targetTransformIndex]?.name}...\n\n";
                Plugin.TeleportTarget(Plugin.terminalMapRenderer.targetTransformIndex);
                return false;
            }
            return true;
        }

        static void RemoveAddedKeywords()
        {
            // Remove references to new keywords.
            foreach (var keyword in modifiedTerminalKeywords)
            {
                if (keyword.compatibleNouns != null)
                    keyword.compatibleNouns = [.. keyword.compatibleNouns.Where(compatible => !newTerminalKeywords.Contains(compatible.noun))];
            }
            modifiedTerminalKeywords.Clear();

            // Remove new keywords.
            foreach (var keyword in newTerminalKeywords)
                Object.Destroy(keyword);

            var nodes = Plugin.Terminal.terminalNodes;
            nodes.allKeywords = [.. nodes.allKeywords.Where(keyword => !newTerminalKeywords.Contains(keyword))];

            newTerminalKeywords.Clear();
        }

        static TerminalKeyword FindKeyword(string word, bool verb)
        {
            return Plugin.Terminal.terminalNodes.allKeywords.FirstOrDefault(keyword => keyword.word == word && keyword.isVerb == verb);
        }

        static CompatibleNoun FindCompatibleNoun(this TerminalKeyword keyword, string noun)
        {
            return keyword.compatibleNouns.FirstOrDefault(compatible => compatible.noun.word == noun);
        }

        static TerminalKeyword FindOrCreateKeyword(string name, string word, bool verb, CompatibleNoun[] compatibleNouns = null)
        {
            Plugin.Instance.Logger.LogInfo($"Creating terminal {(verb ? "verb" : "noun")} '{word}' ({name}).");
            TerminalKeyword keyword = FindKeyword(word, verb);
            if (keyword == null)
            {
                keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
                keyword.name = name;
                keyword.isVerb = verb;
                keyword.word = word;
                keyword.compatibleNouns = compatibleNouns;
                newTerminalKeywords.Add(keyword);
                Plugin.Instance.Logger.LogInfo($"  Keyword was not found, created a new one.");
            }
            else
            {
                keyword.compatibleNouns = [.. keyword.compatibleNouns ?? [], .. compatibleNouns ?? []];
                Plugin.Instance.Logger.LogInfo($"  Keyword existed, appended nouns.");
            }

            modifiedTerminalKeywords.Add(keyword);
            return keyword;
        }

        static void AddNewlyCreatedCommands()
        {
            var nodes = Plugin.Terminal.terminalNodes;
            nodes.allKeywords = [.. nodes.allKeywords, .. newTerminalKeywords];
        }
    }
}
