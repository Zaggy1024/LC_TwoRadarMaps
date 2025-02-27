using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace TwoRadarMaps.Patches;

public static class Reflection
{
    public static readonly MethodInfo m_Component_Transform = typeof(Component).GetMethod("get_transform");

    public static readonly MethodInfo m_StartOfRound_Instance = typeof(StartOfRound).GetMethod("get_Instance");
    public static readonly FieldInfo f_StartOfRound_allPlayerScripts = typeof(StartOfRound).GetField(nameof(StartOfRound.allPlayerScripts));
    public static readonly FieldInfo f_StartOfRound_mapScreen = typeof(StartOfRound).GetField(nameof(StartOfRound.mapScreen));
    public static readonly FieldInfo f_StartOfRound_mapScreenPlayerName = typeof(StartOfRound).GetField(nameof(StartOfRound.mapScreenPlayerName));
    public static readonly FieldInfo f_StartOfRound_radarCanvas = typeof(StartOfRound).GetField(nameof(StartOfRound.radarCanvas));

    public static readonly FieldInfo f_ManualCameraRenderer_radarTargets = typeof(ManualCameraRenderer).GetField(nameof(ManualCameraRenderer.radarTargets));
    public static readonly MethodInfo m_List_TransformAndName_get_Item = typeof(List<TransformAndName>).GetMethod("get_Item", [typeof(int)]);

    public static readonly FieldInfo f_Plugin_terminalMapRenderer = typeof(Plugin).GetField(nameof(Plugin.TerminalMapRenderer));
    public static readonly FieldInfo f_Plugin_terminalMapScreenUICanvas = typeof(Plugin).GetField(nameof(Plugin.TerminalMapScreenUICanvas));
    public static readonly FieldInfo f_Plugin_terminalMapScreenPlayerName = typeof(Plugin).GetField(nameof(Plugin.TerminalMapScreenPlayerName));

    public static readonly FieldInfo f_TransformAndName_name = typeof(TransformAndName).GetField(nameof(TransformAndName.name));

    public static readonly MethodInfo m_UnityEngine_Object_op_Equality = typeof(UnityEngine.Object).GetMethod("op_Equality", [typeof(UnityEngine.Object), typeof(UnityEngine.Object)]);

    public static MethodInfo GetMethod(this Type type, string name, BindingFlags bindingFlags, Type[] parameters)
    {
        return type.GetMethod(name, bindingFlags, null, parameters, null);
    }
}
