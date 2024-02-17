using System.Reflection;

namespace TwoRadarMaps.Patches
{
    public static class Reflection
    {
        public static readonly MethodInfo m_StartOfRound_Instance = typeof(StartOfRound).GetMethod("get_Instance");
        public static readonly FieldInfo f_StartOfRound_mapScreen = typeof(StartOfRound).GetField(nameof(StartOfRound.mapScreen));
        public static readonly FieldInfo f_StartOfRound_mapScreenPlayerName = typeof(StartOfRound).GetField(nameof(StartOfRound.mapScreenPlayerName));
        public static readonly FieldInfo f_StartOfRound_radarCanvas = typeof(StartOfRound).GetField(nameof(StartOfRound.radarCanvas));

        public static readonly FieldInfo f_Plugin_terminalMapRenderer = typeof(Plugin).GetField(nameof(Plugin.terminalMapRenderer));
        public static readonly FieldInfo f_Plugin_terminalMapScreenUICanvas = typeof(Plugin).GetField(nameof(Plugin.terminalMapScreenUICanvas));
        public static readonly FieldInfo f_Plugin_terminalMapScreenPlayerName = typeof(Plugin).GetField(nameof(Plugin.terminalMapScreenPlayerName));

        public static readonly MethodInfo m_UnityEngine_Object_op_Equality = typeof(UnityEngine.Object).GetMethod("op_Equality", [typeof(UnityEngine.Object), typeof(UnityEngine.Object)]);
    }
}
