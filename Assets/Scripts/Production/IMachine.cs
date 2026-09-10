using System;

namespace Game.Production
{
    /// <summary>
    /// Generic contract that every production station (machine) in the pizza line must implement.
    /// Contains no station-specific logic — concrete machines (dough mixer, oven, etc.) implement
    /// their own processing behaviour on top of this, starting Day 2.
    /// </summary>
    public interface IMachine
    {
        /// <summary>
        /// Unique, stable identifier for this machine instance (used for save/load, HMI, logging).
        /// </summary>
        string MachineId { get; }

        /// <summary>
        /// The machine's current lifecycle state.
        /// </summary>
        MachineState CurrentState { get; }

        /// <summary>
        /// Raised whenever <see cref="CurrentState"/> changes.
        /// Parameters are (previousState, newState).
        /// </summary>
        event Action<MachineState, MachineState> StateChanged;

        /// <summary>
        /// Requests the machine to start (Idle -> Starting -> Running).
        /// No-op (or ignored) if the machine is not currently in a state that allows starting.
        /// </summary>
        void StartMachine();

        /// <summary>
        /// Requests the machine to stop (Running -> Stopping -> Stopped).
        /// No-op (or ignored) if the machine is not currently in a state that allows stopping.
        /// </summary>
        void StopMachine();

        /// <summary>
        /// Signals that the machine has entered an error condition, transitioning it into <see cref="MachineState.Fault"/>.
        /// Intended to be called by sensors, the FaultSystem, or internal machine logic.
        /// </summary>
        /// <param name="reason">Human-readable or code-like description of what went wrong.</param>
        void TriggerFault(string reason);

        /// <summary>
        /// Acknowledges and clears a fault, moving the machine into <see cref="MachineState.Maintenance"/>
        /// so it can be inspected/reset before returning to normal operation.
        /// </summary>
        void AcknowledgeFault();

        /// <summary>
        /// Completes maintenance and returns the machine to an operable state (<see cref="MachineState.Idle"/>).
        /// Only valid while the machine is in <see cref="MachineState.Maintenance"/>.
        /// </summary>
        void CompleteMaintenance();

        /// <summary>
        /// Explicitly acknowledges that the machine has finished stopping and returns it to
        /// <see cref="MachineState.Idle"/>, ready to be started again.
        /// </summary>
        void ResetToIdle();
    }
}
