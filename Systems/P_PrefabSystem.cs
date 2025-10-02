// <copyright file="P_PrefabSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Platter.Components;
    using Platter.Extensions;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Todo.
    /// </summary>
    public partial class P_PrefabSystem : GameSystemBase {
        /// <summary>
        /// Range of block sizes we support.
        /// <para>x = min width.</para>
        /// <para>y = min depth.</para>
        /// <para>z = max width.</para>
        /// <para>w = max width.</para>
        /// </summary>
        public static readonly int4 BlockSizes = new (2, 2, 6, 6);

        /// <summary>
        /// Todo.
        /// </summary>
        public static readonly string PrefabNamePrefix = "Parcel";

        /// <summary>
        /// Todo.
        /// </summary>
        private static bool _prefabsAreInstalled;

        /// <summary>
        /// Todo.
        /// </summary>
        private readonly Dictionary<string, PrefabID> m_SourcePrefabsDict = new () {
            { "zone", new PrefabID("ZonePrefab", "EU Residential Mixed") },
            { "road", new PrefabID("RoadPrefab", "Alley") },
            { "uiAssetCategory", new PrefabID("UIAssetCategoryPrefab", "ZonesOffice") },
            { "area", new PrefabID("SurfacePrefab", "Concrete Surface 01") },
            { "netLane", new PrefabID("NetLaneGeometryPrefab", "Gravel Pavement Transition") },
        };

        /// <summary>
        /// Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;
        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;

        // Logger
        private PrefixedLogger m_Log;

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
            m_Log = new PrefixedLogger(nameof(P_PrefabSystem));
            m_Log.Debug($"OnCreate()");

            // Storage
            m_PrefabBases = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();

            // Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);

            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

            if (_prefabsAreInstalled) {
                m_Log.Debug($"{logMethodPrefix} _prefabsAreInstalled = true, skipping");
                return;
            }

            var prefabBaseDict = new Dictionary<string, PrefabBase>();

            foreach (var (key, prefabId) in m_SourcePrefabsDict) {
                if (!m_PrefabSystem.TryGetPrefab(prefabId, out var prefabBase)) {
                    m_Log.Error($"{logMethodPrefix} Failed retrieving prefabBase {prefabId} and must exit.");
                    continue;
                }

                prefabBaseDict[key] = prefabBase;
            }

            if (!prefabBaseDict["zone"].TryGetExactly<UIObject>(out var zonePrefabUIObject)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving zonePrefabUIObject and must exit.");
            }

            m_Log.Debug($"{logMethodPrefix} Creating Area Prefab...");
            CreateParcelAreaPrefab((AreaPrefab)prefabBaseDict["area"], (NetLanePrefab)prefabBaseDict["netLane"], out var areaPrefab);

            m_Log.Debug($"{logMethodPrefix} Creating Category Prefab...");
            CreateCategoryPrefab((UIAssetCategoryPrefab)prefabBaseDict["uiAssetCategory"], out var uiCategoryPrefab);

            m_Log.Debug($"{logMethodPrefix} Creating Parcel Prefabs...");
            for (var i = BlockSizes.x; i <= BlockSizes.z; i++) {
                for (var j = BlockSizes.y; j <= BlockSizes.w; j++) {
                    if (!CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], zonePrefabUIObject, uiCategoryPrefab, areaPrefab)) {
                        m_Log.Error($"{logMethodPrefix} Failed adding ParcelPrefab {i}x{j} to PrefabSystem, exiting prematurely.");
                        return;
                    }
                }
            }

            // Mark the Install as already _prefabsAreInstalled
            _prefabsAreInstalled = true;

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }

        private bool CreateParcelPrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, UIObject zonePrefabUIObject, UIAssetCategoryPrefab uiCategoryPrefab, AreaPrefab areaPrefabBase) {
            var name = $"{PrefabNamePrefix} {lotWidth}x{lotDepth}";
            var icon = $"coui://platter/{PrefabNamePrefix}_{lotWidth}x{lotDepth}.svg";
            var parcelGeo = new ParcelGeometry(new int2(lotWidth, lotDepth));

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

            var corners = parcelGeo.CornerNodesRelativeToGeometryCenter;

            // (experimental) adding area data
            var objectSubAreas = ScriptableObject.CreateInstance<ObjectSubAreas>();
            objectSubAreas.name = $"{name}-areas";
            objectSubAreas.m_SubAreas = new ObjectSubAreaInfo[1] { new () {
                    m_AreaPrefab = areaPrefabBase,
                    m_NodePositions = new float3[] {
                        corners.c0,
                        corners.c1,
                        corners.c2,
                        corners.c3,
                    },
                    m_ParentMeshes = new int[4] {
                         -1,
                         -1,
                         0,
                         0,
                    },
                },
            };

            // parcelPrefabBase.AddComponentFrom(objectSubAreas);

            // Point and populate the new UIObject for our cloned Prefab
            var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = icon;
            placeableLotPrefabUIObject.name = PrefabNamePrefix;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Priority = ((lotWidth - 2) * BlockSizes.z) + lotDepth - 1;
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

        private bool CreateCategoryPrefab(UIAssetCategoryPrefab originalUICategoryPrefab, out UIAssetCategoryPrefab uiCategoryPrefab) {
            var name = $"PlatterCat";
            var icon = $"coui://platter/logo.svg";

            var uiCategoryPrefabBase = ScriptableObject.CreateInstance<UIAssetCategoryPrefab>();

            uiCategoryPrefabBase.name = name;
            uiCategoryPrefabBase.m_Menu = originalUICategoryPrefab.m_Menu;

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

        private bool CreateParcelAreaPrefab(AreaPrefab originalAreaPrefab, NetLanePrefab borderPrefab, out AreaPrefab areaPrefab) {
            var parecelAreaPrefab = (AreaPrefab)originalAreaPrefab.Clone("Parcel Enclosed Area");

            var enclosedArea = ScriptableObject.CreateInstance<EnclosedArea>();
            enclosedArea.name = "EnclosedArea";
            enclosedArea.m_BorderLaneType = borderPrefab;
            enclosedArea.m_CounterClockWise = false;

            parecelAreaPrefab.AddComponentFrom(enclosedArea);

            var success = m_PrefabSystem.AddPrefab(parecelAreaPrefab);

            if (success) {
                areaPrefab = parecelAreaPrefab;
                return true;
            }

            areaPrefab = null;
            return false;
        }
    }
}
