using System.Collections.Generic;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>Data shown for one active fault. Deliberately a plain struct –
    /// the real fault type from the FaultSystem (week 2) maps onto it later.</summary>
    public readonly struct HmiAlarmEntry
    {
        public readonly string SourceName;
        public readonly string Message;
        public readonly bool IsAcknowledged;

        public HmiAlarmEntry(string sourceName, string message, bool isAcknowledged = false)
        {
            SourceName = sourceName;
            Message = message;
            IsAcknowledged = isAcknowledged;
        }
    }

    /// <summary>
    /// Scrollable list of active faults. Instantiates entries from a prefab and
    /// pools them; shows an empty-state label while nothing is wrong.
    /// </summary>
    public class AlarmListView : MonoBehaviour
    {
        [SerializeField] private AlarmEntryView _entryPrefab;
        [SerializeField] private RectTransform _entryContainer;
        [SerializeField] private GameObject _emptyStateRoot;

        private readonly List<AlarmEntryView> _pool = new List<AlarmEntryView>();

        public void SetAlarms(IReadOnlyList<HmiAlarmEntry> alarms)
        {
            int count = alarms?.Count ?? 0;

            EnsurePoolSize(count);

            for (int i = 0; i < _pool.Count; i++)
            {
                bool active = i < count;
                _pool[i].gameObject.SetActive(active);

                if (active)
                {
                    _pool[i].Bind(alarms[i]);
                }
            }

            if (_emptyStateRoot != null)
            {
                _emptyStateRoot.SetActive(count == 0);
            }
        }

        public void Clear() => SetAlarms(null);

        private void EnsurePoolSize(int required)
        {
            if (_entryPrefab == null || _entryContainer == null)
            {
                return;
            }

            while (_pool.Count < required)
            {
                var prefab = Instantiate(_entryPrefab, _entryContainer);
                _pool.Add(prefab);
                prefab.transform.SetSiblingIndex(3); // below the header and status labels
            }
        }
    }
}
