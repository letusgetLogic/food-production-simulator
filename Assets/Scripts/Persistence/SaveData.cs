using System;
using System.Collections.Generic;
using Game.Production;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>
    /// Root of a save file (JSON via JsonUtility). Versioned: bump <see cref="CurrentVersion"/> whenever
    /// the format changes and add a step to <see cref="SaveMigrations.Migrate"/>.
    ///
    /// What is saved (MVP):
    ///  - every machine: state, fault reason, HMI setpoints (generic via IMachineParameterSource) and its
    ///    own runtime content (ISaveableState: tank levels, counters, simulated defect)
    ///  - every product on the line from PortionedDough on: pose, velocity and the full ProductInstance
    ///  - line statistics of FaultSystem and QualitySystem
    /// Not saved (yet): dough balls in mixer/pot/hopper, positions of the worker and of carried objects.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string SavedAtUtc;
        public string SceneName;
        public string Label;
        public float PlayTimeSeconds;

        public List<MachineSave> Machines = new List<MachineSave>();
        public List<ProductSave> Products = new List<ProductSave>();
        public SaveValues FaultStatistics = new SaveValues();
        public SaveValues QualityStatistics = new SaveValues();

        /// <summary>Other scene components with runtime state (ISaveableState, not machines) - e.g. the tutorial.</summary>
        public List<ComponentSave> Components = new List<ComponentSave>();
    }

    [Serializable]
    public class ComponentSave
    {
        /// <summary>Hierarchy path of the component's GameObject.</summary>
        public string Path;
        public SaveValues Content = new SaveValues();
    }

    [Serializable]
    public class MachineSave
    {
        /// <summary>Hierarchy path of the machine's GameObject (stable across sessions).</summary>
        public string Path;
        public MachineState State;
        public string FaultReason;
        public SaveValues Setpoints = new SaveValues();
        public SaveValues Content = new SaveValues();
    }

    /// <summary>Optional float that JsonUtility can serialize (it cannot handle float?).</summary>
    [Serializable]
    public struct OptionalFloat
    {
        public bool HasValue;
        public float Value;

        public static OptionalFloat From(float? value) =>
            new OptionalFloat { HasValue = value.HasValue, Value = value ?? 0f };

        public float? ToNullable() => HasValue ? Value : (float?)null;
    }

    [Serializable]
    public class ProductSave
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;

        public string InstanceId;
        public ProductState State;
        public string RecipeId;
        public string RejectReason;
        public OptionalFloat WeightGrams;
        public OptionalFloat TemperatureCelsius;
        public OptionalFloat BakeTimeSeconds;
        public OptionalFloat DiameterCm;
        public OptionalFloat ThicknessMm;
        public OptionalFloat SauceGrams;
        public OptionalFloat ToppingGrams;
        public OptionalFloat BakeTemperatureCelsius;
        public OptionalFloat CoolingTemperatureCelsius;
        public OptionalFloat FreezingTemperatureCelsius;

        public static ProductSave From(ProductInstance p) => new ProductSave
        {
            InstanceId = p.InstanceId,
            State = p.CurrentState,
            RecipeId = p.RecipeId,
            RejectReason = p.RejectReason,
            WeightGrams = OptionalFloat.From(p.MeasuredWeightGrams),
            TemperatureCelsius = OptionalFloat.From(p.MeasuredTemperatureCelsius),
            BakeTimeSeconds = OptionalFloat.From(p.ActualBakeTimeSeconds),
            DiameterCm = OptionalFloat.From(p.FormedDiameterCm),
            ThicknessMm = OptionalFloat.From(p.FormedThicknessMm),
            SauceGrams = OptionalFloat.From(p.DosedSauceGrams),
            ToppingGrams = OptionalFloat.From(p.DosedToppingGrams),
            BakeTemperatureCelsius = OptionalFloat.From(p.BakeTemperatureCelsius),
            CoolingTemperatureCelsius = OptionalFloat.From(p.CoolingTemperatureCelsius),
            FreezingTemperatureCelsius = OptionalFloat.From(p.FreezingTemperatureCelsius)
        };

        /// <summary>Creates the ProductInstance. The recipe asset is not bound in code (re-resolve via RecipeId later).</summary>
        public ProductInstance ToInstance() => new ProductInstance
        {
            InstanceId = InstanceId,
            CurrentState = State,
            RecipeId = RecipeId,
            RejectReason = RejectReason,
            MeasuredWeightGrams = WeightGrams.ToNullable(),
            MeasuredTemperatureCelsius = TemperatureCelsius.ToNullable(),
            ActualBakeTimeSeconds = BakeTimeSeconds.ToNullable(),
            FormedDiameterCm = DiameterCm.ToNullable(),
            FormedThicknessMm = ThicknessMm.ToNullable(),
            DosedSauceGrams = SauceGrams.ToNullable(),
            DosedToppingGrams = ToppingGrams.ToNullable(),
            BakeTemperatureCelsius = BakeTemperatureCelsius.ToNullable(),
            CoolingTemperatureCelsius = CoolingTemperatureCelsius.ToNullable(),
            FreezingTemperatureCelsius = FreezingTemperatureCelsius.ToNullable()
        };
    }

    /// <summary>Upgrades older save files step by step to <see cref="SaveData.CurrentVersion"/>.</summary>
    public static class SaveMigrations
    {
        /// <summary>Returns false if the file is newer than this build or cannot be migrated.</summary>
        public static bool Migrate(SaveData data, out string error)
        {
            error = null;
            if (data == null)
            {
                error = "Empty save file";
                return false;
            }

            if (data.Version > SaveData.CurrentVersion)
            {
                error = $"Save file version {data.Version} is newer than this build ({SaveData.CurrentVersion})";
                return false;
            }

            if (data.Version < 1)
            {
                error = $"Unknown save file version {data.Version}";
                return false;
            }

            // Add steps here when the format changes, e.g.:
            // if (data.Version == 1) { ...convert...; data.Version = 2; }

            data.Machines ??= new List<MachineSave>();
            data.Products ??= new List<ProductSave>();
            data.FaultStatistics ??= new SaveValues();
            data.QualityStatistics ??= new SaveValues();
            data.Components ??= new List<ComponentSave>(); // saves before 08.10. have none
            return true;
        }
    }
}
