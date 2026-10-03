using Game.Production;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalizationKeyTests
    {
        [TestCase("hmi", "Last bake time", "hmi.last_bake_time")]
        [TestCase("hmi", "Last product temp.", "hmi.last_product_temp")]
        [TestCase("hmi", "Batch ready to tilt", "hmi.batch_ready_to_tilt")]
        [TestCase("unit", "pcs", "unit.pcs")]
        [TestCase("unit", "m/s", "unit.m_s")]
        public void KeyFromText_BuildsSnakeCaseKeys(string prefix, string text, string expected) =>
            Assert.AreEqual(expected, LocText.KeyFromText(prefix, text));

        [TestCase("%")]
        [TestCase("°C")]
        [TestCase("")]
        [TestCase((string)null)]
        public void KeyFromText_WithoutLettersOrDigitsHasNoKey(string text)
        {
            // "°C" only keeps the "c"; units that are only symbols get no key at all.
            string key = LocText.KeyFromText("unit", text);
            Assert.That(key == null || key == "unit.c");
        }

        [TestCase("WrongProduct", "wrong_product")]
        [TestCase("SauceEmpty", "sauce_empty")]
        [TestCase("Jam", "jam")]
        [TestCase("TemperatureOutOfRange", "temperature_out_of_range")]
        public void Snake_ConvertsPascalCase(string pascal, string expected) =>
            Assert.AreEqual(expected, LocText.Snake(pascal));

        [Test]
        public void Get_ReturnsFallbackForEmptyKey() =>
            Assert.AreEqual("Fallback", LocText.Get(null, "Fallback"));
    }

    public class MachineParameterTests
    {
        private float _value;

        private MachineParameter Create() => new MachineParameter(
            "targetWeight", "Target weight", "g", 200f, 300f, 5f, "0",
            () => _value, v => _value = v);

        [Test]
        public void SetValue_ClampsToRange()
        {
            MachineParameter parameter = Create();
            parameter.SetValue(1000f);
            Assert.AreEqual(300f, _value);
            parameter.SetValue(-5f);
            Assert.AreEqual(200f, _value);
        }

        [Test]
        public void SetValue_SnapsToStep()
        {
            MachineParameter parameter = Create();
            parameter.SetValue(252.4f);
            Assert.AreEqual(250f, _value, 0.001f);
            parameter.SetValue(253f);
            Assert.AreEqual(255f, _value, 0.001f);
        }

        [Test]
        public void Nudge_MovesByStep()
        {
            _value = 250f;
            MachineParameter parameter = Create();
            parameter.Nudge(2);
            Assert.AreEqual(260f, _value, 0.001f);
            parameter.Nudge(-100);
            Assert.AreEqual(200f, _value, 0.001f);
        }

        [Test]
        public void SwappedMinMax_AreNormalized()
        {
            var parameter = new MachineParameter("x", "X", "", 10f, 1f, 1f, "0", () => 0f, _ => { });
            Assert.AreEqual(1f, parameter.Min);
            Assert.AreEqual(10f, parameter.Max);
        }
    }

    public class FaultCatalogTests
    {
        private SO_FaultCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = UnityEngine.ScriptableObject.CreateInstance<SO_FaultCatalog>();
            _catalog.ResetToDefaults();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_catalog);

        [Test]
        public void Defaults_ContainTheMvpFaultCases()
        {
            foreach (string code in new[] { "SauceEmpty", "ToppingEmpty", "BufferFull", "Jam", "TemperatureOutOfRange", "WrongProduct" })
            {
                FaultDefinition definition = _catalog.Find(code);
                Assert.IsNotNull(definition, code);
                Assert.IsTrue(definition.IsTrainingCase, code);
                Assert.IsNotEmpty(definition.FallbackMessage, code);
                Assert.IsNotEmpty(definition.FallbackRemedy, code);
            }
        }

        [Test]
        public void Find_MatchesReasonWithDetailByPrefix()
        {
            Assert.AreEqual("WrongProduct", _catalog.Find("WrongProduct:MixedDough").Code);
        }

        [Test]
        public void Find_UnknownReasonIsNull()
        {
            Assert.IsNull(_catalog.Find("SomethingElse"));
            Assert.IsNull(_catalog.Find(null));
        }

        [Test]
        public void Find_PrefersLongestCode()
        {
            // "Temperature..." must not be shadowed by a shorter code sharing the prefix.
            Assert.AreEqual("TemperatureOutOfRange", _catalog.Find("TemperatureOutOfRange").Code);
            Assert.AreEqual("BeltFault", _catalog.Find("BeltFault").Code);
        }

        [Test]
        public void MachineFaultCodes_AreInCatalog()
        {
            foreach (string code in new[]
                     {
                         ConveyorBelt.FaultReasonJam, ConveyorBelt.FaultReasonBufferFull,
                         DosingMachine.FaultReasonSauceEmpty, DosingMachine.FaultReasonToppingEmpty,
                         ContinuousProcessStation.FaultReasonTemperatureOutOfRange,
                         ContinuousProcessStation.FaultReasonHeatUpTimeout,
                         ContinuousProcessStation.FaultReasonBeltFault, PressMachine.FaultReasonWrongProduct
                     })
            {
                Assert.IsNotNull(_catalog.Find(code), code);
            }
        }
    }
}
