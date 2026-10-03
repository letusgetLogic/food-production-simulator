using System.Collections.Generic;
using Game.Production;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class MachineBaseTests
    {
        private GameObject _go;
        private TestMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestMachine");
            _machine = _go.AddComponent<TestMachine>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void StartsInReady() => Assert.AreEqual(MachineState.Ready, _machine.CurrentState);

        [Test]
        public void StartRun_FromReady_GoesToStarting()
        {
            _machine.StartRun();
            Assert.AreEqual(MachineState.Starting, _machine.CurrentState);
        }

        [Test]
        public void StopRun_OnlyWorksWhileRunning()
        {
            _machine.StopRun();
            Assert.AreEqual(MachineState.Ready, _machine.CurrentState);

            _machine.StartRun();
            _machine.ForceState(MachineState.Running);
            _machine.StopRun();
            Assert.AreEqual(MachineState.Stopping, _machine.CurrentState);
        }

        [Test]
        public void FaultCycle_RequiresAcknowledgeAndMaintenance()
        {
            _machine.TriggerFault("Jam");
            Assert.AreEqual(MachineState.Fault, _machine.CurrentState);
            Assert.AreEqual("Jam", _machine.FaultReason);

            // No shortcut out of a fault.
            _machine.StartRun();
            _machine.RequestRun();
            _machine.CompleteMaintenance();
            Assert.AreEqual(MachineState.Fault, _machine.CurrentState);

            _machine.AcknowledgeFault();
            Assert.AreEqual(MachineState.Maintenance, _machine.CurrentState);
            Assert.AreEqual("Jam", _machine.FaultReason, "reason stays visible during maintenance");

            _machine.CompleteMaintenance();
            Assert.AreEqual(MachineState.Ready, _machine.CurrentState);
            Assert.AreEqual(string.Empty, _machine.FaultReason);
        }

        [Test]
        public void TriggerFault_IsIgnoredDuringMaintenance()
        {
            _machine.TriggerFault("Jam");
            _machine.AcknowledgeFault();
            _machine.TriggerFault("Other");
            Assert.AreEqual(MachineState.Maintenance, _machine.CurrentState);
            Assert.AreEqual("Jam", _machine.FaultReason);
        }

        [Test]
        public void RequestRun_StartsFromStopped()
        {
            _machine.StartRun();
            _machine.ForceState(MachineState.Running);
            _machine.StopRun();
            _machine.ForceState(MachineState.Stopped);

            _machine.RequestRun();
            Assert.AreEqual(MachineState.Starting, _machine.CurrentState);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndNext()
        {
            var changes = new List<(MachineState, MachineState)>();
            _machine.StateChanged += (previous, next) => changes.Add((previous, next));

            _machine.StartRun();
            _machine.ForceState(MachineState.Running);

            CollectionAssert.AreEqual(new[]
            {
                (MachineState.Ready, MachineState.Starting),
                (MachineState.Starting, MachineState.Running)
            }, changes);
            CollectionAssert.Contains(_machine.Hooks, "Running");
        }

        [Test]
        public void Warning_IsIndependentOfStateAndDeduplicated()
        {
            int events = 0;
            _machine.WarningChanged += (_, __) => events++;

            _machine.Warn(true, "BufferFilling");
            _machine.Warn(true, "BufferFilling");
            Assert.IsTrue(_machine.HasWarning);
            Assert.AreEqual("BufferFilling", _machine.WarningReason);
            Assert.AreEqual(MachineState.Ready, _machine.CurrentState);
            Assert.AreEqual(1, events);

            _machine.Warn(false, "ignored");
            Assert.IsFalse(_machine.HasWarning);
            Assert.AreEqual(string.Empty, _machine.WarningReason);
            Assert.AreEqual(2, events);
        }
    }
}
