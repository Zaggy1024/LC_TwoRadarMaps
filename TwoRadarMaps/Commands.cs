using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwoRadarMaps
{
    public static class Commands
    {
        private static bool hasInitialized = false;
        public static TerminalNode CycleZoomNode = null;
        public static TerminalNode ZoomInNode = null;
        public static TerminalNode ZoomOutNode = null;
        public static TerminalNode ResetZoomNode = null;

        public static void Initialize(TerminalNodesList nodes)
        {
            if (hasInitialized)
                return;
            hasInitialized = true;

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

                TerminalKeyword GetOrCreateKeyword(string word, bool verb, CompatibleNoun[] compatibleNouns = null)
                {
                    TerminalKeyword keyword = nodes.allKeywords.FirstOrDefault(k => k.isVerb == verb && k.word == word);
                    if (keyword == null)
                    {
                        keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
                        keyword.name = word;
                        keyword.isVerb = verb;
                        keyword.word = word.ToLower();
                        keyword.compatibleNouns = compatibleNouns;
                        keywordsToAppend.Add(keyword);
                    }
                    else
                    {
                        keyword.compatibleNouns = keyword.compatibleNouns.Union(compatibleNouns, CompatibleNounComparer.Instance).ToArray();
                    }
                    return keyword;
                }
                TerminalKeyword inKeyword = GetOrCreateKeyword("In", false);
                TerminalKeyword outKeyword = GetOrCreateKeyword("Out", false);
                var zoomVerbKeyword = GetOrCreateKeyword("Zoom", true,
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

                TerminalKeyword zoomNounKeyword = GetOrCreateKeyword("Zoom", false);
                var resetVerbKeyword = GetOrCreateKeyword("Reset", true,
                    [
                        new CompatibleNoun()
                        {
                            noun = zoomNounKeyword,
                            result = ResetZoomNode,
                        },
                    ]);
            }
            
            nodes.allKeywords = [.. nodes.allKeywords, .. keywordsToAppend];
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
            return true;
        }
    }

    public class CompatibleNounComparer : IEqualityComparer<CompatibleNoun>
    {
        public static readonly CompatibleNounComparer Instance = new CompatibleNounComparer();

        public bool Equals(CompatibleNoun a, CompatibleNoun b)
        {
            return a.noun == b.noun && a.result == b.result;
        }

        public int GetHashCode(CompatibleNoun noun)
        {
            return noun.noun.GetHashCode() ^ noun.result.GetHashCode();
        }
    }
}
