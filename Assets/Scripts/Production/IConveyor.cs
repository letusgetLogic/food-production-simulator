using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Contract for a conveyor segment that physically transports load(s) from one station to the
    /// next. The conveyor deliberately has no knowledge of <see cref="ProductInstance"/> or recipes -
    /// it only tracks physical occupancy (is there something on this slot?) and two distinct
    /// "cannot move forward" conditions (see <see cref="IsJammed"/> vs. <see cref="IsBackedUp"/>).
    /// Identifying *what* is riding on the belt is the responsibility of the sensors and stations
    /// at either end (e.g. a Presence/Weight sensor scans the load and, via its attached
    /// <see cref="ProductToken"/>, resolves the corresponding ProductInstance).
    /// This is a pure contract definition - no movement/timing behavior is implemented here.
    /// </summary>
    public interface IConveyor
    {
        /// <summary>
        /// Number of discrete load slots along this conveyor segment (belt length dependent).
        /// </summary>
        int SlotCount { get; }

        /// <summary>
        /// Physical occupancy per slot, ordered from the input end (index 0) to the output end
        /// (last index). True means a load currently sits in that slot. Purely physical - carries
        /// no information about what the load is.
        /// </summary>
        IReadOnlyList<bool> SlotOccupancy { get; }

        /// <summary>
        /// True if this conveyor's own jam sensor detects a physical obstruction/malfunction
        /// (e.g. a load stuck sideways, belt motor fault). This is an abnormal condition and is
        /// expected to be surfaced to the later FaultSystem - independent of whether any
        /// downstream station is running or not.
        /// </summary>
        bool IsJammed { get; }

        /// <summary>
        /// True if a load is ready and waiting at the output end but cannot currently be released
        /// because the next station/conveyor is not accepting loads right now (e.g. its MachineState
        /// is not Running, or its input is full). This is normal operational backpressure, not a
        /// fault - it is expected to resolve on its own once the downstream component becomes ready
        /// again, and should not by itself trigger the FaultSystem.
        /// </summary>
        bool IsBackedUp { get; }

        /// <summary>Whether the input slot is free and able to accept a new load right now.</summary>
        bool CanAcceptLoad { get; }

        /// <summary>
        /// Attempts to place a physical load onto the input end of this conveyor (e.g. handed off
        /// from an upstream station or conveyor).
        /// </summary>
        /// <param name="load">The physical load object to accept. May carry a <see cref="ProductToken"/>
        /// component for identification by downstream sensors, but the conveyor does not inspect it.</param>
        /// <returns>True if the load was accepted; false if <see cref="CanAcceptLoad"/> was false.</returns>
        bool TryAcceptLoad(GameObject load);

        /// <summary>
        /// Attempts to hand off the load currently sitting at the output end to the next
        /// station/conveyor. Fails while <see cref="IsJammed"/> or <see cref="IsBackedUp"/> is true.
        /// Does not affect any other loads still travelling on this conveyor.
        /// </summary>
        /// <param name="load">The load that was released, or null if none was released.</param>
        /// <returns>True if a load was successfully released; false if none had reached the output end or the handoff failed.</returns>
        bool TryReleaseLoad(out GameObject load);
    }
}