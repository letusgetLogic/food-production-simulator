using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// A held tool the player can stand on and drive (pallet jack as a scooter). While <see cref="IsRiding"/>
    /// the player controller hands its throttle to the tool and places the player at <see cref="RiderPosition"/>.
    /// The tool steers itself towards the rider's view direction.
    /// </summary>
    public interface IRideable
    {
        bool IsRiding { get; }

        /// <summary>Feet position of the rider (world), read every frame.</summary>
        Vector3 RiderPosition { get; }

        /// <summary>What the rider looks at when getting on (e.g. the handle grip).</summary>
        Vector3 RiderLookTarget { get; }

        /// <summary>Where the rider steps off (world, feet).</summary>
        Vector3 DismountPosition { get; }

        /// <summary>Throttle from the rider, -1 (backwards) .. 1 (forwards), set every frame.</summary>
        void Drive(float throttle);
    }
}
