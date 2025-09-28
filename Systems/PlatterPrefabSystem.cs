// <copyright file="PlatterPrefabSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.SceneFlow;
    using Game.Simulation;
    using Game.UI;
    using HarmonyLib;
    using Platter.Components;
    using Platter.Extensions;
    using Platter.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Todo.
    /// </summary>
    public partial class PlatterPrefabSystem : UISystemBase {
        /// <summary>
        /// Range of block sizes we support.
        /// <para>x = min width.</para>
        /// <para>y = min depth.</para>
        /// <para>z = max width.</para>
        /// <para>w = max width.</para>
        /// </summary>
        public static readonly int4 BlockSizes = new(2, 2, 6, 6);

        /// <summary>
        /// Todo.
        /// </summary>
        public static readonly string PrefabNamePrefix = "Parcel";

        /// <summary>
        /// Todo.
        /// </summary>
        public static readonly Dictionary<string, string[]> SourcePrefabNames = new() {
            { "subLanePrefab", new string[2] { "NetLaneGeometryPrefab", "EU Car Bay Line" } },
            { "roadPrefab", new string[2] { "RoadPrefab", "Alley" } },
            { "uiPrefab", new string[2] { "ZonePrefab", "EU Residential Mixed" } },
        };

        /// <summary>
        /// Todo.
        /// </summary>
        private static bool PrefabsAreInstalled;

        /// <summary>
        /// Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;
        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;
        private static BuildingInitializeSystem m_BuildingInitializeSystem;

        // Class State
        private readonly bool m_Executed = false;

        // Barriers & Buffers
        private readonly EndFrameBarrier m_Barrier;
        private readonly EndFrameBarrier m_EndFrameBarrier;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityCommandBuffer m_BlockCommandBuffer;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_UpdatedEdgesQuery;

        // Entities
        private Entity m_CachedBuildingEntity;
        private Entity m_CachedEdgeEntity;
        private EntityArchetype m_DefinitionArchetype;

        /// <summary>
        /// Data to define our prefabs.
        /// </summary>
        private struct CustomPrefabData {
            public int m_LotWidth;
            public int m_LotDepth;

            public CustomPrefabData(int w, int d) {
                m_LotWidth = w;
                m_LotDepth = d;
            }
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PlatterPrefabSystem));
            m_Log.Debug($"OnCreate()");

            // Storage
            m_PrefabBases = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();

            // Systems
            m_BuildingInitializeSystem = World.GetOrCreateSystemManaged<BuildingInitializeSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Override base system
            var originalQuery = (EntityQuery)m_BuildingInitializeSystem.GetMemberValue("m_PrefabQuery");
            var originalQueryDescs = originalQuery.GetEntityQueryDescs();
            var componentType = ComponentType.ReadOnly<Parcel>();
            var getQueryMethod = typeof(ComponentSystemBase).GetMethod(
                "GetEntityQuery",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                CallingConventions.Any,
                new Type[] { typeof(EntityQueryDesc[]) },
                Array.Empty<ParameterModifier>()
            );

            foreach (var originalQueryDesc in originalQueryDescs) {
                if (originalQueryDesc.None.Contains(componentType)) {
                    continue;
                }

                // add Parcel to force vanilla skip all entities with the Parcel component
                originalQueryDesc.None = originalQueryDesc.None.Append(componentType).ToArray();


                // generate EntityQuery
                var modifiedQuery = (EntityQuery)getQueryMethod.Invoke(m_BuildingInitializeSystem, new object[] { new EntityQueryDesc[] { originalQueryDesc } });

                // add modified EntityQuery to update check
                m_BuildingInitializeSystem.RequireForUpdate(modifiedQuery);
            }
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

            if (PrefabsAreInstalled) {
                m_Log.Debug($"{logMethodPrefix} PrefabsAreInstalled = true, skipping");
                return;
            }

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

        private bool CreateParcelPrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, UIObject zonePrefabUIObject, UIAssetCategoryPrefab uiCategoryPrefab) {
            var name = $"{PrefabNamePrefix} {lotWidth}x{lotDepth}";
            var icon = $"coui://platter/{PrefabNamePrefix}_{lotWidth}x{lotDepth}.svg";

            // Point our new prefab
            var parcelPrefabBase = ScriptableObject.CreateInstance<ParcelPrefab>();
            parcelPrefabBase.name = name;
            parcelPrefabBase.m_LotWidth = lotWidth;
            parcelPrefabBase.m_LotDepth = lotDepth;

            // Adding PlaceableObject Data.
            var placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            parcelPrefabBase.AddComponentFrom(placeableObject);

            // Adding ZoneBlock data.
            parcelPrefabBase.m_ZoneBlock = roadPrefab.m_ZoneBlock;

            // Point and populate the new UIObject for our cloned Prefab
            var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = icon;
            placeableLotPrefabUIObject.name = PrefabNamePrefix;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Group = uiCategoryPrefab;
            placeableLotPrefabUIObject.active = zonePrefabUIObject.active;
            parcelPrefabBase.AddComponentFrom(placeableLotPrefabUIObject);

            m_Log.Debug($"Created Parcel SelectedPrefabBase with uiTag {parcelPrefabBase.uiTag}");

            // Try to add it to the prefab System
            var success = m_PrefabSystem.AddPrefab(parcelPrefabBase);

            if (success) {
                // Todo can we set data here instead of the system?
                var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
                m_PrefabBases.Add(parcelPrefabBase);
                m_PrefabEntities.Add(parcelPrefabBase, prefabEntity);
            }

            return success;
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
