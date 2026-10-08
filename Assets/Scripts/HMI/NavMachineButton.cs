using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    [RequireComponent(typeof(Button))]
    public class NavMachineButton : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        public Button Button => GetComponent<Button>();
        public void SetLabel(string text)
        {
            if (_label != null)
            {
                _label.text = text;
            }
        }
    }
}
