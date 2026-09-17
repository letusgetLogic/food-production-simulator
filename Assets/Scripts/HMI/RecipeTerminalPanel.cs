using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>Single read-only "label: value" line, e.g. "Mehl: 500 g".</summary>
    public readonly struct RecipeEntryData
    {
        public readonly string Label;
        public readonly string Value;

        public RecipeEntryData(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    public class RecipeEntryRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private TextMeshProUGUI _valueText;

        public void Bind(RecipeEntryData entry)
        {
            if (_labelText != null)
            {
                _labelText.text = entry.Label;
            }

            if (_valueText != null)
            {
                _valueText.text = entry.Value;
            }
        }
    }

    /// <summary>
    /// Read-only panel shown at the recipe pillars: name of the currently active
    /// recipe plus a flat list of value rows. No selection/switching in the MVP -
    /// every pillar always shows the same active recipe. Deliberately generic
    /// (label/value pairs) instead of binding to RecipeDefinition's exact fields
    /// here, so this view stays data-free like the rest of the HMI; a binder maps
    /// the real recipe data onto <see cref="SetEntries"/>.
    /// </summary>
    public class RecipeTerminalPanel : HmiPanelBase
    {
        [SerializeField] private TextMeshProUGUI _recipeNameText;
        [SerializeField] private RecipeEntryRowView _entryPrefab;
        [SerializeField] private RectTransform _entryContainer;
        [SerializeField] private GameObject _emptyStateRoot;

        private readonly List<RecipeEntryRowView> _pool = new List<RecipeEntryRowView>();

        public void SetRecipeName(string recipeName)
        {
            if (_recipeNameText != null)
            {
                _recipeNameText.text = recipeName;
            }
        }

        public void SetEntries(IReadOnlyList<RecipeEntryData> entries)
        {
            int count = entries?.Count ?? 0;

            EnsurePoolSize(count);

            for (int i = 0; i < _pool.Count; i++)
            {
                bool active = i < count;
                _pool[i].gameObject.SetActive(active);

                if (active)
                {
                    _pool[i].Bind(entries[i]);
                }
            }

            if (_emptyStateRoot != null)
            {
                _emptyStateRoot.SetActive(count == 0);
            }
        }

        private void EnsurePoolSize(int required)
        {
            if (_entryPrefab == null || _entryContainer == null)
            {
                return;
            }

            while (_pool.Count < required)
            {
                _pool.Add(Instantiate(_entryPrefab, _entryContainer));
            }
        }
    }
}