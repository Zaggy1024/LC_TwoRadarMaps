using System.Reflection;

namespace TwoRadarMaps.Patches
{
    public static class Reflection
    {
        public static readonly FieldInfo f_StartOfRound_mapScreen = typeof(StartOfRound).GetField(nameof(StartOfRound.mapScreen));
        public static readonly FieldInfo f_Plugin_terminalMapRenderer = typeof(Plugin).GetField(nameof(Plugin.terminalMapRenderer));
    }
}
