using Game.Core;
using System;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Production
{
    /// <summary>
    /// Latching emergency stop (Not-Halt, ISO 13850) with two states: released and engaged. Every interaction (E)
    /// switches the state and sends exactly one signal - <see cref="Engaged"/> or <see cref="Released"/>
    /// (press = engage, press again = twist-to-release). The mushroom stays in while engaged.
    /// Releasing never restarts anything; that is up to the listener (<see cref="MachineControlPanel"/>).
    /// </summary>
    public class EmergencyStopButton : InteractableBase
    {
        [Tooltip("Moving part (mushroom). Empty = this object itself moves.")]
        [SerializeField] private Transform _cap;
        [Tooltip("Travel of the mushroom into the panel (local +forward of its parent), metres.")]
        [Min(0f)] [SerializeField] private float _travel = 0.012f;
        [SerializeField] private LocalizedString _releaseKey;
        public virtual string ReleaseText
        {
            get
            {
                string fallback = Localizer("Release emergency stop");
                return _releaseKey == null || _releaseKey.IsEmpty ? fallback : LocText.Now(_releaseKey, fallback);
            }
        }
        /// <summary>While engaged always "Release emergency stop"; otherwise the inspector text or the default.</summary>
        public override string InteractionText => IsEngaged ? ReleaseText : base.InteractionText;

        private Transform Cap => _cap != null ? _cap : transform;
        private Vector3 _capRest;

        public bool IsEngaged { get; private set; }

        public event Action<EmergencyStopButton> Engaged;
        public event Action<EmergencyStopButton> Released;

        private void Awake()
        {
            _capRest = Cap.localPosition;
        }

        protected override void OnInteract(IInteractor interactor) => SetEngaged(!IsEngaged);

        /// <summary>Sets the state (automation, tests); signals only on a real change.</summary>
        public void SetEngaged(bool engaged)
        {
            if (IsEngaged == engaged)
            {
                return;
            }

            IsEngaged = engaged;
            if (engaged)
            {
                Engaged?.Invoke(this);
            }
            else
            {
                Released?.Invoke(this);
            }
        }

        private void Update()
        {
            Vector3 target = _capRest + (IsEngaged ? Vector3.forward * _travel : Vector3.zero);
            Transform cap = Cap;
            if (cap.localPosition != target)
            {
                cap.localPosition = Vector3.MoveTowards(cap.localPosition, target, 0.2f * Time.deltaTime);
            }
        }
    }
}
