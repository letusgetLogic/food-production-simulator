using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>Button that opens another HMI page by its panel id (e.g. "statistics", "overview").</summary>
    [RequireComponent(typeof(Button))]
    public class HmiNavButton : MonoBehaviour
    {
        [SerializeField] private SO_HmiInteractChannel _interactChannel;
        [SerializeField] private string _targetPanelId = "statistics";

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Navigate);
        }

        public void Navigate()
        {
            if (_interactChannel != null)
            {
                _interactChannel.Request(_targetPanelId, null);
            }
        }
    }
}
