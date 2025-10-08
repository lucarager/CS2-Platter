using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Colossal;
using Colossal.Assertions;
using Colossal.AssetPipeline;
using Colossal.Json;
using Colossal.Logging;
using Colossal.TestFramework;
using Unity.Mathematics;
using UnityEngine;

namespace Platter.Tests {
    [TestDescriptor("Test Platter", Category.General, false, TestPhase.Default, false)]
    public class TestScenarioExample : TestScenario {
        protected override async Task OnPrepare() {
            TestScenario.log.Info("OnPrepare");
            //GameTestUtility.SetDefaultTestConditions();
            await Task.CompletedTask;
        }

        protected override async Task OnCleanup() {
            TestScenario.log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [TestPrepare]
        private void TestPrepare() {
            TestScenario.log.Info("TestPrepare");
        }

        [TestCleanup]
        private void TestCleanup() {
            TestScenario.log.Info("TestPrepare");
        }

        [Test]
        private void TestExampleVoid() {
            TestScenario.log.Info("TestExampleVoid");
        }

        [Test]
        private async Task TestExampleAsync() {
            TestScenario.log.Info("TestExampleAsync");
            await Task.CompletedTask;
        }

        public TestScenarioExample() {
        }
    }
}
