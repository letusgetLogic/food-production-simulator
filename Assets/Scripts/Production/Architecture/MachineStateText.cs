using Game.Core;

namespace Game.Production
{
    /// <summary>
    /// Display texts for <see cref="MachineState"/> (HMI rows, debug panel): key "state.&lt;state&gt;" in the
    /// localization table, e.g. <c>state.maintenance</c>.
    /// </summary>
    public static class MachineStateText
    {
        public static string Key(MachineState state) => "state." + LocText.Snake(state.ToString());

        public static string Get(MachineState state) => Get(state, state.ToString());

        public static string Get(MachineState state, string fallback) => LocText.Get(Key(state), fallback);
    }
}
