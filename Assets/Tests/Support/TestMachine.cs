// Runtime (non-editor) assembly so the component can be added to GameObjects in EditMode tests.
using System.Collections.Generic;
using Game.Production;

namespace Game.Tests
{
    /// <summary>Concrete machine for tests: exposes the protected state switch and records hooks.</summary>
    public class TestMachine : MachineBase
    {
        public readonly List<string> Hooks = new List<string>();

        public void ForceState(MachineState state) => SetState(state);
        public void Warn(bool hasWarning, string reason) => SetWarning(hasWarning, reason);

        protected override void OnEnterRunning() => Hooks.Add("Running");
        protected override void OnEnterFault(string reason) => Hooks.Add("Fault:" + reason);
        protected override void OnEnterMaintenance() => Hooks.Add("Maintenance");
    }
}
