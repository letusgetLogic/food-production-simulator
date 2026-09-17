using System;
using System.Collections.Generic;
using UnityEngine;

namespace HMIAssetKit
{
    public enum HMIMachineState { Running, Warning, Fault, Idle }

    [Serializable]
    public sealed class HMIMachineData
    {
        public string id;
        public string displayName;
        public HMIMachineState state;
        public float temperature;
        public float fillLevel;
        public float cycleTime;
        public string setpoint;
        public string activeFault;
    }

    [Serializable]
    public sealed class HMIProductionData
    {
        public float throughput = 42f;
        public int units = 1284;
        public int scrap = 17;
        public int activeFaults = 1;
        public List<HMIMachineData> machines = new();
    }

    public sealed class HMIProductionModel : MonoBehaviour
    {
        public HMIProductionData Data = new();
        public event Action Changed;

        private void Reset()
        {
            Data.machines = new List<HMIMachineData>
            {
                new() { id = "mixer", displayName = "Teigmischer", state = HMIMachineState.Running },
                new() { id = "portioner", displayName = "Portionierer", state = HMIMachineState.Running },
                new() { id = "former", displayName = "Former", state = HMIMachineState.Fault, temperature = 61.2f, fillLevel = 68f, cycleTime = 4.8f, setpoint = "28cm/3mm", activeFault = "Formsensor außerhalb Toleranz" },
                new() { id = "doser", displayName = "Dosierstation", state = HMIMachineState.Idle }
            };
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
