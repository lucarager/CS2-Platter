// <copyright file="P_PrefabsCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// System responsible for generating our parcel prefabs on game load.
    /// </summary>
    public partial class P_PrefabsCreateSystem : GameSystemBase {
        /// <summary>
        /// Range of block sizes we support.
        /// <para>x = min width.</para>
        /// <para>y = min depth.</para>
        /// <para>z = max width.</para>
        /// <para>w = max width.</para>
        /// </summary>
        public static readonly int4 BlockSizes = new(2, 2, 6, 6);

        /// <summary>
        /// Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        /// <summary>
        /// Configuration for vanilla prefabas to load for further processing.
        /// </summary>
        private readonly Dictionary<string, PrefabID> m_SourcePrefabsDict = new() {
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
        private static BuildingInitializeSystem m_BuildingInitializeSystem;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_PrefabsCreateSystem));
            m_Log.Debug($"OnCreate()");

            // Storage
            m_PrefabBases = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();

            // Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_BuildingInitializeSystem = World.GetOrCreateSystemManaged<BuildingInitializeSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";
            m_Log.Debug($"{logMethodPrefix}");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        private void Install() {
            var logMethodPrefix = $"Install() --";
            
            // Mark the Install as already _prefabsAreInstalled
            m_PrefabsAreInstalled = true;

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
                    if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], zonePrefabUIObject, uiCategoryPrefab, areaPrefab)) {
                        m_Log.Debug($"Created Parcel Prefab {i}x{j}");
                    } else {
                        m_Log.Error($"{logMethodPrefix} Failed adding Parcel Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
                    }
                    if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], zonePrefabUIObject, uiCategoryPrefab, areaPrefab, true)) {
                        m_Log.Debug($"Created ParcelPlaceholder Prefab {i}x{j}");
                    } else {
                        m_Log.Error($"{logMethodPrefix} Failed adding ParcelPlaceholder Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
                    }
                }
            }

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }


        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose  purpose,
                                                      GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        private bool CreateParcelPrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, UIObject zonePrefabUIObject, UIAssetCategoryPrefab uiCategoryPrefab, AreaPrefab areaPrefabBase, bool placeholder = false) {
            var prefix     = "Parcel";
            var name       = $"{prefix} {lotWidth}x{lotDepth}";
            var icon       = $"coui://platter/{prefix}_{lotWidth}x{lotDepth}.svg";
            var parcelGeo  = new ParcelGeometry(new int2(lotWidth, lotDepth));

            PrefabBase prefabBase;
            if (placeholder) {
                var parcelPrefabBase = ScriptableObject.CreateInstance<ParcelPlaceholderPrefab>();
                parcelPrefabBase.name        = name;
                parcelPrefabBase.m_LotWidth  = lotWidth;
                parcelPrefabBase.m_LotDepth  = lotDepth;
                parcelPrefabBase.m_ZoneBlock = roadPrefab.m_ZoneBlock;
                prefabBase                   = parcelPrefabBase;
            } else {
                var parcelPrefabBase = ScriptableObject.CreateInstance<ParcelPrefab>();
                parcelPrefabBase.name        = name;
                parcelPrefabBase.m_LotWidth  = lotWidth;
                parcelPrefabBase.m_LotDepth  = lotDepth;
                parcelPrefabBase.m_ZoneBlock = roadPrefab.m_ZoneBlock;
                prefabBase                   = parcelPrefabBase;
            }

            var placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            prefabBase.AddComponentFrom(placeableObject);

            // (experimental) adding area data
            //var corners = parcelGeo.CornerNodesRelativeToGeometryCenter;
            //var objectSubAreas = ScriptableObject.CreateInstance<ObjectSubAreas>();
            //objectSubAreas.name = "ObjectSubAreas";
            //objectSubAreas.m_SubAreas = new ObjectSubAreaInfo[1] { new () {
            //        m_AreaPrefab = areaPrefabBase,
            //        m_NodePositions = new float3[] {
            //            corners.c0,
            //            corners.c1,
            //            corners.c2,
            //            corners.c3,
            //        },
            //        m_ParentMeshes = new int[4] {
            //             -1,
            //             -1,
            //             0,
            //             0,
            //        },
            //    },
            //};
            // prefabBase.AddComponentFrom(objectSubAreas);

            if (placeholder) {
                var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
                placeableLotPrefabUIObject.m_Icon          = icon;
                placeableLotPrefabUIObject.m_IsDebugObject = true;
                placeableLotPrefabUIObject.m_Priority      = ((lotWidth - 2) * BlockSizes.z) + lotDepth - 1;
                placeableLotPrefabUIObject.m_Group         = uiCategoryPrefab;
                placeableLotPrefabUIObject.active          = true;
                prefabBase.AddComponentFrom(placeableLotPrefabUIObject);
            }

            if (m_PrefabSystem.AddPrefab(prefabBase)) {
                var prefabEntity = m_PrefabSystem.GetEntity(prefabBase);
                m_PrefabBases.Add(prefabBase);
                m_PrefabEntities.Add(prefabBase, prefabEntity);
                return true;
            } else {
                return false;
            }
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
