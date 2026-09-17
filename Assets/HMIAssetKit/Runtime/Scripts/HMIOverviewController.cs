using UnityEngine;
using UnityEngine.UIElements;

namespace HMIAssetKit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HMIOverviewController : MonoBehaviour
    {
        [SerializeField] private HMIProductionModel model;
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
            var data = model != null ? model.Data : new HMIProductionData();
            SetText("throughput-value", $"{data.throughput:0.0} /min");
            SetText("units-value", data.units.ToString());
            SetText("scrap-value", data.scrap.ToString());
            SetText("faults-value", data.activeFaults.ToString());

            var list = root.Q<VisualElement>("machine-list");
            list?.Clear();
            if (list == null) return;
            foreach (var machine in data.machines)
            {
                var row = new Label($"●   {machine.displayName}                                      {machine.state}");
                row.AddToClassList("machine-row");
                row.AddToClassList(machine.state.ToString().ToLowerInvariant());
                list.Add(row);
            }

            var fault = root.Q<Label>("active-fault");
            if (fault != null)
            {
                var firstFault = data.machines.Find(m => m.state == HMIMachineState.Fault);
                fault.text = firstFault == null ? "Keine aktiven Fehler" : $"{firstFault.displayName} — {firstFault.activeFault}";
            }
        }

        private void SetText(string name, string value)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = value;
        }
    }
}
