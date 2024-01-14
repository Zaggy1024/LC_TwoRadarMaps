using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    internal class Common
    {
        public static IEnumerable<CodeInstruction> TranspileReplaceMainWithTerminalMapRenderer(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(Reflection.f_StartOfRound_mapScreen))
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldsfld, Reflection.f_Plugin_terminalMapRenderer);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
