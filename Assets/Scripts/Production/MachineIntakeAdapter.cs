
using UnityEngine;
using UnityEngine.Events;

namespace Game.Production
{
    /// <summary>
    /// Bridges a ConveyorLineController's final segment to a receiving
    /// machine (Portionierer, Formanlage, ...) without requiring the machine
    /// class itself to implement ILoadReceiver.
    ///
    /// Cross-team point (Tag 4, not yet resolved with Dev A): Dev A's machine
    /// scripts don't implement ILoadReceiver yet. Rather than block on that,
    /// this adapter sits as its own component on the receiving machine's
    /// GameObject - conveyor-side, it's a plain ILoadReceiver that
    /// ConveyorLineController.SetFinalReceiver() can target today. Machine-
    /// side, it exposes OnLoadReceived (UnityEvent<GameObject>) and an
    /// optional CanAcceptLoad delegate that Dev A can hook up once the
    /// Portionierer/Formanlage intake logic exists. Until that hookup
    /// happens, CanAcceptLoad defaults to "always true" so the line can be
    /// wired and tested end-to-end now; TryReceiveLoad always succeeds and
    /// fires the event, so nothing here quietly no-ops. This intentionally
    /// does NOT touch IProductProcessor or ProductState - that mapping is
    /// exactly the part Dev A still needs to own.
    /// </summary>
    public class MachineIntakeAdapter : MonoBehaviour, ILoadReceiver
    {
        [Tooltip("Fired whenever a load is accepted onto this machine's intake. " +
                 "Dev A: hook the receiving machine's actual intake handling here.")]
        [SerializeField] private UnityEvent<GameObject> _onLoadReceived;

        /// <summary>
        /// Optional gate a machine script can set at runtime (e.g. "am I
        /// Idle/Running with a free intake slot?"). Null means "not wired up
        /// yet" - defaults to always accepting so the conveyor line isn't
        /// blocked while this integration is still open.
        /// </summary>
        public System.Func<bool> CanAcceptLoad { get; set; }

        public bool TryReceiveLoad(GameObject load)
        {
            if (CanAcceptLoad != null && !CanAcceptLoad())
            {
                return false;
            }

            _onLoadReceived?.Invoke(load);
            return true;
        }
    }

}