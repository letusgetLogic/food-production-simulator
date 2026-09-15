using System.Collections.Generic;

namespace Game.Production
{
    /// <summary>
    /// Contract for a single conveyor segment. Deliberately minimal - a
    /// conveyor only knows whether it is running and whether it is jammed. It
    /// has no opinion of its own about when it should run; running state is
    /// commanded from outside (typically by a ConveyorLineController acting as
    /// the "control box" between two machines, itself driven by the receiving
    /// machine's readiness).
    ///
    /// Anything about how a concrete conveyor represents its physical load
    /// (slots, occupancy, positions, ...) is an implementation detail of that
    /// concrete type - e.g. SlotConveyor - and intentionally not part of this
    /// interface.
    /// </summary>
    public interface IConveyor
    {
        bool IsRunning { get; }

        /// <summary>
        /// True only on a genuine physical fault: the belt is commanded to run
        /// but a load is not advancing (something is physically stuck). Never
        /// true just because the belt was intentionally stopped via
        /// StopRunning() - that is normal operation, not an error.
        /// </summary>
        bool IsJammed { get; }

        void StartRunning();

        void StopRunning();
    }
}
