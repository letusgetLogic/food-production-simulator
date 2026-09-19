namespace Game.Production
{
    /// <summary>
    /// Represents the lifecycle state of an <see cref="IMachine"/>.
    /// </summary>
    public enum MachineState
    {
        /// <summary>
        /// The machine is powered but not processing. Safe to start or enter maintenance.
        /// </summary>
        Idle,

        /// <summary>
        /// The machine is transitioning from Idle to Running (warm-up, homing, self-check, etc.).
        /// </summary>
        Starting,

        /// <summary>
        /// The machine is actively processing product.
        /// </summary>
        Running,

        /// <summary>
        /// The machine is transitioning from Running back to Stopped (winding down, finishing the current cycle).
        /// </summary>
        Stopping,

        /// <summary>
        /// The machine is fully powered down / halted. No processing occurs.
        /// </summary>
        Stopped,

        /// <summary>
        /// The machine has detected an error condition and requires attention before it can resume.
        /// </summary>
        Fault,

        /// <summary>
        /// The machine is undergoing maintenance, typically to clear a Fault and reset internal conditions.
        /// </summary>
        Maintenance
    }
}
