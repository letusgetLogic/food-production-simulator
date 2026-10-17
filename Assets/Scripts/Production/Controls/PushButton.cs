using System;
using Game.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Production
{
    /// <summary>
    /// Momentary push button (Taster): every interaction (E) sends exactly one <see cref="Pressed"/> signal; the
    /// cap moves in briefly and springs back. Start (I) / Stop (O) are listened to by the <see cref="MachineControlPanel"/>;
    /// machine-specific buttons (Button_Up / Button_Down: drum, lift) call their machine through
    /// <see cref="_onPressed"/> (inspector), special buttons override <see cref="OnPressed"/> (refill).
    /// </summary>
    public class PushButton : InteractableBase
    {
        [Tooltip("Moving part (cap). Empty = this object itself moves.")]
        [SerializeField] private Transform _cap;
        [Tooltip("Travel of the cap into the panel (local +forward of the cap's parent), metres.")]
        [Min(0f)] [SerializeField] private float _travel = 0.008f;
        [Tooltip("How long the cap stays in after a press, seconds.")]
        [Min(0f)] [SerializeField] private float _pressSeconds = 0.18f;

        [Tooltip("Machine action of this button, e.g. MixerMachine.TipDrum or PortionerMachine.RaiseLift.")]
        [SerializeField] private UnityEvent _onPressed = new UnityEvent();

        private Vector3 _capRest;
        private float _pressTimer;

        /// <summary>One signal per press.</summary>
        public event Action<PushButton> Pressed;

        protected virtual void Awake()
        {
           if (_cap != null)
            {
                _capRest = _cap.localPosition;
            }
           
        }

        protected sealed override void OnInteract(IInteractor interactor)
        {
            _pressTimer = _pressSeconds;
            OnPressed();
            _onPressed?.Invoke();
            Pressed?.Invoke(this);
        }

        /// <summary>Machine-specific action of a subclass; default none (the panel listens to <see cref="Pressed"/>).</summary>
        protected virtual void OnPressed() { }

        protected virtual void Update()
        {
            if (_cap == null)
            {
                return;
            }

            if (_travel <= 0f)
            {
                return;
            }

            _pressTimer -= Time.deltaTime;
            Vector3 target = _capRest + (_pressTimer > 0f ? Vector3.forward * _travel : Vector3.zero);
       
            if (_cap.localPosition != target)
            {
                _cap.localPosition = Vector3.MoveTowards(_cap.localPosition, target, 0.2f * Time.deltaTime);
            }
        }
    }
}
