// <copyright file="P_PrefabsCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;

    #endregion

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

        private static BuildingInitializeSystem m_BuildingInitializeSystem;

        /// <summary>
        /// Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;

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
        /// Cache for prefab entities indexed by PrefabID hash.
        /// </summary>
        private NativeHashMap<int, Entity> m_PrefabCache;

        /// <summary>
        /// Reverse cache mapping from Entity to PrefabBase for quick lookup.
        /// </summary>
        private Dictionary<Entity, PrefabBase> m_PrefabBaseCache;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_PrefabsCreateSystem));
            m_Log.Debug("OnCreate()");

            // Initialize native hash map (initial capacity for 2x6x6 = 72 parcels + 1 category + 1 area)
            m_PrefabCache = new NativeHashMap<int, Entity>(100, Allocator.Persistent);
            
            // Initialize reverse cache for Entity -> PrefabBase lookup
            m_PrefabBaseCache = new Dictionary<Entity, PrefabBase>(100);

            // Systems
            m_PrefabSystem             = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_BuildingInitializeSystem = World.GetOrCreateSystemManaged<BuildingInitializeSystem>();
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            base.OnDestroy();
            if (m_PrefabCache.IsCreated) {
                m_PrefabCache.Dispose();
            }
            if (m_PrefabBaseCache != null) {
                m_PrefabBaseCache.Clear();
            }
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { }

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
            var logMethodPrefix = "Install() --";

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
            for (var i = BlockSizes.x; i <= BlockSizes.z; i++)
            for (var j = BlockSizes.y; j <= BlockSizes.w; j++) {
                if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], uiCategoryPrefab, areaPrefab)) {
                    m_Log.Debug($"Created Parcel Prefab {i}x{j}");
                } else {
                    m_Log.Error($"{logMethodPrefix} Failed adding Parcel Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
                }

                if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], uiCategoryPrefab, areaPrefab, true)) {
                    m_Log.Debug($"Created ParcelPlaceholder Prefab {i}x{j}");
                } else {
                    m_Log.Error($"{logMethodPrefix} Failed adding ParcelPlaceholder Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
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

        private bool CreateParcelPrefab(int  lotWidth, int lotDepth, RoadPrefab roadPrefab, UIAssetCategoryPrefab uiCategoryPrefab, AreaPrefab areaPrefabBase,
                                        bool placeholder = false) {
            var prefix    = "Parcel";
            var name      = $"{prefix} {lotWidth}x{lotDepth}";
            var icon      = $"coui://platter/{prefix}_{lotWidth}x{lotDepth}.svg";
            var parcelGeo = new ParcelGeometry(new int2(lotWidth, lotDepth));

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
            placeableObject.m_XPReward         = 0;
            prefabBase.AddComponentFrom(placeableObject);

            if (placeholder) {
                var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
                placeableLotPrefabUIObject.m_Icon          = icon;
                placeableLotPrefabUIObject.m_IsDebugObject = false;
                placeableLotPrefabUIObject.m_Priority      = (lotWidth - 2) * BlockSizes.z + lotDepth - 1;
                placeableLotPrefabUIObject.m_Group         = uiCategoryPrefab;
                placeableLotPrefabUIObject.active          = true;
                prefabBase.AddComponentFrom(placeableLotPrefabUIObject);
            }

            if (m_PrefabSystem.AddPrefab(prefabBase)) {
                var prefabEntity = m_PrefabSystem.GetEntity(prefabBase);
                var prefabID     = prefabBase.GetPrefabID();
                var cacheKey     = ParcelUtils.GetCustomHashCode(prefabID, placeholder);

                m_Log.Debug($"Populating Prefab Cache. Type: Parcel Key: {cacheKey} prefabID: {prefabID} placeholder: {placeholder.ToString()}");

                m_PrefabCache[cacheKey] = prefabEntity;
                m_PrefabBaseCache[prefabEntity] = prefabBase;
                return true;
            }

            return false;
        }

        private bool CreateCategoryPrefab(UIAssetCategoryPrefab originalUICategoryPrefab, out UIAssetCategoryPrefab uiCategoryPrefab) {
            var name = "PlatterCat";
            var icon = "coui://platter/logo.svg";

            var uiCategoryPrefabBase = ScriptableObject.CreateInstance<UIAssetCategoryPrefab>();
            uiCategoryPrefabBase.name   = name;
            uiCategoryPrefabBase.m_Menu = originalUICategoryPrefab.m_Menu;

            var uiObject = ScriptableObject.CreateInstance<UIObject>();
            uiObject.active          = true;
            uiObject.m_Group         = null;
            uiObject.m_Priority      = 100;
            uiObject.m_Icon          = icon;
            uiObject.m_IsDebugObject = false;
            uiObject.m_Icon          = icon;
            uiCategoryPrefabBase.AddComponentFrom(uiObject);

            var success = m_PrefabSystem.AddPrefab(uiCategoryPrefabBase);
            uiCategoryPrefab = uiCategoryPrefabBase;

            if (success) {
                var prefabID = uiCategoryPrefabBase.GetPrefabID();
                var cacheKey = ParcelUtils.GetCustomHashCode(prefabID);
                var prefabEntity = m_PrefabSystem.GetEntity(uiCategoryPrefabBase);

                m_Log.Debug($"Populating Prefab Cache. Type: Category Key: {cacheKey} prefabID: {prefabID}");
                m_PrefabCache[cacheKey] = prefabEntity;
                m_PrefabBaseCache[prefabEntity] = uiCategoryPrefabBase;
            }

            return success;
        }

        private bool CreateParcelAreaPrefab(AreaPrefab originalAreaPrefab, NetLanePrefab borderPrefab, out AreaPrefab areaPrefab) {
            var parecelAreaPrefab = (AreaPrefab)originalAreaPrefab.Clone("Parcel Enclosed Area");

            var enclosedArea = ScriptableObject.CreateInstance<EnclosedArea>();
            enclosedArea.name               = "EnclosedArea";
            enclosedArea.m_BorderLaneType   = borderPrefab;
            enclosedArea.m_CounterClockWise = false;

            parecelAreaPrefab.AddComponentFrom(enclosedArea);

            var success = m_PrefabSystem.AddPrefab(parecelAreaPrefab);

            if (success) {
                var prefabID = parecelAreaPrefab.GetPrefabID();
                var cacheKey = ParcelUtils.GetCustomHashCode(prefabID);
                var prefabEntity = m_PrefabSystem.GetEntity(parecelAreaPrefab);

                m_Log.Debug($"Populating Prefab Cache. Type: ParcelArea Key: {cacheKey} prefabID: {prefabID}");

                m_PrefabCache[cacheKey] = prefabEntity;
                m_PrefabBaseCache[prefabEntity] = parecelAreaPrefab;
                areaPrefab              = parecelAreaPrefab;
                return true;
            }

            areaPrefab = null;
            return false;
        }
        
        /// <summary>
        /// Get a cached prefab entity by PrefabID.
        /// </summary>
        public bool TryGetCachedPrefab(int hash, out Entity entity) { return m_PrefabCache.TryGetValue(hash, out entity); }

        /// <summary>
        /// Get a readonly version of the cache for use in Burst jobs.
        /// </summary>
        public NativeHashMap<int, Entity>.ReadOnly GetReadOnlyPrefabCache() { return m_PrefabCache.AsReadOnly(); }

        /// <summary>
        /// Get a cached PrefabBase from a Prefab Entity.
        /// </summary>
        public bool TryGetCachedPrefabBase(Entity entity, out PrefabBase prefabBase) { return m_PrefabBaseCache.TryGetValue(entity, out prefabBase); }
    }
}