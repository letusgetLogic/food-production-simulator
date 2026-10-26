using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Core
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] private LocalizedString localizedString;
        private TextMeshProUGUI label;

        private void Awake() => label = GetComponent<TextMeshProUGUI>();

        private void OnEnable() => localizedString.StringChanged += OnStringChanged;
        private void OnDisable() => localizedString.StringChanged -= OnStringChanged;

        private void OnStringChanged(string value) => label.text = value;
        public void SetArguments(params object[] args)
        {
            localizedString.Arguments = args;
            localizedString.RefreshString();
        }
    }
}