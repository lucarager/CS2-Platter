// <copyright file="TestTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Linq;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Tools;
using Platter.Systems;
using Platter.Utils;
using Unity.Mathematics;
using ConnectedBuilding = Game.Buildings.ConnectedBuilding;
using Edge = Game.Net.Edge;

namespace Platter.Tests {
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Colossal.Assertions;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;
    using Game;
    using Game.Assets;
    using Game.City;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Simulation;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    [TestDescriptor("Platter: Debug Saves", Category.Default)]
    public class DebugSavesTest : TestScenario {
        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            log.Info("OnPrepare");
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [Test]
        private Task DebugSaves() {
            // Log Saves
            var saves = AssetDatabase.global.GetAssets<SaveGameMetadata>();
            log.InfoFormat("Available Saves:");
            foreach (var save in saves) {
                log.InfoFormat($"{save.ToJSONString()} + {save.GetMeta().displayName}");
            }

            return Task.CompletedTask;
        }

        [Test]
        private Task DebugMaps() {
            // Log Saves
            var maps = AssetDatabase.global.GetAssets<MapMetadata>();
            log.InfoFormat("Available Maps:");
            foreach (var map in maps) {
                log.InfoFormat($"{map.ToJSONString()} + {map.GetMeta().displayName}");
            }

            return Task.CompletedTask;
        }
    }
}