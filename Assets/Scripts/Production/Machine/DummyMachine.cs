using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Minimal, non-functional example machine used to demonstrate how a concrete station
    /// would drive the state machine defined in <see cref="MachineBase"/>. Not intended for
    /// production use — real stations (dough mixer, oven, ...) arrive from Day 2 onwards.
    /// </summary>
    public class DummyMachine : MachineBase
    {
        [SerializeField]
        private float _startupDurationSeconds = 1.5f;

        private float _startupTimer;

        protected override void OnEnterReady()
        {
            Debug.Log($"[{Id}] Idle.");
        }

        protected override void OnEnterStarting()
        {
            Debug.Log($"[{Id}] Warming up...");
            _startupTimer = 0f;
        }

        protected override void OnEnterRunning()
        {
            Debug.Log($"[{Id}] Now running.");
        }

        protected override void OnEnterStopping()
        {
            Debug.Log($"[{Id}] Winding down...");
            // A real machine would finish its current product cycle here;
            // the dummy finishes immediately.
            SetState(MachineState.Stopped);
        }

        protected override void OnEnterStopped()
        {
            Debug.Log($"[{Id}] Stopped.");
        }

        protected override void OnEnterFault(string reason)
        {
            Debug.LogError($"[{Id}] FAULT: {reason}");
        }

        protected override void OnEnterMaintenance()
        {
            Debug.Log($"[{Id}] Under maintenance.");
        }

        private void Update()
        {
            // Example of a concrete machine driving its own Starting -> Running transition
            // once some condition (here: a simple timer) is met.
            if (CurrentState != MachineState.Starting)
            {
                return;
            }

            _startupTimer += Time.deltaTime;
            if (_startupTimer >= _startupDurationSeconds)
            {
                SetState(MachineState.Running);
            }
        }

        /// <summary>
        /// Example entry point that a test scene / editor button could call to see the
        /// full happy-path cycle: Idle -> Starting -> Running -> Stopping -> Stopped.
        /// </summary>
        [ContextMenu("Demo: Run Full Cycle")]
        private void DemoRunFullCycle()
        {
            StartRun(); // Idle -> Starting (Running is reached automatically via Update)
        }

        /// <summary>
        /// Example entry point for stopping the machine.
        /// </summary>
        [ContextMenu("Demo: Stop")]
        private void DemoStop()
        {
            StopRun(); // Running -> Stopping (Stopped is reached automatically via Update)
        }

        /// <summary>
        /// Example entry point simulating a sensor reporting a jam.
        /// </summary>
        [ContextMenu("Demo: Simulate Fault")]
        private void DemoSimulateFault()
        {
            TriggerFault("Conveyor jam detected by sensor #3");
        }

        /// <summary>
        /// Example entry point for initiating maintenance.
        /// </summary>
        [ContextMenu("Demo: Maintenance")]
        private void DemoMaintenance()
        {
            AcknowledgeFault(); // Fault -> Maintenance
        }

        /// <summary>
        /// Example entry point for completing maintenance.
        /// </summary>
        [ContextMenu("Demo: Complete Maintenance")]
        private void DemoCompleteMaintenance()
        {
            CompleteMaintenance(); // Maintenance -> Idle
        }

        /// <summary>
        /// Example entry point simulating an operator confirming the station is clear
        /// after stopping, e.g. via the HMI.
        /// </summary>
        [ContextMenu("Demo: Reset After Stop")]
        private void DemoResetToIdle()
        {
            ResetToIdle();
        }
    }
}
