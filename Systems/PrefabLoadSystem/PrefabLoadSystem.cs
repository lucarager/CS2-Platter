// <copyright file="PrefabLoadSystem.cs" company="Luca Rager">
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
    using Unity.Entities;

    /// <summary>
    /// Todo.
    /// </summary>
    public partial class PrefabLoadSystem : UISystemBase {
        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PrefabLoadSystem));
            m_Log.Debug($"OnCreate()");
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            string logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

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
            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "EU Residential Mixed"), out PrefabBase zonePrefab)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefab not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("BuildingPrefab", "ParkingLot01"), out _)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. parkingLotPrefab not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("RoadPrefab", "Alley"), out PrefabBase roadPrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. roadPrefabBase not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("NetLaneGeometryPrefab", "EU Car Bay Line"), out PrefabBase netLanePrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. NetLaneGeometryPrefab not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("StaticObjectPrefab", "NA RoadArrow Forward"), out PrefabBase roadArrowFwdbBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. NetLaneGeometryPrefab not found");
            }

            if (!zonePrefab.TryGetExactly<UIObject>(out UIObject zonePrefabUIObject)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefabUIObject not found");
            }

            m_Log.Debug($"{logMethodPrefix} Successfully found all required prefabs and components.");

            // Cast prefabs
            RoadPrefab roadPrefab = (RoadPrefab)roadPrefabBase;
            NetLaneGeometryPrefab netLaneGeoPrefab = (NetLaneGeometryPrefab)netLanePrefabBase;
            StaticObjectPrefab roadArrowFwd = (StaticObjectPrefab)roadArrowFwdbBase;

            for (int i = BlockSizes.x; i <= BlockSizes.z; i++) {
                for (int j = BlockSizes.y; j <= BlockSizes.w; j++) {
                    if (!CreatePrefab(i, j, roadPrefab, netLaneGeoPrefab, zonePrefabUIObject, roadArrowFwd)) {
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
