// <copyright file="PlatterPrefabSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.UI;
    using HarmonyLib;
    using Platter.Utils;
    using System.Collections.Generic;
    using Unity.Entities;

    /// <summary>
    /// Todo.
    /// </summary>
    public partial class PlatterPrefabSystem : UISystemBase {
        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PlatterPrefabSystem));
            m_Log.Debug($"OnCreate()");

            // Storage
            m_PrefabBases = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

            if (PrefabsAreInstalled) {
                m_Log.Debug($"{logMethodPrefix} PrefabsAreInstalled = true, skipping");
                return;
            }

            // Getting World instance
            m_World = Traverse.Create(GameManager.instance).Field<World>("m_World").Value;
            if (m_World == null) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving World instance, exiting.");
                return;
            }

            // Getting PrefabSystem instance from World
            m_PrefabSystem = m_World.GetOrCreateSystemManaged<PrefabSystem>();
            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "EU Residential Mixed"), out var zonePrefab)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefab not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("RoadPrefab", "Alley"), out var roadPrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. roadPrefabBase not found");
            }

            if (!zonePrefab.TryGetExactly<UIObject>(out var zonePrefabUIObject)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefabUIObject not found");
            }

            // Cast prefabs
            var roadPrefab = (RoadPrefab)roadPrefabBase;

            for (var i = BlockSizes.x; i <= BlockSizes.z; i++) {
                for (var j = BlockSizes.y; j <= BlockSizes.w; j++) {
                    if (!CreatePrefab(i, j, roadPrefab, zonePrefabUIObject)) {
                        m_Log.Error($"{logMethodPrefix} Failed adding ParcelPrefab {i}x{j} to PrefabSystem, exiting prematurely.");
                        return;
                    }
                }
            }

            // Mark the Install as already PrefabsAreInstalled
            PrefabsAreInstalled = true;

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }
    }
}
