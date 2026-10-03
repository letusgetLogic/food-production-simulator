using Game.Persistence;
using Game.Production;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class SaveDataTests
    {
        [Test]
        public void SaveValues_SetOverwritesAndTypedGettersWork()
        {
            var values = new SaveValues();
            values.Set("level", 1.5f);
            values.Set("level", 2.5f);
            values.Set("count", 7);
            values.Set("flag", true);

            Assert.AreEqual(3, values.Keys.Count);
            Assert.AreEqual(2.5f, values.GetFloat("level", 0f));
            Assert.AreEqual(7, values.GetInt("count", 0));
            Assert.IsTrue(values.GetBool("flag", false));
            Assert.AreEqual(42, values.GetInt("missing", 42));
            Assert.IsFalse(values.TryGet("missing", out _));
        }

        [Test]
        public void ProductSave_KeepsNullableValues()
        {
            var product = new ProductInstance("p1", null, ProductState.BakedPizza)
            {
                MeasuredWeightGrams = 251f,
                DosedSauceGrams = null,
                ActualBakeTimeSeconds = 15.2f,
                BakeTemperatureCelsius = 279f
            };
            product.Reject("PressInterrupted");

            ProductInstance restored = ProductSave.From(product).ToInstance();

            Assert.AreEqual("p1", restored.InstanceId);
            Assert.AreEqual(ProductState.BakedPizza, restored.CurrentState);
            Assert.AreEqual(251f, restored.MeasuredWeightGrams);
            Assert.IsNull(restored.DosedSauceGrams);
            Assert.AreEqual(15.2f, restored.ActualBakeTimeSeconds);
            Assert.AreEqual(279f, restored.BakeTemperatureCelsius);
            Assert.IsNull(restored.FreezingTemperatureCelsius);
            Assert.AreEqual("PressInterrupted", restored.RejectReason);
        }

        [Test]
        public void SaveData_SurvivesJsonRoundTrip()
        {
            var data = new SaveData { SceneName = "Factory_Prototyp", Label = "test" };

            var machine = new MachineSave { Path = "Oven", State = MachineState.Fault, FaultReason = "TemperatureOutOfRange" };
            machine.Setpoints.Set("dwellTime", 15f);
            machine.Content.Set("completed", 12);
            data.Machines.Add(machine);

            ProductSave product = ProductSave.From(new ProductInstance("p1", null, ProductState.FormedPizza) { FormedDiameterCm = 28f });
            product.Position = new Vector3(1f, 2f, 3f);
            data.Products.Add(product);
            data.QualityStatistics.Set("good", 5);

            string json = JsonUtility.ToJson(data);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(SaveData.CurrentVersion, loaded.Version);
            Assert.AreEqual("Factory_Prototyp", loaded.SceneName);
            Assert.AreEqual(1, loaded.Machines.Count);
            Assert.AreEqual(MachineState.Fault, loaded.Machines[0].State);
            Assert.AreEqual("TemperatureOutOfRange", loaded.Machines[0].FaultReason);
            Assert.AreEqual(15f, loaded.Machines[0].Setpoints.GetFloat("dwellTime", 0f));
            Assert.AreEqual(12, loaded.Machines[0].Content.GetInt("completed", 0));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), loaded.Products[0].Position);
            Assert.AreEqual(28f, loaded.Products[0].ToInstance().FormedDiameterCm);
            Assert.AreEqual(5, loaded.QualityStatistics.GetInt("good", 0));
        }

        [Test]
        public void Migration_AcceptsCurrentVersion()
        {
            var data = new SaveData();
            Assert.IsTrue(SaveMigrations.Migrate(data, out string error), error);
        }

        [Test]
        public void Migration_RejectsNewerAndInvalidVersions()
        {
            Assert.IsFalse(SaveMigrations.Migrate(new SaveData { Version = SaveData.CurrentVersion + 1 }, out _));
            Assert.IsFalse(SaveMigrations.Migrate(new SaveData { Version = 0 }, out _));
            Assert.IsFalse(SaveMigrations.Migrate(null, out _));
        }

        [Test]
        public void Migration_FillsMissingLists()
        {
            SaveData data = JsonUtility.FromJson<SaveData>("{\"Version\":1}");
            Assert.IsTrue(SaveMigrations.Migrate(data, out _));
            Assert.IsNotNull(data.Machines);
            Assert.IsNotNull(data.Products);
            Assert.IsNotNull(data.FaultStatistics);
        }
    }
}
