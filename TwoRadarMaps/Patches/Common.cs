using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using HarmonyLib;

namespace TwoRadarMaps.Patches
{
    public class SequenceMatch(int start, int end)
    {
        public int Start = start;
        public int End = end;

        public int Size { get => End - Start; }
    }

    public static class Common
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

        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, int startIndex, int count, IEnumerable<Predicate<T>> predicates)
        {
            var index = startIndex;
            while (index < list.Count())
            {
                var predicateEnumerator = predicates.GetEnumerator();
                if (!predicateEnumerator.MoveNext())
                    return null;
                index = list.FindIndex(index, predicateEnumerator.Current);

                if (index < 0)
                    break;

                bool matches = true;
                var sequenceIndex = 1;
                while (predicateEnumerator.MoveNext())
                {
                    if (sequenceIndex >= list.Count() - index
                        || !predicateEnumerator.Current(list[index + sequenceIndex]))
                    {
                        matches = false;
                        break;
                    }
                    sequenceIndex++;
                }

                if (matches)
                    return new SequenceMatch(index, index + predicates.Count());
                index++;
            }

            return null;
        }

        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, int startIndex, IEnumerable<Predicate<T>> predicates)
        {
            return FindIndexOfSequence(list, startIndex, -1, predicates);
        }

        public static SequenceMatch FindIndexOfSequence<T>(this List<T> list, IEnumerable<Predicate<T>> predicates)
        {
            return FindIndexOfSequence(list, 0, -1, predicates);
        }
    }
}
