using System;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Production
{
    [Serializable]
    public class Content<T> where T : Enum
    {
        public T State;
        public LocalizedString InfoKey;

        private string _info;
        public string Info => _info;

        public void SetInfo(string info) => _info = info;
    }

    /// <summary>
    /// Generic abstract base class for all production stations. Owns the <see cref="MachineState"/>
    /// state machine, enforces valid transitions, and exposes protected hooks for concrete machines
    /// to react to state changes without having to reimplement transition logic themselves.
    /// </summary>
    public abstract class MachineBase : MonoBehaviour, IMachine
    {
        [SerializeField]
        private LocalizedString _nameKey;
        public string NameKey => _nameKey.TableEntryReference.Key;
        private MachineState _currentState = MachineState.Ready;

        /// <inheritdoc />
        public string Id => $"{_nameKey}_{_number}";
        public string Name => _nameKey.GetLocalizedString();
        private int _number;
        public int Number => _number;
        public void SetNumber(int number) => _number = number;

        /// <inheritdoc />
        public MachineState CurrentState => _currentState;

        /// <inheritdoc />
        public event Action<MachineState, MachineState> StateChanged;

        /// <summary>
        /// The reason passed to the most recent <see cref="TriggerFault(string)"/> call, if any.
        /// Cleared once maintenance completes.
        /// </summary>
        protected string LastFaultReason { get; private set; } = string.Empty;

        public event Action<string> ContentChanged;
        public void NotifyContentChanged(string content) => ContentChanged?.Invoke(content);
       

        /// <inheritdoc />
        public void StartRun()
        {
            if (_currentState != MachineState.Ready)
            {
                return;
            }

            SetState(MachineState.Starting);
        }

        /// <inheritdoc />
        public void StopRun()
        {
            if (_currentState != MachineState.Running)
            {
                return;
            }

            SetState(MachineState.Stopping);
        }

        /// <inheritdoc />
        public void TriggerFault(string reason)
        {
            // A machine can fault from almost any operational state, but not while
            // it is already being serviced.
            if (_currentState == MachineState.Maintenance)
            {
                return;
            }

            LastFaultReason = reason;
            SetState(MachineState.Fault);
        }

        /// <inheritdoc />
        public void AcknowledgeFault()
        {
            if (_currentState != MachineState.Fault)
            {
                return;
            }

            SetState(MachineState.Maintenance);
        }

        /// <inheritdoc />
        public void CompleteMaintenance()
        {
            if (_currentState != MachineState.Maintenance)
            {
                return;
            }

            LastFaultReason = string.Empty;
            SetState(MachineState.Ready);
        }

        /// <inheritdoc />
        public void ResetToIdle()
        {
            if (_currentState != MachineState.Stopped)
            {
                return;
            }

            SetState(MachineState.Ready);
        }

        /// <summary>
        /// Advances the internal state machine to the given target state, but only if the transition
        /// is valid. Invalid transitions are rejected silently (logged as a warning) so that a stray
        /// call from a concrete machine can never corrupt the state machine.
        /// </summary>
        /// <param name="target">The state to transition into.</param>
        protected void SetState(MachineState target)
        {
            if (!IsValidTransition(_currentState, target))
            {
                Debug.LogWarning(
                    $"[{Id}] Rejected invalid machine state transition: {_currentState} -> {target}.");
                return;
            }

            MachineState previous = _currentState;
            _currentState = target;

            OnStateExit(previous);
            InvokeEnterHook(target);

            StateChanged?.Invoke(previous, target);
        }

        /// <summary>
        /// Defines which state transitions are legal. Centralised here so concrete machines cannot
        /// accidentally skip steps (e.g. Idle -> Running directly, or leaving Fault without Maintenance).
        /// </summary>
        protected virtual bool IsValidTransition(MachineState from, MachineState to)
        {
            switch (from)
            {
                case MachineState.Ready:
                    return to == MachineState.Starting || to == MachineState.Fault;

                case MachineState.Starting:
                    return to == MachineState.Running || to == MachineState.Fault;

                case MachineState.Running:
                    return to == MachineState.Stopping || to == MachineState.Fault;

                case MachineState.Stopping:
                    return to == MachineState.Stopped || to == MachineState.Fault;

                case MachineState.Stopped:
                    return to == MachineState.Ready || to == MachineState.Fault;

                case MachineState.Fault:
                    // The only way out of a fault is through maintenance.
                    return to == MachineState.Maintenance;

                case MachineState.Maintenance:
                    return to == MachineState.Ready;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Dispatches to the matching protected OnEnter* hook for the given state.
        /// </summary>
        private void InvokeEnterHook(MachineState entered)
        {
            switch (entered)
            {
                case MachineState.Ready:
                    OnEnterReady();
                    break;
                case MachineState.Starting:
                    OnEnterStarting();
                    break;
                case MachineState.Running:
                    OnEnterRunning();
                    break;
                case MachineState.Stopping:
                    OnEnterStopping();
                    break;
                case MachineState.Stopped:
                    OnEnterStopped();
                    break;
                case MachineState.Fault:
                    OnEnterFault(LastFaultReason);
                    break;
                case MachineState.Maintenance:
                    OnEnterMaintenance();
                    break;
            }
        }

        // ---- Overridable hooks for concrete machines (Day 2+) ----

        /// <summary>Called right before the machine leaves <paramref name="previousState"/>.</summary>
        protected virtual void OnStateExit(MachineState previousState) { }

        /// <summary>Called when the machine enters <see cref="MachineState.Ready"/>.</summary>
        protected virtual void OnEnterReady() { }

        /// <summary>Called when the machine enters <see cref="MachineState.Starting"/>.</summary>
        protected virtual void OnEnterStarting() { }

        /// <summary>Called when the machine enters <see cref="MachineState.Running"/>.</summary>
        protected virtual void OnEnterRunning() { }

        /// <summary>Called when the machine enters <see cref="MachineState.Stopping"/>.</summary>
        protected virtual void OnEnterStopping() { }

        /// <summary>Called when the machine enters <see cref="MachineState.Stopped"/>.</summary>
        protected virtual void OnEnterStopped() { }

        /// <summary>Called when the machine enters <see cref="MachineState.Fault"/>, with the triggering reason.</summary>
        protected virtual void OnEnterFault(string reason) { }

        /// <summary>Called when the machine enters <see cref="MachineState.Maintenance"/>.</summary>
        protected virtual void OnEnterMaintenance() { }
    }
}
