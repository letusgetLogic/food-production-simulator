using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Production;
using Game.Quality;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Persistence
{
    /// <summary>
    /// Save/Load of the running line (Woche 3). One instance per scene (on "LineSystems").
    ///
    /// Saving: collects machines, products and statistics into a <see cref="SaveData"/> and writes it as
    /// JSON to a slot. Loading: reads and migrates the file, reloads the scene (clean state, all Awake/Start
    /// run normally) and applies the data a few frames after the scene started - after the line controller
    /// has started the machines.
    ///
    /// Apply order: setpoints and machine content -> products -> faults/stops -> statistics.
    /// Public API is called by the debug panel / pause menu: <see cref="Save"/>, <see cref="Load"/>,
    /// <see cref="HasSave"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveLoadController : MonoBehaviour
    {
        public const string DefaultSlot = "slot1";

        [Tooltip("Prefab spawned for saved products (the portioner's pizza prefab). Products are not saved without it.")]
        [SerializeField] private ProductToken _productPrefab;

        [SerializeField] private FaultMonitor _faultMonitor;
        [SerializeField] private QualityInspector _qualityInspector;

        [Tooltip("Frames to wait after scene start before applying a loaded save (machines/line controller start first).")]
        [Min(1)] [SerializeField] private int _applyDelayFrames = 3;

        [Tooltip("Write the JSON indented (readable, larger).")]
        [SerializeField] private bool _prettyPrint = true;

        /// <summary>Data waiting to be applied after the scene reload (survives the reload as a static).</summary>
        private static SaveData _pendingLoad;

        private ISaveStorage _storage;

        /// <summary>Raised after saving: (slot, success, message).</summary>
        public event Action<string, bool, string> Saved;

        /// <summary>Raised after a loaded save was applied: (slot, success, message).</summary>
        public event Action<string, bool, string> Loaded;

        private static string _pendingSlot;

        /// <summary>Prefab used to re-spawn products (also used by the debug panel to spawn test products).</summary>
        public ProductToken ProductPrefab => _productPrefab;

        public ISaveStorage Storage => _storage ??= SaveStorageFactory.CreateDefault();

        public bool HasSave(string slot = DefaultSlot) => Storage.Exists(slot);

        private void Awake()
        {
            if (_faultMonitor == null)
            {
                _faultMonitor = FindFirstObjectByType<FaultMonitor>();
            }

            if (_qualityInspector == null)
            {
                _qualityInspector = FindFirstObjectByType<QualityInspector>();
            }
        }

        private void Start()
        {
            if (_pendingLoad != null)
            {
                StartCoroutine(ApplyPendingLoad());
            }
        }

        // ---- Save ----

        public bool Save(string slot = DefaultSlot, string label = null)
        {
            try
            {
                SaveData data = Capture(label);
                string json = JsonUtility.ToJson(data, _prettyPrint);
                Storage.Write(slot, json);
                string message = $"Saved {data.Machines.Count} machines, {data.Products.Count} products to {Storage.Describe(slot)}";
                Debug.Log("[SaveLoad] " + message, this);
                Saved?.Invoke(slot, true, message);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Saved?.Invoke(slot, false, exception.Message);
                return false;
            }
        }

        public SaveData Capture(string label = null)
        {
            var data = new SaveData
            {
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                SceneName = SceneManager.GetActiveScene().name,
                Label = label,
                PlayTimeSeconds = Time.timeSinceLevelLoad
            };

            foreach (KeyValuePair<string, MachineBase> entry in FindMachinesByPath())
            {
                MachineBase machine = entry.Value;
                var save = new MachineSave
                {
                    Path = entry.Key,
                    State = machine.CurrentState,
                    FaultReason = machine.FaultReason
                };

                if (machine is IMachineParameterSource source && source.Parameters != null)
                {
                    foreach (MachineParameter parameter in source.Parameters)
                    {
                        save.Setpoints.Set(parameter.Id, parameter.Value);
                    }
                }

                if (machine is ISaveableState saveable)
                {
                    saveable.CaptureState(save.Content);
                }

                data.Machines.Add(save);
            }

            foreach (ProductToken token in FindObjectsByType<ProductToken>(FindObjectsSortMode.None))
            {
                ProductInstance product = token.Product;
                if (product == null || product.CurrentState < ProductState.PortionedDough)
                {
                    continue; // dough in mixer/pot/hopper is not saved (MVP)
                }

                ProductSave save = ProductSave.From(product);
                save.Position = token.transform.position;
                save.Rotation = token.transform.rotation;
                if (token.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                {
                    save.Velocity = body.linearVelocity;
                }
                data.Products.Add(save);
            }

            if (_faultMonitor != null)
            {
                _faultMonitor.CaptureStatistics(data.FaultStatistics);
            }

            if (_qualityInspector != null)
            {
                _qualityInspector.CaptureStatistics(data.QualityStatistics);
            }
            return data;
        }

        // ---- Load ----

        /// <summary>Reads, migrates and schedules a save; reloads the scene. False if there is nothing valid to load.</summary>
        public bool Load(string slot = DefaultSlot)
        {
            string json;
            try
            {
                json = Storage.Read(slot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Loaded?.Invoke(slot, false, exception.Message);
                return false;
            }

            if (string.IsNullOrEmpty(json))
            {
                Loaded?.Invoke(slot, false, "No save in slot " + slot);
                return false;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Loaded?.Invoke(slot, false, "Save file is corrupt");
                return false;
            }

            if (!SaveMigrations.Migrate(data, out string error))
            {
                Debug.LogWarning("[SaveLoad] " + error, this);
                Loaded?.Invoke(slot, false, error);
                return false;
            }

            _pendingLoad = data;
            _pendingSlot = slot;

            string sceneName = string.IsNullOrEmpty(data.SceneName) ? SceneManager.GetActiveScene().name : data.SceneName;
            SceneManager.LoadScene(sceneName);
            return true;
        }

        private IEnumerator ApplyPendingLoad()
        {
            for (int i = 0; i < _applyDelayFrames; i++)
            {
                yield return null;
            }

            SaveData data = _pendingLoad;
            string slot = _pendingSlot;
            _pendingLoad = null;
            _pendingSlot = null;

            string report;
            try
            {
                report = Apply(data, _pendingStops);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Loaded?.Invoke(slot, false, exception.Message);
                yield break;
            }

            // Operator stops: the line controller starts the line element by element (0.5 s apart) and
            // StopRun only works while Running. So wait until no start sequence runs any more and the
            // machine has finished Starting - otherwise the sequence would start it again afterwards.
            float waited = 0f;
            ConveyorLineController[] lines = FindObjectsByType<ConveyorLineController>(FindObjectsSortMode.None);
            while (_pendingStops.Count > 0 && waited < StopWaitTimeoutSeconds)
            {
                bool sequenceRunning = false;
                foreach (ConveyorLineController line in lines)
                {
                    sequenceRunning |= line != null && line.IsSequenceRunning;
                }

                for (int i = _pendingStops.Count - 1; i >= 0 && !sequenceRunning; i--)
                {
                    MachineBase machine = _pendingStops[i];
                    // Sequence finished: Running -> stop it; still Ready -> it is not auto-started, nothing to do.
                    if (machine == null || machine.CurrentState != MachineState.Starting)
                    {
                        if (machine != null)
                        {
                            machine.StopRun();
                        }
                        _pendingStops.RemoveAt(i);
                    }
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            _pendingStops.Clear();

            Debug.Log("[SaveLoad] Loaded " + slot + ": " + report, this);
            Loaded?.Invoke(slot, true, report);
        }

        private const float StopWaitTimeoutSeconds = 30f;
        private readonly List<MachineBase> _pendingStops = new List<MachineBase>();

        /// <summary>
        /// Applies a save onto the freshly started scene. Machines the operator had stopped are added to
        /// <paramref name="stopsToApply"/> (the caller stops them once they finished starting). Returns a short report.
        /// </summary>
        public string Apply(SaveData data, List<MachineBase> stopsToApply)
        {
            Dictionary<string, MachineBase> machines = FindMachinesByPath();
            int machineCount = 0;
            var missing = new List<string>();

            // 1. Setpoints and content
            foreach (MachineSave save in data.Machines)
            {
                if (!machines.TryGetValue(save.Path, out MachineBase machine))
                {
                    missing.Add(save.Path);
                    continue;
                }

                if (machine is IMachineParameterSource source && source.Parameters != null)
                {
                    foreach (MachineParameter parameter in source.Parameters)
                    {
                        if (save.Setpoints.TryGet(parameter.Id, out float value))
                        {
                            parameter.SetValue(value);
                        }
                    }
                }

                if (machine is ISaveableState saveable)
                {
                    saveable.RestoreState(save.Content);
                }

                machineCount++;
            }

            // 2. Products
            int productCount = RestoreProducts(data.Products);

            // 3. Faults and operator stops
            foreach (MachineSave save in data.Machines)
            {
                if (!machines.TryGetValue(save.Path, out MachineBase machine))
                {
                    continue;
                }

                switch (save.State)
                {
                    case MachineState.Fault:
                        machine.TriggerFault(string.IsNullOrEmpty(save.FaultReason) ? "Restored" : save.FaultReason);
                        break;
                    case MachineState.Maintenance:
                        machine.TriggerFault(string.IsNullOrEmpty(save.FaultReason) ? "Restored" : save.FaultReason);
                        machine.AcknowledgeFault();
                        break;
                    case MachineState.Stopping:
                    case MachineState.Stopped:
                        stopsToApply?.Add(machine);
                        break;
                }
            }

            // 4. Statistics last (re-raised faults above must not be counted twice)
            if (_faultMonitor != null)
            {
                _faultMonitor.RestoreStatistics(data.FaultStatistics);
            }

            if (_qualityInspector != null)
            {
                _qualityInspector.RestoreStatistics(data.QualityStatistics);
            }

            var report = new StringBuilder($"{machineCount} machines, {productCount} products");
            if (missing.Count > 0)
            {
                report.Append($", {missing.Count} machines not found in scene: ").Append(string.Join(", ", missing));
            }
            return report.ToString();
        }

        private int RestoreProducts(List<ProductSave> products)
        {
            if (products.Count == 0)
            {
                return 0;
            }

            if (_productPrefab == null)
            {
                Debug.LogWarning("[SaveLoad] No product prefab assigned - saved products are skipped.", this);
                return 0;
            }

            int count = 0;
            foreach (ProductSave save in products)
            {
                ProductToken token = Instantiate(_productPrefab, save.Position, save.Rotation);
                token.Product = save.ToInstance();
                token.name = $"{_productPrefab.name} ({save.InstanceId})";

                if (token.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                {
                    body.linearVelocity = save.Velocity;
                }
                count++;
            }
            return count;
        }

        // ---- Machine identity ----

        /// <summary>All active machines by hierarchy path; duplicates get "#2", "#3" in scene order.</summary>
        private static Dictionary<string, MachineBase> FindMachinesByPath()
        {
            var result = new Dictionary<string, MachineBase>();
            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);
            Array.Sort(machines, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));

            foreach (MachineBase machine in machines)
            {
                if (!machine.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string path = PathOf(machine.transform);
                string key = path;
                for (int n = 2; result.ContainsKey(key); n++)
                {
                    key = path + "#" + n;
                }
                result.Add(key, machine);
            }
            return result;
        }

        private static string PathOf(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                builder.Insert(0, parent.name + "/");
            }
            return builder.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pendingLoad = null;
            _pendingSlot = null;
        }
    }
}
