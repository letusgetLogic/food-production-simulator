namespace Game.Sensors
{
    /// <summary>
    /// Non-generic base contract shared by every sensor type (Presence, Weight, Temperature,
    /// Level, Motor, Jam), independent of the concrete value type each sensor reports.
    /// Enables polymorphic handling of heterogeneous sensor collections (e.g. an HMI panel or a
    /// FaultSystem that needs to iterate "all sensors on this machine" without knowing their
    /// individual value types).
    /// </summary>
    public interface ISensor
    {
        /// <summary>Unique identifier for this sensor instance (e.g. for HMI display, logging, fault reporting).</summary>
        string SensorId { get; }

        /// <summary>Whether the sensor's current reading is within its configured normal/expected range.</summary>
        bool IsWithinNormalRange { get; }

        /// <summary>Timestamp (in seconds since startup, i.e. Time.time) of the most recent reading.</summary>
        float LastReadingTimestamp { get; }

        /// <summary>Forces the sensor to take a fresh reading.</summary>
        void UpdateReading();
    }

    /// <summary>
    /// Generic extension of <see cref="ISensor"/> for sensors that report a typed measurement
    /// value, e.g. <c>ISensor&lt;bool&gt;</c> for a Presence sensor or <c>ISensor&lt;float&gt;</c>
    /// for Weight/Temperature sensors.
    /// </summary>
    /// <typeparam name="T">The type of value this sensor reports (e.g. bool, float).</typeparam>
    public interface ISensor<out T> : ISensor
    {
        /// <summary>The current measured value of this sensor.</summary>
        T CurrentValue { get; }
    }
}
