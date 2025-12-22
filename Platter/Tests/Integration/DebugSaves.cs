// <copyright file="DebugSaves.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests {
    #region Using Statements

    using System.Threading.Tasks;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.TestFramework;
    using Game.Assets;

    #endregion

    [TestDescriptor("Platter: Debug Saves", Category.Default)]
    public class DebugSavesTest : TestScenario {
        /// <inheritdoc/>
        protected override async Task OnPrepare() { log.Info("OnPrepare"); }

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