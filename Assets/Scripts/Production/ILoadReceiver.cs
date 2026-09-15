
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Deliberately NOT part of the public IConveyor contract — IConveyor stays
    /// "dumb" (StartRunning/StopRunning/IsRunning/IsJammed only). This is the
    /// internal plumbing that lets a conveyor segment push a completed load onto
    /// whatever sits next in line, whether that's another SlotConveyor or a
    /// receiving machine's intake. Wired by ConveyorLineController, never called
    /// by application code directly.
    /// </summary>
    public interface ILoadReceiver
    {
        /// <summary>
        /// Attempts to physically place a load at this receiver's entry point.
        /// Returns false if there is no room right now — the caller is expected
        /// to simply hold onto the load and try again next frame (no exception,
        /// no queueing logic on the receiver's side).
        /// </summary>
        bool TryReceiveLoad(GameObject load);
    }

}