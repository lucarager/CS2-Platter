// <copyright file="TestTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests {
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

    [TestDescriptor("Test Platter", Category.General, false, TestPhase.Default, false)]
    public class TestScenarioExample : TestScenario {
        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            TestScenario.log.Info("OnPrepare");

            // GameTestUtility.SetDefaultTestConditions();
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            TestScenario.log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestScenarioExample"/> class.
        /// </summary>
        public TestScenarioExample() {
        }
    }
}
