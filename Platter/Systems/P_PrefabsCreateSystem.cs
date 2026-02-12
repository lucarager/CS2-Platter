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
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for generating custom prefabs on game load.
    /// </summary>
    public partial class P_PrefabsCreateSystem : PlatterGameSystemBase {
        /// <summary>
        /// Range of parcel lot sizes we support.
        /// <para>x = min width.</para>
        /// <para>y = min depth.</para>
        /// <para>z = max width.</para>
        /// <para>w = max depth.</para>
        /// todo: Change this to be more inline with vanilla, with y being max width etc.
        /// </summary>
        public static readonly int4 AvailableParcelLotSizes = new(1, 2, 8, 6);

        /// <summary>
        /// Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        /// <summary>
        /// Reference to the game's prefab system for managing prefab entities.
        /// </summary>
        private static PrefabSystem m_PrefabSystem;

        /// <summary>
        /// Configuration for vanilla prefabs to load for further processing.
        /// Maps identifiers to their corresponding PrefabID references.
        /// </summary>
        private readonly Dictionary<string, PrefabID> m_SourcePrefabsDict = new() {
            { "zone", new PrefabID("ZonePrefab", "EU Residential Mixed") },
            { "unzonedZone", new PrefabID("ZonePrefab", "Unzoned") },
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

        /// <summary>
        /// Bidirectional cache mapping between parcel and placeholder entities.
        /// Given a Parcel entity, returns its ParcelPlaceholder entity and vice versa.
        /// </summary>
        private NativeHashMap<Entity, Entity> m_ParcelPairCache;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Initialize native hash map (initial capacity for 2x6x6 = 72 parcels + 1 category + 1 area)
            m_PrefabCache = new NativeHashMap<int, Entity>(100, Allocator.Persistent);
            
            // Initialize reverse cache for Entity -> PrefabBase lookup
            m_PrefabBaseCache = new Dictionary<Entity, PrefabBase>(100);

            // Initialize parcel pair cache (capacity for 72 pairs = 144 entries since it's bidirectional)
            m_ParcelPairCache = new NativeHashMap<Entity, Entity>(144, Allocator.Persistent);

            // Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
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

            if (m_ParcelPairCache.IsCreated) {
                m_ParcelPairCache.Dispose();
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

        /// <summary>
        /// Installs all custom parcel prefabs by creating and registering them with the prefab system.
        /// This includes area prefabs, category prefabs, and individual parcel prefabs for all supported sizes.
        /// </summary>
        private void Install() {
            const string logMethodPrefix = "Install() --";

            // Mark as already _prefabsAreInstalled
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

            m_Log.Debug($"{logMethodPrefix} Creating Unzoned Prefab...");
            CreateUnzonedPrefab((ZonePrefab)prefabBaseDict["unzonedZone"], out var unzonedPrefab);

            m_Log.Debug($"{logMethodPrefix} Creating Parcel Prefabs...");
            for (var i = AvailableParcelLotSizes.x; i <= AvailableParcelLotSizes.z; i++)
            for (var j = AvailableParcelLotSizes.y; j <= AvailableParcelLotSizes.w; j++) {
                Entity parcelEntity = Entity.Null;
                Entity placeholderEntity = Entity.Null;

                if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], uiCategoryPrefab, areaPrefab, false, out parcelEntity)) {
                    m_Log.Debug($"Created Parcel Prefab {i}x{j}");
                } else {
                    m_Log.Error($"{logMethodPrefix} Failed adding Parcel Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
                }

                if (CreateParcelPrefab(i, j, (RoadPrefab)prefabBaseDict["road"], uiCategoryPrefab, areaPrefab, true, out placeholderEntity)) {
                    m_Log.Debug($"Created ParcelPlaceholder Prefab {i}x{j}");
                } else {
                    m_Log.Error($"{logMethodPrefix} Failed adding ParcelPlaceholder Prefab {i}x{j} to PrefabSystem, exiting prematurely.");
                }

                // Register the parcel pair in the bidirectional cache
                if (parcelEntity != Entity.Null && placeholderEntity != Entity.Null) {
                    m_ParcelPairCache[parcelEntity] = placeholderEntity;
                    m_ParcelPairCache[placeholderEntity] = parcelEntity;
                    m_Log.Debug($"Registered parcel pair {i}x{j} in cache");
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

        /// <summary>
        /// Creates a parcel prefab with the specified dimensions and properties.
        /// </summary>
        /// <param name="lotWidth">The width of the parcel lot.</param>
        /// <param name="lotDepth">The depth of the parcel lot.</param>
        /// <param name="roadPrefab">The road prefab to use for zone block configuration.</param>
        /// <param name="uiCategoryPrefab">The UI category prefab to group this parcel under.</param>
        /// <param name="areaPrefabBase">The area prefab base for enclosed area configuration.</param>
        /// <param name="placeholder">If true, creates a placeholder prefab; otherwise creates a regular parcel prefab.</param>
        /// <param name="entity">When this method returns, contains the created entity if successful; otherwise, Entity.Null.</param>
        /// <returns>True if the prefab was successfully created and added to the prefab system; otherwise, false.</returns>
        private bool CreateParcelPrefab(int  lotWidth, int lotDepth, RoadPrefab roadPrefab, UIAssetCategoryPrefab uiCategoryPrefab, AreaPrefab areaPrefabBase,
                                        bool placeholder, out Entity entity) {
            var prefix    = placeholder ? "ParcelPlaceholder" : "Parcel";
            var name      = $"{prefix} {lotWidth}x{lotDepth}";
            var icon      = $"coui://platter/Parcel_{lotWidth}x{lotDepth}.svg";

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
                placeableLotPrefabUIObject.m_Priority      = (lotWidth - 2) * AvailableParcelLotSizes.z + lotDepth - 1;
                placeableLotPrefabUIObject.m_Group         = uiCategoryPrefab;
                placeableLotPrefabUIObject.active          = true;
                prefabBase.AddComponentFrom(placeableLotPrefabUIObject);
            }

            if (m_PrefabSystem.AddPrefab(prefabBase)) {
                entity = RegisterPrefabInCache(prefabBase);
                return true;
            }

            entity = Entity.Null;
            return false;
        }

        // Overload for backward compatibility
        private bool CreateParcelPrefab(int  lotWidth, int lotDepth, RoadPrefab roadPrefab, UIAssetCategoryPrefab uiCategoryPrefab, AreaPrefab areaPrefabBase,
                                        bool placeholder = false) {
            return CreateParcelPrefab(lotWidth, lotDepth, roadPrefab, uiCategoryPrefab, areaPrefabBase, placeholder, out _);
        }

        /// <summary>
        /// Creates a UI category prefab for organizing parcel prefabs in the game UI.
        /// </summary>
        /// <param name="originalUICategoryPrefab">The original UI category prefab to base the new category on.</param>
        /// <param name="uiCategoryPrefab">When this method returns, contains the created UI category prefab if successful; otherwise, null.</param>
        /// <returns>True if the category prefab was successfully created and added to the prefab system; otherwise, false.</returns>
        private bool CreateCategoryPrefab(UIAssetCategoryPrefab originalUICategoryPrefab, out UIAssetCategoryPrefab uiCategoryPrefab) {
            const string name = "PlatterCat";
            const string icon = "coui://platter/logo.svg";

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
                RegisterPrefabInCache(uiCategoryPrefabBase);
            }

            return success;
        }

        /// <summary>
        /// Creates an area prefab for parcel enclosed areas with border configuration.
        /// </summary>
        /// <param name="originalAreaPrefab">The original area prefab to clone and modify.</param>
        /// <param name="borderPrefab">The net lane prefab to use for the border of the enclosed area.</param>
        /// <param name="areaPrefab">When this method returns, contains the created area prefab if successful; otherwise, null.</param>
        /// <returns>True if the area prefab was successfully created and added to the prefab system; otherwise, false.</returns>
        private bool CreateParcelAreaPrefab(AreaPrefab originalAreaPrefab, NetLanePrefab borderPrefab, out AreaPrefab areaPrefab) {
            var parecelAreaPrefab = (AreaPrefab)originalAreaPrefab.Clone("Parcel Enclosed Area");

            var enclosedArea = ScriptableObject.CreateInstance<EnclosedArea>();
            enclosedArea.name               = "EnclosedArea";
            enclosedArea.m_BorderLaneType   = borderPrefab;
            enclosedArea.m_CounterClockWise = false;

            parecelAreaPrefab.AddComponentFrom(enclosedArea);

            var success = m_PrefabSystem.AddPrefab(parecelAreaPrefab);

            if (success) {
                RegisterPrefabInCache(parecelAreaPrefab);
                areaPrefab = parecelAreaPrefab;
                return true;
            }

            areaPrefab = null;
            return false;
        }

        /// <summary>
        /// Creates a custom unzoned prefab by cloning the vanilla Unzoned prefab.
        /// </summary>
        /// <param name="originalUnzonedPrefab">The original Unzoned zone prefab to clone.</param>
        /// <param name="unzonedPrefab">When this method returns, contains the created unzoned prefab if successful; otherwise, null.</param>
        /// <returns>True if the unzoned prefab was successfully created and added to the prefab system; otherwise, false.</returns>
        private bool CreateUnzonedPrefab(ZonePrefab originalUnzonedPrefab, out ZonePrefab unzonedPrefab) {
            var clonedUnzonedPrefab = (ZonePrefab)originalUnzonedPrefab.Clone("PlatterUnzoned");
            clonedUnzonedPrefab.m_AreaType = Game.Zones.AreaType.Residential;
            clonedUnzonedPrefab.m_Edge = new Color(1f, 0.388f, 0.718f, 0.13f);

            var success = m_PrefabSystem.AddPrefab(clonedUnzonedPrefab);

            if (success) {
                RegisterPrefabInCache(clonedUnzonedPrefab);
                unzonedPrefab = clonedUnzonedPrefab;
                return true;
            }

            unzonedPrefab = null;
            return false;
        }
        
        /// <summary>
        /// Get a cached prefab entity by PrefabID hash.
        /// </summary>
        /// <param name="hash">The hash code of the prefab ID to look up.</param>
        /// <param name="entity">When this method returns, contains the cached entity if found; otherwise, the default entity value.</param>
        /// <returns>True if the entity was found in the cache; otherwise, false.</returns>
        public bool TryGetCachedPrefab(int hash, out Entity entity) { return m_PrefabCache.TryGetValue(hash, out entity); }

        /// <summary>
        /// Get a readonly version of the cache for use in Burst jobs.
        /// </summary>
        /// <returns>A read-only view of the prefab cache that can be safely used in parallel jobs.</returns>
        public NativeHashMap<int, Entity>.ReadOnly GetReadOnlyPrefabCache() { return m_PrefabCache.AsReadOnly(); }

        /// <summary>
        /// Get a cached PrefabBase from a Prefab Entity.
        /// </summary>
        /// <param name="entity">The entity to look up in the prefab base cache.</param>
        /// <param name="prefabBase">When this method returns, contains the cached PrefabBase if found; otherwise, null.</param>
        public bool TryGetCachedPrefabBase(Entity entity, out PrefabBase prefabBase) { return m_PrefabBaseCache.TryGetValue(entity, out prefabBase); }

        /// <summary>
        /// Get the paired entity (placeholder for parcel, or parcel for placeholder).
        /// </summary>
        /// <param name="entity">The entity to look up (either a parcel or placeholder).</param>
        /// <param name="pairedEntity">When this method returns, contains the paired entity if found; otherwise, Entity.Null.</param>
        /// <returns>True if the paired entity was found in the cache; otherwise, false.</returns>
        public bool TryGetParcelPair(Entity entity, out Entity pairedEntity) { return m_ParcelPairCache.TryGetValue(entity, out pairedEntity); }

        /// <summary>
        /// Get the paired PrefabBase (placeholder for parcel, or parcel for placeholder).
        /// </summary>
        /// <param name="entity">The entity to look up (either a parcel or placeholder).</param>
        /// <param name="pairedPrefabBase">When this method returns, contains the paired PrefabBase if found; otherwise, null.</param>
        /// <returns>True if the paired PrefabBase was found; otherwise, false.</returns>
        public bool TryGetParcelPairPrefabBase(Entity entity, out PrefabBase pairedPrefabBase) {
            pairedPrefabBase = null;
            return m_ParcelPairCache.TryGetValue(entity, out var pairedEntity) && 
                   m_PrefabBaseCache.TryGetValue(pairedEntity, out pairedPrefabBase);
        }

        /// <summary>
        /// Get the paired PrefabBase strongly typed (placeholder for parcel, or parcel for placeholder).
        /// </summary>
        /// <typeparam name="T">The type of PrefabBase to cast to (e.g., ParcelPrefab or ParcelPlaceholderPrefab).</typeparam>
        /// <param name="entity">The entity to look up (either a parcel or placeholder).</param>
        /// <param name="pairedPrefab">When this method returns, contains the paired prefab if found and cast successful; otherwise, null.</param>
        /// <returns>True if the paired prefab was found and successfully cast to type T; otherwise, false.</returns>
        public bool TryGetParcelPairPrefabBase<T>(Entity entity, out T pairedPrefab) where T : PrefabBase {
            pairedPrefab = null;
            if (m_ParcelPairCache.TryGetValue(entity, out var pairedEntity) && 
                m_PrefabBaseCache.TryGetValue(pairedEntity, out var prefabBase)) {
                pairedPrefab = prefabBase as T;
                return pairedPrefab != null;
            }
            return false;
        }

        /// <summary>
        /// Get the paired PrefabBase from a source PrefabBase (placeholder for parcel, or parcel for placeholder).
        /// </summary>
        /// <param name="sourcePrefabBase">The source PrefabBase to find the pair for.</param>
        /// <param name="pairedPrefabBase">When this method returns, contains the paired PrefabBase if found; otherwise, null.</param>
        /// <returns>True if the paired PrefabBase was found; otherwise, false.</returns>
        public bool TryGetParcelPairPrefabBase(PrefabBase sourcePrefabBase, out PrefabBase pairedPrefabBase) {
            pairedPrefabBase = null;
            
            // Find the entity for the source PrefabBase by reverse lookup
            Entity sourceEntity = Entity.Null;
            foreach (var kvp in m_PrefabBaseCache) {
                if (kvp.Value == sourcePrefabBase) {
                    sourceEntity = kvp.Key;
                    break;
                }
            }
            
            if (sourceEntity == Entity.Null) {
                return false;
            }
            
            // Now get the paired entity and its PrefabBase
            return m_ParcelPairCache.TryGetValue(sourceEntity, out var pairedEntity) && 
                   m_PrefabBaseCache.TryGetValue(pairedEntity, out pairedPrefabBase);
        }

        /// <summary>
        /// Get the paired PrefabBase strongly typed from a source PrefabBase (placeholder for parcel, or parcel for placeholder).
        /// </summary>
        /// <typeparam name="T">The type of PrefabBase to cast to (e.g., ParcelPrefab or ParcelPlaceholderPrefab).</typeparam>
        /// <param name="sourcePrefabBase">The source PrefabBase to find the pair for.</param>
        /// <param name="pairedPrefab">When this method returns, contains the paired prefab if found and cast successful; otherwise, null.</param>
        /// <returns>True if the paired prefab was found and successfully cast to type T; otherwise, false.</returns>
        public bool TryGetParcelPairPrefabBase<T>(PrefabBase sourcePrefabBase, out T pairedPrefab) where T : PrefabBase {
            pairedPrefab = null;
            if (TryGetParcelPairPrefabBase(sourcePrefabBase, out var prefabBase)) {
                pairedPrefab = prefabBase as T;
                return pairedPrefab != null;
            }
            return false;
        }

        /// <summary>
        /// Get a readonly version of the parcel pair cache for use in Burst jobs.
        /// </summary>
        /// <returns>A read-only view of the parcel pair cache that can be safely used in parallel jobs.</returns>
        public NativeHashMap<Entity, Entity>.ReadOnly GetReadOnlyParcelPairCache() { return m_ParcelPairCache.AsReadOnly(); }

        /// <summary>
        /// Registers a prefab in both caches and logs the registration.
        /// </summary>
        /// <param name="prefabBase">The prefab base to register.</param>
        /// <returns>The entity associated with the registered prefab.</returns>
        private Entity RegisterPrefabInCache(PrefabBase prefabBase) {
            var prefabID     = prefabBase.GetPrefabID();
            var cacheKey     = ParcelUtils.GetHashCode(prefabID);
            var prefabEntity = m_PrefabSystem.GetEntity(prefabBase);

            m_Log.Debug($"Registered prefab {prefabID} with hash {cacheKey}");

            m_PrefabCache[cacheKey] = prefabEntity;
            m_PrefabBaseCache[prefabEntity] = prefabBase;

            return prefabEntity;
        }
    }
}