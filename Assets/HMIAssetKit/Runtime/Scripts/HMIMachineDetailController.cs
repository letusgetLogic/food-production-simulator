using UnityEngine;
using UnityEngine.UIElements;

namespace HMIAssetKit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HMIMachineDetailController : MonoBehaviour
    {
        [SerializeField] private HMIProductionModel model;
        [SerializeField] private string machineId = "former";
        private VisualElement root;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (model != null) model.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (model != null) model.Changed -= Refresh;
        }

        public void Refresh()
        {
            if (root == null) return;
            var machine = model?.Data.machines.Find(m => m.id == machineId);
            if (machine == null) return;
            SetText("machine-name", machine.displayName);
            SetText("machine-state", machine.state.ToString());
            SetText("temperature-value", $"{machine.temperature:0.0} °C");
            SetText("fill-value", $"{machine.fillLevel:0} %");
            SetText("cycle-value", $"{machine.cycleTime:0.0} s");
            SetText("setpoint-value", machine.setpoint);
            SetText("detail-fault", machine.activeFault);
        }

        private void SetText(string name, string value)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = value ?? "—";
        }
    }
}
