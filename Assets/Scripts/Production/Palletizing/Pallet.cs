using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// A pallet (Palette). The palletizer stacks cartons on it in layers: <see cref="StacksX"/> x
    /// <see cref="StacksZ"/> stacks, <see cref="Layers"/> cartons high (default 2 x 2 x 12 = 48).
    /// Transport: an empty pallet can be carried by hand (HoldInteractable, only while empty and nothing
    /// lies on it) or with the pallet jack; a loaded pallet only with the pallet jack.
    ///
    /// The pallet is a scene root object (its hierarchy path identifies it in save files) and is never
    /// re-parented: the jack moves it kinematically, the cartons are its children.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pallet : MonoBehaviour, ISaveableState
    {
        /// <summary>All active pallets (few - linear searches are fine).</summary>
        public static readonly List<Pallet> All = new List<Pallet>();

        [Header("Stacking layout")]
        [Min(1)] [SerializeField] private int _stacksX = 2;
        [Min(1)] [SerializeField] private int _stacksZ = 2;
        [Min(1)] [SerializeField] private int _layers = 12;
        [Tooltip("Distance between stack centres (carton width + gap), in metres.")]
        [SerializeField] private Vector2 _stackPitch = new Vector2(0.73f, 0.73f);
        [Tooltip("Height of one carton layer, in metres.")]
        [Min(0.01f)] [SerializeField] private float _layerHeight = 0.088f;
        [Tooltip("Height of the loading surface (deck top) above the pallet origin.")]
        [SerializeField] private float _deckHeight = 0.1445f;

        [Header("Weight")]
        [Tooltip("Own weight of the empty pallet (kg).")]
        [Min(0f)] [SerializeField] private float _tareKilograms = 25f;
        [Tooltip("Packaging weight per carton (g), added to the product's dough + ingredients.")]
        [Min(0f)] [SerializeField] private float _cartonGrams = 50f;

        [Header("Physics")]
        [Tooltip("Start parked (kinematic) on a pallet stack - pallets placed in the stacks of the store in the scene. " +
                 "Kinematic only while on a stack; everywhere else a pallet is a normal physics body.")]
        [SerializeField] private bool _startParked = true;

        [Header("Save/Load")]
        [Tooltip("Carton (pizza prefab) used to rebuild the load after loading a save file.")]
        [SerializeField] private ProductToken _cartonPrefab;

        private readonly List<ProductToken> _cartons = new List<ProductToken>();
        private Rigidbody _body;
        private HoldInteractable _hold;
        private float _loadGrams;
        private bool _parked;
        private float _interactableCheckTimer;

        public int StacksX => _stacksX;
        public int StacksZ => _stacksZ;
        public int Layers => _layers;
        public int Capacity => _stacksX * _stacksZ * _layers;
        public int Count => _cartons.Count;
        public bool IsEmpty => _cartons.Count == 0;
        public bool IsFull => _cartons.Count >= Capacity;
        public Rigidbody Body => _body != null ? _body : _body = GetComponent<Rigidbody>();

        /// <summary>Total weight incl. pallet, in kg.</summary>
        public float WeightKilograms => _tareKilograms + _loadGrams / 1000f;

        /// <summary>True while a pallet jack has it on its forks.</summary>
        public bool IsCarried { get; private set; }

        /// <summary>True while it sits on the palletizer place (kept still there).</summary>
        public bool IsOnStation { get; private set; }

        /// <summary>True while the player carries it by hand.</summary>
        public bool IsHeldByHand => _hold != null && _hold.IsHeld;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _hold = GetComponent<HoldInteractable>();
            _interactableCheckTimer = Random.value * 0.25f; // spread the checks over frames
            if (_startParked)
            {
                _parked = true;
                UpdateKinematic();
            }
        }

        private void OnEnable()
        {
            All.Add(this);
            if (_hold != null)
            {
                _hold.OnReleased += HandleReleasedByHand;
            }
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (_hold != null)
            {
                _hold.OnReleased -= HandleReleasedByHand;
            }
        }

        // Q while carrying an empty pallet next to a pallet stack puts it on top of the stack (kinematic);
        // anywhere else it simply falls as a normal physics body.
        private void HandleReleasedByHand(HoldInteractable _) => TryAddToNearbyStack();

        /// <summary>Puts this (empty) pallet on the nearest pallet stack within reach. Also used by tests/automation.</summary>
        public bool TryAddToNearbyStack() => IsEmpty && !IsCarried && PalletStack.TryStackNear(this);

        /// <summary>Places the pallet exactly (e.g. on top of a stack) and holds it there until it is carried again.</summary>
        public void PlaceParked(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            _parked = true;
            Body.isKinematic = true;
        }

        private void Update()
        {
            if (_hold == null)
            {
                return;
            }

            if (_hold.IsHeld)
            {
                _parked = false; // HoldInteractable drives the body
                return;
            }

            _interactableCheckTimer -= Time.deltaTime;
            if (_interactableCheckTimer <= 0f)
            {
                _interactableCheckTimer = 0.25f;
                // By hand only while empty, free (not on the jack) and nothing lies on top of it.
                _hold.SetInteractable(IsEmpty && !IsCarried && !HasPalletOnTop());
            }
        }

        // ---- Stacking ----

        /// <summary>Local position (pallet space) of carton slot <paramref name="index"/>: layer by layer, stack by stack.</summary>
        public Vector3 SlotLocalPosition(int index) => SlotLocalPosition(index, _stacksX, _stacksZ, _stackPitch, _layerHeight, _deckHeight);

        /// <summary>Slot layout as a pure function (tests).</summary>
        public static Vector3 SlotLocalPosition(int index, int stacksX, int stacksZ, Vector2 pitch, float layerHeight, float deckHeight)
        {
            int perLayer = stacksX * stacksZ;
            int layer = index / perLayer;
            int inLayer = index % perLayer;
            int ix = inLayer % stacksX;
            int iz = inLayer / stacksX;
            float x = (ix - (stacksX - 1) * 0.5f) * pitch.x;
            float z = (iz - (stacksZ - 1) * 0.5f) * pitch.y;
            return new Vector3(x, deckHeight + layer * layerHeight, z);
        }

        /// <summary>Puts a carton on the next free slot (snaps, no animation). False if full.</summary>
        public bool TryAdd(ProductToken carton)
        {
            if (carton == null || IsFull)
            {
                return false;
            }

            if (carton.TryGetComponent(out HoldInteractable cartonHold))
            {
                cartonHold.SetInteractable(false); // stacked cartons stay on the pallet
            }

            if (carton.TryGetComponent(out Rigidbody cartonBody))
            {
                cartonBody.linearVelocity = Vector3.zero;
                cartonBody.angularVelocity = Vector3.zero;
                cartonBody.isKinematic = true;
                cartonBody.interpolation = RigidbodyInterpolation.None;
            }

            carton.transform.SetParent(transform, false);
            carton.transform.localPosition = SlotLocalPosition(_cartons.Count);
            carton.transform.localRotation = Quaternion.identity;
            _cartons.Add(carton);
            _loadGrams += ProductGrams(carton.Product) + _cartonGrams;
            return true;
        }

        private static float ProductGrams(ProductInstance product)
        {
            if (product == null)
            {
                return 0f;
            }

            float grams = product.MeasuredWeightGrams ?? 0f;
            if (product.Doses != null)
            {
                foreach (IngredientDose dose in product.Doses)
                {
                    grams += dose.Grams;
                }
            }
            return grams;
        }

        // ---- Transport state (pallet jack / palletizer) ----

        /// <summary>Called by the pallet jack when it lifts (true) or sets down (false) the pallet.</summary>
        public void SetCarried(bool carried)
        {
            IsCarried = carried;
            _parked = false;
            if (carried)
            {
                IsOnStation = false;
            }
            UpdateKinematic();
        }

        /// <summary>Called by the palletizer when the pallet is placed on / taken from its place.</summary>
        public void SetOnStation(bool onStation)
        {
            IsOnStation = onStation;
            _parked = false;
            UpdateKinematic();
        }

        private void UpdateKinematic()
        {
            if (IsHeldByHand)
            {
                return; // HoldInteractable controls the body while carried by hand
            }

            Body.isKinematic = IsCarried || IsOnStation || _parked;
            if (!Body.isKinematic)
            {
                Body.WakeUp();
            }
        }

        /// <summary>True while a pallet lies on this one (empty pallets in a stack).</summary>
        public bool HasPalletOnTop()
        {
            foreach (Pallet other in All)
            {
                if (other != this && IsAbove(other))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Pallets stacked on this one (recursive, bottom to top) - the jack lifts the whole stack.</summary>
        public void CollectStackAbove(List<Pallet> result)
        {
            foreach (Pallet other in All)
            {
                if (other != this && !result.Contains(other) && IsAbove(other))
                {
                    result.Add(other);
                    other.CollectStackAbove(result);
                }
            }
        }

        private bool IsAbove(Pallet other)
        {
            Vector3 offset = other.transform.position - transform.position;
            float horizontal = new Vector2(offset.x, offset.z).magnitude;
            return horizontal < 0.4f && offset.y > 0.05f && offset.y < _deckHeight * 1.6f;
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            values.Set("px", position.x);
            values.Set("py", position.y);
            values.Set("pz", position.z);
            values.Set("yaw", rotation.eulerAngles.y);
            values.Set("carried", IsCarried);
            values.Set("count", _cartons.Count);
            values.Set("loadGrams", _loadGrams);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            var position = new Vector3(values.GetFloat("px", transform.position.x),
                values.GetFloat("py", transform.position.y), values.GetFloat("pz", transform.position.z));
            if (values.GetBool("carried", false))
            {
                position.y = Mathf.Max(0.01f, position.y - PalletJack.LiftHeight); // the jack comes back lowered
            }

            _parked = values.GetInt("count", 0) == 0 && PalletStack.IsOnAnyStack(position); // kinematic only on a stack
            UpdateKinematic();
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, values.GetFloat("yaw", transform.eulerAngles.y), 0f));
            Body.position = position;
            Body.rotation = transform.rotation;

            int count = Mathf.Clamp(values.GetInt("count", 0), 0, Capacity);
            if (count > 0 && _cartonPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    ProductToken carton = Instantiate(_cartonPrefab);
                    carton.Product = new ProductInstance($"{name}-carton-{i + 1}", null, ProductState.PackagedPizza) { QualityPassed = true };
                    carton.name = $"{_cartonPrefab.name} ({carton.Product.InstanceId})";
                    TryAdd(carton);
                }
            }
            _loadGrams = values.GetFloat("loadGrams", _loadGrams);
        }

        public void PlaceToHoldPoint()
        {
            transform.SetParent(null, true);
            Transform holdPoint = _hold.HoldPoint.HoldPoint;
            transform.SetPositionAndRotation(_hold.HoldPosition(holdPoint), _hold.HoldRotation(holdPoint));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < Capacity; i += _stacksX * _stacksZ)
            {
                for (int s = 0; s < _stacksX * _stacksZ; s++)
                {
                    Vector3 p = SlotLocalPosition(i + s);
                    Gizmos.DrawWireCube(p + Vector3.up * _layerHeight * 0.5f, new Vector3(_stackPitch.x * 0.95f, _layerHeight * 0.95f, _stackPitch.y * 0.95f));
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
