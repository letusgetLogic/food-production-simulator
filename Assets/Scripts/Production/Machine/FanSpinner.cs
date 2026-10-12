using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Visual only: turns a fan rotor while its machine is running (cooling tunnel fans). Ramps up/down, and
    /// stands still during a simulated cooling unit failure - a visible cue for the training fault.
    /// </summary>
    public class FanSpinner : MonoBehaviour
    {
        [SerializeField] private MachineBase _machine;

        [Tooltip("Visual speed in revolutions per minute (kept low so the blades do not strobe).")]
        [Min(0f)] [SerializeField] private float _rpm = 120f;

        [Tooltip("Seconds from standstill to full speed (and back).")]
        [Min(0.01f)] [SerializeField] private float _rampSeconds = 1.5f;

        [SerializeField] private Vector3 _localAxis = Vector3.up;

        private float _currentRpm;

        private void Update()
        {
            bool running = _machine == null || _machine.CurrentState == MachineState.Running;
            if (_machine is ContinuousProcessStation station && station.HasHeaterFailure)
            {
                running = false;
            }

            float target = running ? _rpm : 0f;
            _currentRpm = Mathf.MoveTowards(_currentRpm, target, _rpm / _rampSeconds * Time.deltaTime);
            if (_currentRpm > 0f)
            {
                transform.Rotate(_localAxis, _currentRpm * 6f * Time.deltaTime, Space.Self);
            }
        }
    }
}
