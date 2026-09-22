using Game.Production;
using System;
using UnityEngine;

namespace Game.HMI
{
    [CreateAssetMenu(fileName = "HmiInteractChannel", menuName = "HMI/HMI Interact Channel")]
    public class SO_HmiInteractChannel : ScriptableObject
    {
        public event Action<string,MachineBase> DetailRequested;
        public void Request(string id, MachineBase machine) => DetailRequested?.Invoke(id, machine);
        public event Action<string> DetailRequestedFromMachineRow;
        public void Request(string machineId) => DetailRequestedFromMachineRow?.Invoke(machineId);
    }
}