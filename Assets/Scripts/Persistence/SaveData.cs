using System;
using System.Collections.Generic;
using System.Linq;
using Game.Production;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>
    /// Root of a save file (JSON via JsonUtility). Versioned: bump <see cref="CurrentVersion"/> whenever
    /// the format changes and add a step to <see cref="SaveMigrations.Migrate"/>.
    ///
    /// Version history:
    ///  1 - first format (product state stored as enum number).
    ///  2 - 11.10.: line order changed to real frozen pizza production (pre-bake -> cool -> dose -> freeze),
    ///      ProductState reordered. Product state is now stored by name, so later reorders keep saves valid.
    ///      Version 1 files are not migrated (their numbers mean other states) - delete them in the main menu.
    ///      Same day: dosed amounts as a list by ingredient id (modular dosing station) instead of sauce/topping fields.
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
        public const int CurrentVersion = 2;

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
        /// <summary>ProductState by name (not by number - robust against reordering the enum).</summary>
        public string State;
        public string RecipeId;
        public string RejectReason;
        public OptionalFloat WeightGrams;
        public OptionalFloat TemperatureCelsius;
        public OptionalFloat BakeTimeSeconds;
        public OptionalFloat DiameterCm;
        public OptionalFloat ThicknessMm;
        /// <summary>Dosed ingredients by id (generic - new ingredients need no format change).</summary>
        public List<IngredientDose> Doses = new List<IngredientDose>();
        public OptionalFloat BakeTemperatureCelsius;
        public OptionalFloat CoolingTemperatureCelsius;
        public OptionalFloat FreezingTemperatureCelsius;

        public static ProductSave From(ProductInstance p) => new ProductSave
        {
            InstanceId = p.InstanceId,
            State = p.CurrentState.ToString(),
            RecipeId = p.RecipeId,
            RejectReason = p.RejectReason,
            WeightGrams = OptionalFloat.From(p.MeasuredWeightGrams),
            TemperatureCelsius = OptionalFloat.From(p.MeasuredTemperatureCelsius),
            BakeTimeSeconds = OptionalFloat.From(p.ActualBakeTimeSeconds),
            DiameterCm = OptionalFloat.From(p.FormedDiameterCm),
            ThicknessMm = OptionalFloat.From(p.FormedThicknessMm),
            Doses = p.Doses != null
                ? p.Doses.Select(d => new IngredientDose { IngredientId = d.IngredientId, Grams = d.Grams }).ToList()
                : new List<IngredientDose>(),
            BakeTemperatureCelsius = OptionalFloat.From(p.BakeTemperatureCelsius),
            CoolingTemperatureCelsius = OptionalFloat.From(p.CoolingTemperatureCelsius),
            FreezingTemperatureCelsius = OptionalFloat.From(p.FreezingTemperatureCelsius)
        };

        /// <summary>Parses <see cref="State"/>; false for an unknown or empty name.</summary>
        public bool TryGetState(out ProductState state) =>
            Enum.TryParse(State, false, out state) && Enum.IsDefined(typeof(ProductState), state)
            && !int.TryParse(State, out _);

        /// <summary>
        /// Creates the ProductInstance. The recipe asset is not bound in code (re-resolve via RecipeId later).
        /// The state name is validated by <see cref="SaveMigrations.Migrate"/> before loading.
        /// </summary>
        public ProductInstance ToInstance() => new ProductInstance
        {
            InstanceId = InstanceId,
            CurrentState = TryGetState(out ProductState state) ? state : ProductState.PortionedDough,
            RecipeId = RecipeId,
            RejectReason = RejectReason,
            MeasuredWeightGrams = WeightGrams.ToNullable(),
            MeasuredTemperatureCelsius = TemperatureCelsius.ToNullable(),
            ActualBakeTimeSeconds = BakeTimeSeconds.ToNullable(),
            FormedDiameterCm = DiameterCm.ToNullable(),
            FormedThicknessMm = ThicknessMm.ToNullable(),
            Doses = Doses != null
                ? Doses.Select(d => new IngredientDose { IngredientId = d.IngredientId, Grams = d.Grams }).ToList()
                : new List<IngredientDose>(),
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

            if (data.Version < 2)
            {
                // Line order and ProductState numbers changed (11.10.) - old product states cannot be mapped reliably.
                error = $"Save file version {data.Version} is from the old line layout and can no longer be loaded";
                return false;
            }

            // Add steps here when the format changes, e.g.:
            // if (data.Version == 2) { ...convert...; data.Version = 3; }

            data.Machines ??= new List<MachineSave>();
            data.Products ??= new List<ProductSave>();
            data.FaultStatistics ??= new SaveValues();
            data.QualityStatistics ??= new SaveValues();
            data.Components ??= new List<ComponentSave>(); // saves before 08.10. have none

            foreach (ProductSave product in data.Products)
            {
                if (product != null && !product.TryGetState(out _))
                {
                    error = $"Unknown product state '{product.State}' in save file";
                    return false;
                }
            }
            return true;
        }
    }
}
