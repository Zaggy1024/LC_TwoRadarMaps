using System.Reflection.Emit;
using System.Reflection;

using HarmonyLib;

using TwoRadarMaps.Utilities.IL;

namespace TwoRadarMaps.Patches;

internal static class Common
{
    public static ILInjector ReplaceMainMapWithTerminalMap(this ILInjector injector)
    {
        injector.GoToStart();

        while (true)
        {
            injector
                .Find([
                    ILMatcher.Call(Reflection.m_StartOfRound_Instance),
                    ILMatcher.Ldfld(Reflection.f_StartOfRound_mapScreen),
                ]);

            if (!injector.IsValid)
                break;

            injector
                .ReplaceLastMatch([
                    new CodeInstruction(OpCodes.Ldsfld, Reflection.f_Plugin_terminalMapRenderer),
                ]);
        }

        return injector.GoToStart();
    }

    internal static ILInjector InsertTerminalField(this ILInjector injector, CodeInstruction loadRenderer, FieldInfo vanillaStartOfRoundField, FieldInfo pluginField)
    {
        injector.GoToStart();

        while (true)
        {
            injector
                .Find([
                    ILMatcher.Call(Reflection.m_StartOfRound_Instance),
                    ILMatcher.Ldfld(vanillaStartOfRoundField),
                ]);

            if (!injector.IsValid)
                break;

            injector
                .DefineLabel(out var isTerminalMapLabel)
                .Insert([
                    loadRenderer,
                    new(OpCodes.Ldsfld, Reflection.f_Plugin_terminalMapRenderer),
                    new(OpCodes.Beq_S, isTerminalMapLabel),
                ])
                .GoToMatchEnd()
                .AddLabel(out var isVanillaMapLabel)
                .Insert([
                    new(OpCodes.Br_S, isVanillaMapLabel),
                    new CodeInstruction(OpCodes.Ldsfld, pluginField).WithLabels(isTerminalMapLabel),
                ]);
        }

        return injector.GoToStart();
    }
}
