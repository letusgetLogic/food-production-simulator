using System;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Production
{
    /// <summary>
    /// Localized HMI message of a machine for one state. The language is only switched in the main menu, so the
    /// text is resolved once (as soon as the string table is loaded) and then cached.
    /// </summary>
    [Serializable]
    public class Content<T> where T : Enum
    {
        public T State;
        public LocalizedString InfoKey;

        private string _info;

        /// <summary>The translated text, or null while the table is still loading or no key is set.</summary>
        public string Info => _info ??= LocText.Now(InfoKey, null);
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
        /// <summary>
        /// Identifies the machine type for numbering ("Oven 1", "Oven 2"). The name keys are referenced by
        /// key id (key name empty), so fall back to the id - otherwise all machines shared one counter.
        /// </summary>
        public string NameKey => string.IsNullOrEmpty(_nameKey.TableEntryReference.Key)
            ? _nameKey.TableEntryReference.KeyId.ToString()
            : _nameKey.TableEntryReference.Key;
        private MachineState _currentState = MachineState.Ready;

        /// <inheritdoc />
        public string Id => $"{NameKey}_{_number}";

        private string _name;

        /// <summary>
        /// Localized name ("Ofen"). The language is only switched in the main menu, so it is resolved once and
        /// cached; the GameObject name stands in while the string table is still loading (non-blocking - WebGL).
        /// </summary>
        public string Name => (_name ??= LocText.Now(_nameKey, null)) ?? name;

        /// <summary>Name with the per-type number from the overview ("Ofen 1"), or only the name while unnumbered.</summary>
        public string DisplayName => _number > 0 ? $"{Name} {_number}" : Name;

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

        /// <summary>
        /// Public read access to the current fault reason (code, e.g. "Jam", "SauceEmpty",
        /// "WrongProduct:MixedDough"). Valid while in Fault or Maintenance, empty otherwise.
        /// Used by the FaultMonitor/HMI to look up the fault definition.
        /// </summary>
        public string FaultReason => LastFaultReason;

        public event Action<string> ContentChanged;
        public void NotifyContentChanged(string content) => ContentChanged?.Invoke(content);
       

        /// <summary>
        /// True while the machine reports a non-blocking warning (HMI amber), e.g. "buffer almost full"
        /// or "temperature deviation". Independent of <see cref="CurrentState"/>: a machine can be
        /// Running and have a warning at the same time. Deliberately NOT a MachineState value so the
        /// transition table stays untouched.
        /// </summary>
        public bool HasWarning { get; private set; }

        /// <summary>Code-like reason of the current warning (empty when <see cref="HasWarning"/> is false).</summary>
        public string WarningReason { get; private set; } = string.Empty;

        /// <summary>Raised whenever <see cref="HasWarning"/> or <see cref="WarningReason"/> changes. Parameters: (hasWarning, reason).</summary>
        public event Action<bool, string> WarningChanged;

        /// <summary>
        /// Sets or clears the warning. Repeated identical calls are ignored, so concrete machines may
        /// call this every frame.
        /// </summary>
        protected void SetWarning(bool hasWarning, string reason = "")
        {
            string normalizedReason = hasWarning ? (reason ?? string.Empty) : string.Empty;
            if (HasWarning == hasWarning && WarningReason == normalizedReason)
            {
                return;
            }

            HasWarning = hasWarning;
            WarningReason = normalizedReason;
            WarningChanged?.Invoke(hasWarning, normalizedReason);
        }

        /// <summary>
        /// Convenience for controllers (line controller, tunnel stations): starts the machine from
        /// Ready, or from Stopped via <see cref="ResetToIdle"/>. Ignored in every other state, so it
        /// never bypasses Fault/Maintenance.
        /// </summary>
        public void RequestRun()
        {
            if (_currentState == MachineState.Stopped)
            {
                ResetToIdle();
            }

            StartRun();
        }

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
