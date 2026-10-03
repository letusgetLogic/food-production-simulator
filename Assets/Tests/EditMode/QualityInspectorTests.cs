using System.Reflection;
using Game.Production;
using Game.Quality;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class QualityInspectorTests
    {
        private GameObject _go;
        private QualityInspector _inspector;
        private SO_QualitySpec _spec;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("QualityInspector");
            _inspector = _go.AddComponent<QualityInspector>();
            _spec = ScriptableObject.CreateInstance<SO_QualitySpec>();

            SetField(_inspector, "_spec", _spec);
            SetField(_inspector, "_logResults", false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_spec);
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        /// <summary>A packaged Margherita exactly on the spec defaults.</summary>
        private static ProductInstance GoodPizza() => new ProductInstance
        {
            InstanceId = "test",
            CurrentState = ProductState.PackagedPizza,
            MeasuredWeightGrams = 250f,
            FormedDiameterCm = 28f,
            FormedThicknessMm = 3f,
            DosedSauceGrams = 80f,
            DosedToppingGrams = 120f,
            ActualBakeTimeSeconds = 15f,
            BakeTemperatureCelsius = 280f,
            CoolingTemperatureCelsius = 20f,
            FreezingTemperatureCelsius = -18f
        };

        [Test]
        public void PizzaOnSpec_IsGood()
        {
            QualityResult result = _inspector.Evaluate(GoodPizza());
            Assert.IsTrue(result.IsGood, result.Summary);
            Assert.AreEqual(1, _inspector.GoodCount);
            Assert.AreEqual(0, _inspector.ScrapCount);
        }

        [Test]
        public void ValuesAtToleranceLimit_AreStillGood()
        {
            ProductInstance pizza = GoodPizza();
            pizza.MeasuredWeightGrams = 250f + 15f;
            pizza.ActualBakeTimeSeconds = 15f - 1.5f;
            Assert.IsTrue(_inspector.Evaluate(pizza).IsGood);
        }

        [Test]
        public void Underweight_IsScrap()
        {
            ProductInstance pizza = GoodPizza();
            pizza.MeasuredWeightGrams = 200f;

            QualityResult result = _inspector.Evaluate(pizza);
            Assert.IsFalse(result.IsGood);
            CollectionAssert.Contains(result.Defects, QualityInspector.DefectUnderweight);
            Assert.AreEqual(1, _inspector.DefectCounts[QualityInspector.DefectUnderweight]);
        }

        [Test]
        public void OverbakedAndColdChain_AreBothReported()
        {
            ProductInstance pizza = GoodPizza();
            pizza.ActualBakeTimeSeconds = 30f;
            pizza.FreezingTemperatureCelsius = -5f;

            QualityResult result = _inspector.Evaluate(pizza);
            CollectionAssert.Contains(result.Defects, QualityInspector.DefectOverbaked);
            CollectionAssert.Contains(result.Defects, QualityInspector.DefectColdChain);
        }

        [Test]
        public void RejectedByStation_IsScrapWithReason()
        {
            ProductInstance pizza = GoodPizza();
            pizza.Reject(PressMachine.RejectReasonPressInterrupted);

            QualityResult result = _inspector.Evaluate(pizza);
            CollectionAssert.Contains(result.Defects, QualityInspector.DefectRejected + ":" + PressMachine.RejectReasonPressInterrupted);
            Assert.AreEqual(1, _inspector.DefectCounts[QualityInspector.DefectRejected]);
        }

        [Test]
        public void Reject_KeepsFirstReason()
        {
            var pizza = new ProductInstance();
            pizza.Reject("A");
            pizza.Reject("B");
            Assert.AreEqual("A", pizza.RejectReason);
        }

        [Test]
        public void UnfinishedProduct_IsNotFinished()
        {
            ProductInstance pizza = GoodPizza();
            pizza.CurrentState = ProductState.ToppedPizza;

            QualityResult result = _inspector.Evaluate(pizza);
            CollectionAssert.Contains(result.Defects, QualityInspector.DefectNotFinished + ":ToppedPizza");
        }

        [Test]
        public void MissingMeasurement_DependsOnRequireAllMeasurements()
        {
            ProductInstance pizza = GoodPizza();
            pizza.DosedSauceGrams = null;

            Assert.IsFalse(_inspector.Evaluate(pizza).IsGood);

            _spec.RequireAllMeasurements = false;
            Assert.IsTrue(_inspector.Evaluate(pizza).IsGood);
        }

        [Test]
        public void DisabledCheck_IsSkipped()
        {
            ProductInstance pizza = GoodPizza();
            pizza.MeasuredWeightGrams = 100f;
            _spec.CheckWeight = false;
            Assert.IsTrue(_inspector.Evaluate(pizza).IsGood);
        }

        [Test]
        public void Statistics_RoundTripThroughSaveValues()
        {
            _inspector.Evaluate(GoodPizza());
            ProductInstance bad = GoodPizza();
            bad.MeasuredWeightGrams = 100f;
            _inspector.Evaluate(bad);

            var values = new SaveValues();
            _inspector.CaptureStatistics(values);

            var otherGo = new GameObject("Other");
            try
            {
                QualityInspector other = otherGo.AddComponent<QualityInspector>();
                other.RestoreStatistics(values);
                Assert.AreEqual(1, other.GoodCount);
                Assert.AreEqual(1, other.ScrapCount);
                Assert.AreEqual(1, other.DefectCounts[QualityInspector.DefectUnderweight]);
            }
            finally
            {
                Object.DestroyImmediate(otherGo);
            }
        }
    }
}
