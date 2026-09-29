using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(fileName = "HoldPoint", menuName = "Channels/Hold Point")]
    public class SO_HoldPoint : ScriptableObject
    {
        public Transform HoldPoint { get; private set; }

        public void SetHoldPoint(Transform holdPoint)
        {
            HoldPoint = holdPoint;
        }
    }
}