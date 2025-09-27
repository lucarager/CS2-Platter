// <copyright file="PlatterPrefabSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.UI;
    using HarmonyLib;
    using Platter.Utils;
    using Unity.Entities;
    using UnityEngine;

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
            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "EU Residential Mixed"), out var zonePrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefabBase not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("RoadPrefab", "Alley"), out var roadPrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. roadPrefabBase not found");
            }

            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("UIAssetCategoryPrefab", "ZonesOffice"), out var uiAssetCategoryPrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. uiAssetCategoryPrefab not found");
            }

            if (!zonePrefabBase.TryGetExactly<UIObject>(out var zonePrefabUIObject)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefabUIObject not found");
            }

            m_Log.Debug($"{logMethodPrefix} Creating Category Prefab...");
            CreateCategoryPrefab((UIAssetCategoryPrefab)uiAssetCategoryPrefabBase, out var uiCategoryPrefab);

            m_Log.Debug($"{logMethodPrefix} Creating Parcel Prefabs...");
            for (var i = BlockSizes.x; i <= BlockSizes.z; i++) {
                for (var j = BlockSizes.y; j <= BlockSizes.w; j++) {
                    if (!CreateParcelPrefab(i, j, (RoadPrefab)roadPrefabBase, zonePrefabUIObject, uiCategoryPrefab)) {
                        m_Log.Error($"{logMethodPrefix} Failed adding ParcelPrefab {i}x{j} to PrefabSystem, exiting prematurely.");
                        return;
                    }
                }
            }

            // Mark the Install as already PrefabsAreInstalled
            PrefabsAreInstalled = true;

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }

        private bool CreateCategoryPrefab(UIAssetCategoryPrefab uiCategoryPrefabClone, out UIAssetCategoryPrefab uiCategoryPrefab) {
            var name = $"PlatterCat";
            var icon = $"coui://platter/logo.svg";

            var uiCategoryPrefabBase = ScriptableObject.CreateInstance<UIAssetCategoryPrefab>();

            uiCategoryPrefabBase.name = name;
            uiCategoryPrefabBase.m_Menu = uiCategoryPrefabClone.m_Menu;

            var uiObject = ScriptableObject.CreateInstance<UIObject>();
            uiObject.active = true;
            uiObject.m_Group = null;
            uiObject.m_Priority = 100;
            uiObject.m_Icon = icon;
            uiObject.m_IsDebugObject = false;
            uiObject.m_Icon = icon;
            uiObject.name = name;
            uiCategoryPrefabBase.AddComponentFrom(uiObject);

            var success = m_PrefabSystem.AddPrefab(uiCategoryPrefabBase);
            uiCategoryPrefab = uiCategoryPrefabBase;
            return success;
        }
    }
}
