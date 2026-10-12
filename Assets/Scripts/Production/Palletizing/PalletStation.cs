using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Production
{
    /// <summary>
    /// End station (Palettierer): good cartons from the line end are stacked automatically on the pallet that
    /// stands on the marked place (snap, no animation; layer by layer, see <see cref="Pallet"/>). Scrap and
    /// unfinished products are sorted out (removed by the <see cref="LineEndSink"/>).
    ///
    /// The worker brings an empty pallet (by hand or with the pallet jack) and takes full ones away with the
    /// pallet jack. Without a pallet, or with a full one, the station is not ready for infeed: the last belt
    /// holds the cartons and the line backs up until a new empty pallet stands on the place (warning, amber,
    /// no fault - it is normal operation).
    /// </summary>
    public class PalletStation : MachineBase, IInfeedReadiness, IProductReceiver, IMachineParameterSource, ISaveableState
    {
        /// <summary>Content messages this station reports via NotifyContentChanged.</summary>
        public enum PalletizerInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            WaitingForPallet,
            PalletFull,
            CartonPlaced,
            ScrapRemoved,
            PalletPlaced
        }

        public const string WarningReasonNoPallet = "NoPallet";
        public const string WarningReasonPalletFull = "PalletFull";

        [Header("Pallet place")]
        [Tooltip("Where the pallet has to stand (origin = floor, forward = pallet orientation).")]
        [SerializeField] private Transform _place;
        [Tooltip("A pallet whose centre is within this distance of the place is taken (and snapped exactly).")]
        [Min(0.05f)] [SerializeField] private float _placeTolerance = 0.35f;
        [Tooltip("Max. yaw difference to the place (modulo 90°).")]
        [Range(0f, 45f)] [SerializeField] private float _angleTolerance = 15f;

        [Header("Timing")]
        [Min(0f)] [SerializeField] private float _startupDurationSeconds = 0.5f;
        [Min(0f)] [SerializeField] private float _shutdownDurationSeconds = 0.5f;

        [Header("HMI Content")]
        [SerializeField] private List<Content<PalletizerInfo>> _contents = new List<Content<PalletizerInfo>>();

        private Pallet _pallet;
        private int _palletsCompleted;
        private int _cartonsTotal;
        private int _scrapCount;

        private PalletizerInfo? _lastReportedInfo;
        private string _lastReportedDetail;
        private Coroutine _routine;
        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

        /// <summary>Raised when a pallet has been filled completely.</summary>
        public event Action<Pallet> PalletCompleted;

        public Pallet CurrentPallet => _pallet;
        public int PalletsCompleted => _palletsCompleted;
        public int CartonsTotal => _cartonsTotal;
        public int ScrapCount => _scrapCount;

        /// <inheritdoc />
        public bool IsReadyForInfeed => CurrentState == MachineState.Running && _pallet != null && !_pallet.IsFull;

        /// <inheritdoc />
        public bool IsAccepting => IsReadyForInfeed;

        // ---- Unity lifecycle ----

        private void Update()
        {
            DetectPallet();

            if (CurrentState == MachineState.Running)
            {
                UpdateWarning();
            }
        }

        private void DetectPallet()
        {
            if (_pallet != null && (_pallet.IsCarried || _pallet.IsHeldByHand || !IsInPlace(_pallet)))
            {
                _pallet.SetOnStation(false);
                _pallet = null;
            }

            if (_pallet != null || _place == null)
            {
                return;
            }

            foreach (Pallet pallet in Pallet.All)
            {
                if (pallet.IsCarried || pallet.IsHeldByHand || !IsInPlace(pallet) || pallet.HasPalletOnTop())
                {
                    continue;
                }

                if (!pallet.Body.isKinematic && pallet.Body.linearVelocity.sqrMagnitude > 0.01f)
                {
                    continue; // still settling
                }

                Snap(pallet);
                _pallet = pallet;
                pallet.SetOnStation(true);
                Report(PalletizerInfo.PalletPlaced, $"{pallet.Count}/{pallet.Capacity}", force: true);
                break;
            }
        }

        private bool IsInPlace(Pallet pallet)
        {
            Vector3 offset = pallet.transform.position - _place.position;
            if (new Vector2(offset.x, offset.z).magnitude > _placeTolerance || Mathf.Abs(offset.y) > 0.1f)
            {
                return false;
            }

            float yaw = Mathf.Abs(Mathf.DeltaAngle(pallet.transform.eulerAngles.y, _place.eulerAngles.y)) % 90f;
            return Mathf.Min(yaw, 90f - yaw) <= _angleTolerance;
        }

        /// <summary>Aligns the pallet exactly to the place (yaw to the nearest 90° step).</summary>
        private void Snap(Pallet pallet)
        {
            float delta = Mathf.DeltaAngle(_place.eulerAngles.y, pallet.transform.eulerAngles.y);
            float yaw = _place.eulerAngles.y + Mathf.Round(delta / 90f) * 90f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            pallet.transform.SetPositionAndRotation(_place.position, rotation);
            pallet.Body.position = _place.position;
            pallet.Body.rotation = rotation;
        }

        // ---- Products (IProductReceiver) ----

        /// <inheritdoc />
        public bool TryReceive(ProductToken token)
        {
            ProductInstance product = token != null ? token.Product : null;
            if (product == null || product.QualityPassed == false || product.CurrentState != ProductState.PackagedPizza)
            {
                _scrapCount++;
                Report(PalletizerInfo.ScrapRemoved, product?.CurrentState.ToString(), force: true);
                return false; // scrap: removed by the line end
            }

            if (!IsReadyForInfeed || !_pallet.TryAdd(token))
            {
                return false;
            }

            _cartonsTotal++;
            Report(PalletizerInfo.CartonPlaced, $"{_pallet.Count}/{_pallet.Capacity}");

            if (_pallet.IsFull)
            {
                _palletsCompleted++;
                PalletCompleted?.Invoke(_pallet);
                Report(PalletizerInfo.PalletFull, $"{_pallet.Count}/{_pallet.Capacity}", force: true);
            }
            return true;
        }

        private void UpdateWarning()
        {
            if (_pallet == null)
            {
                SetWarning(true, WarningReasonNoPallet);
                Report(PalletizerInfo.WaitingForPallet);
                return;
            }

            if (_pallet.IsFull)
            {
                SetWarning(true, WarningReasonPalletFull);
                Report(PalletizerInfo.PalletFull, $"{_pallet.Count}/{_pallet.Capacity}");
                return;
            }

            SetWarning(false);
        }

        // ---- HMI terminal values (IMachineParameterSource) ----

        /// <inheritdoc />
        public IReadOnlyList<MachineParameter> Parameters => _parameters ??= new List<MachineParameter>();

        /// <inheritdoc />
        public IReadOnlyList<MachineReadout> Readouts
        {
            get
            {
                if (_readouts == null)
                {
                    BuildReadouts();
                }
                return _readouts;
            }
        }

        private void BuildReadouts()
        {
            _readouts = new List<MachineReadout>
            {
                new MachineReadout("pallet", "Pallet", "",
                    () => _pallet != null ? LocText.Get("hmi.yes", "Yes") : LocText.Get("hmi.no", "No"),
                    () => _pallet != null ? MachineValueLevel.Normal : MachineValueLevel.Warning),
                new MachineReadout("cartons", "Cartons", "pcs",
                    () => _pallet != null ? $"{_pallet.Count} / {_pallet.Capacity}" : "--",
                    () => _pallet == null ? MachineValueLevel.Inactive
                        : _pallet.IsFull ? MachineValueLevel.Warning : MachineValueLevel.Normal,
                    MachineReadoutSlot.FillLevel),
                new MachineReadout("palletWeight", "Pallet weight", "kg",
                    () => _pallet != null ? _pallet.WeightKilograms.ToString("0.0") : "--"),
                new MachineReadout("palletsCompleted", "Pallets completed", "pcs",
                    () => _palletsCompleted.ToString()),
                new MachineReadout("cartonsTotal", "Cartons stacked", "pcs",
                    () => _cartonsTotal.ToString()),
                new MachineReadout("scrap", "Scrap removed", "pcs",
                    () => _scrapCount.ToString(),
                    () => _scrapCount > 0 ? MachineValueLevel.Warning : MachineValueLevel.Normal),
            };
        }

        // ---- Content messages ----

        private void Report(PalletizerInfo info, string detail = null, bool force = false)
        {
            if (!force && _lastReportedInfo == info && _lastReportedDetail == detail)
            {
                return;
            }

            _lastReportedInfo = info;
            _lastReportedDetail = detail;
            NotifyContentChanged(string.IsNullOrEmpty(detail) ? Info(info) : detail + "\n" + Info(info));
        }

        private string Info(PalletizerInfo info)
        {
            string localized = _contents.Find(c => c.State == info)?.Info;
            return string.IsNullOrEmpty(localized) ? LocText.Info("palletizer", info, FallbackText(info)) : localized;
        }

        private static string FallbackText(PalletizerInfo info) => info switch
        {
            PalletizerInfo.WaitingForPallet => "Waiting for an empty pallet",
            PalletizerInfo.PalletFull => "Pallet full - bring an empty pallet",
            PalletizerInfo.CartonPlaced => "Carton stacked",
            PalletizerInfo.ScrapRemoved => "Scrap sorted out",
            PalletizerInfo.PalletPlaced => "Pallet in place",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady() => Report(PalletizerInfo.Ready, force: true);

        protected override void OnEnterStarting()
        {
            Report(PalletizerInfo.Starting, force: true);
            Restart(After(_startupDurationSeconds, MachineState.Running));
        }

        protected override void OnEnterRunning() => Report(PalletizerInfo.Running, force: true);

        protected override void OnEnterStopping()
        {
            Report(PalletizerInfo.Stopping, force: true);
            SetWarning(false);
            Restart(After(_shutdownDurationSeconds, MachineState.Stopped));
        }

        protected override void OnEnterStopped() => Report(PalletizerInfo.Stopped, force: true);

        protected override void OnEnterFault(string reason)
        {
            Report(PalletizerInfo.Fault, force: true);
            SetWarning(false);
            Restart(null);
        }

        protected override void OnEnterMaintenance() => Report(PalletizerInfo.Maintenance, force: true);

        private IEnumerator After(float seconds, MachineState target)
        {
            yield return new WaitForSeconds(seconds);
            _routine = null;
            SetState(target);
        }

        private void Restart(IEnumerator body)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (body != null)
            {
                _routine = StartCoroutine(body);
            }
        }

        private void OnDrawGizmos()
        {
            if (_place == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.5f);
            Gizmos.matrix = _place.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.07f, new Vector3(1.5f, 0.14f, 1.5f));
            Gizmos.matrix = Matrix4x4.identity;
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("palletsCompleted", _palletsCompleted);
            values.Set("cartonsTotal", _cartonsTotal);
            values.Set("scrap", _scrapCount);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            _palletsCompleted = values.GetInt("palletsCompleted", _palletsCompleted);
            _cartonsTotal = values.GetInt("cartonsTotal", _cartonsTotal);
            _scrapCount = values.GetInt("scrap", _scrapCount);
        }
    }
}
