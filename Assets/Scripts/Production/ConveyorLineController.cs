using AYellowpaper;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Owns a chain of IConveyor segments and starts/stops them together as one
    /// line. Physical counterpart: a control box between two machines.
    ///
    /// Has no logic of its own about *when* to run - that decision belongs
    /// entirely to whoever is downstream (typically the receiving machine,
    /// which knows its own MachineState and reads its own intake sensor). This
    /// controller only propagates StartLine()/StopLine() to every segment it
    /// owns.
    ///
    /// Physical hand-off between segments (and from the last segment into the
    /// receiving machine) is wired here once, via the internal ILoadReceiver
    /// chain - not exposed on IConveyor itself. Feeding a brand-new load onto
    /// the first segment (e.g. a machine's freshly produced output) goes
    /// through TryFeedLine, the one place outside the chain that needs to
    /// reach in.
    /// </summary>
    public class ConveyorLineController : MonoBehaviour
    {
        [Tooltip("Segments in conveying direction, from source to destination. Each must implement IConveyor and ILoadReceiver (SlotConveyor does).")]
        [SerializeField] private List<InterfaceReference<IConveyor>> _segmentReferences = new();

        private readonly List<IConveyor> _segments = new();
        private ILoadReceiver _finalReceiver; // set by the receiving machine, see SetFinalReceiver

        private void Awake()
        {
            ResolveSegments();
            LinkSegmentChain();
        }

        /// <summary>
        /// Starts every segment in this line. Call this from whatever sits
        /// downstream once it is ready to accept the next load.
        /// </summary>
        public void StartLine()
        {
            foreach (var segment in _segments)
            {
                segment.StartRunning();
            }
        }

        /// <summary>
        /// Stops every segment in this line. Call this from whatever sits
        /// downstream once it is no longer ready - e.g. its intake presence
        /// sensor detects a load has already arrived, it isn't running, or it
        /// has a fault.
        /// </summary>
        public void StopLine()
        {
            foreach (var segment in _segments)
            {
                segment.StopRunning();
            }
        }

        /// <summary>
        /// Called once by the receiving machine (or the next ConveyorLineController,
        /// if this line feeds into another line rather than a machine) to
        /// receive whatever the last segment in this chain hands off.
        /// </summary>
        public void SetFinalReceiver(ILoadReceiver receiver)
        {
            _finalReceiver = receiver;

            if (_segments.Count > 0)
            {
                LinkLastSegmentTo(_finalReceiver);
            }
        }

        /// <summary>
        /// Entry point for whoever produces a new physical load at the source
        /// end of this line (typically a machine's output stage). Not part of
        /// IConveyor - a producer needs a concrete place to hand a load to,
        /// and this line's first segment is it.
        /// </summary>
        public bool TryFeedLine(GameObject load)
        {
            if (_segments.Count == 0 || _segments[0] is not ILoadReceiver firstReceiver)
            {
                return false;
            }

            return firstReceiver.TryReceiveLoad(load);
        }

        private void ResolveSegments()
        {
            _segments.Clear();

            foreach (var reference in _segmentReferences)
            {
                var conveyor = reference.Value;

                if (conveyor != null)
                {
                    _segments.Add(conveyor);
                }
                else
                {
                    Debug.LogError(
                        $"ConveyorLineController '{name}': found an empty or unresolved IConveyor reference and skipped it.",
                        this);
                }
            }
        }

        private void LinkSegmentChain()
        {
            for (int i = 0; i < _segments.Count - 1; i++)
            {
                LinkSegmentTo(i, _segments[i + 1] as ILoadReceiver);
            }
        }

        private void LinkSegmentTo(int index, ILoadReceiver receiver)
        {
            if (_segments[index] is SlotConveyor slotConveyor && receiver != null)
            {
                slotConveyor.LinkNext(receiver);
            }
        }

        private void LinkLastSegmentTo(ILoadReceiver receiver)
        {
            if (_segments[^1] is SlotConveyor lastSlotConveyor)
            {
                lastSlotConveyor.LinkNext(receiver);
            }
        }
    }
}