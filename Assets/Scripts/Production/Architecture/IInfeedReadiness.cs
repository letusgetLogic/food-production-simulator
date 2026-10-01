namespace Game.Production
{
    /// <summary>
    /// Optional contract for elements that can tell an upstream conveyor whether they will take a
    /// product right now (belts, tunnel stations). Machines that do not implement it are judged by
    /// <see cref="InfeedReadinessUtility.IsReady"/>: Running + an optional free infeed PresenceSensor.
    /// </summary>
    public interface IInfeedReadiness
    {
        /// <summary>True if a product handed over now would be carried/processed immediately.</summary>
        bool IsReadyForInfeed { get; }
    }

    /// <summary>
    /// Shared readiness rule used by <see cref="AccumulationZone"/> and <see cref="ConveyorBelt"/>.
    /// Sensors only deliver raw values - the decision what "ready" means lives here.
    /// </summary>
    public static class InfeedReadinessUtility
    {
        /// <param name="downstream">Next element in the line. Null = no state check.</param>
        /// <param name="infeedSensor">Optional sensor at the downstream infeed. Occupied = not ready.</param>
        public static bool IsReady(MachineBase downstream, PresenceSensor infeedSensor)
        {
            if (infeedSensor != null && infeedSensor.CurrentValue)
            {
                return false;
            }

            if (downstream == null)
            {
                return true;
            }

            if (downstream is IInfeedReadiness readiness)
            {
                return readiness.IsReadyForInfeed;
            }

            return downstream.CurrentState == MachineState.Running;
        }
    }
}

namespace Game.Production
{
    /// <summary>
    /// Elements that need to know the next element of the line (belts, the portioner). Wired by
    /// ConveyorLineController in material-flow order, or set by hand in the inspector.
    /// </summary>
    public interface IDownstreamLink
    {
        void SetDownstream(MachineBase downstream);
    }
}
