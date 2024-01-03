using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    internal class Common
    {
        static readonly FieldInfo f_StartOfRound_mapScreen = AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.mapScreen));
        static readonly FieldInfo f_Plugin_terminalMapRenderer = AccessTools.Field(typeof(Plugin), nameof(Plugin.terminalMapRenderer));

        public static IEnumerable<CodeInstruction> TranspileReplaceMainWithTerminalMapRenderer(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(f_StartOfRound_mapScreen))
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldsfld, f_Plugin_terminalMapRenderer);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
