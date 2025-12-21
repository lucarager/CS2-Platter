// <copyright file="P_ZoneCacheSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Game.Areas;
    using Game.Common;
    using Game.Prefabs;
    using Game.UI;
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;
    using UnityEngine;

    #endregion

    /// <summary>
    /// System responsible for caching Zone Information for other systems.
    /// </summary>
    public partial class P_ZoneCacheSystem  : PlatterGameSystemBase {
        private EntityQuery                   m_ZonesModifiedQuery;
        private EntityQuery                   m_UIAssetCategoryPrefabCreatedQuery;
        private EntityQuery                   m_ZonesAllQuery;
        private NativeHashMap<ushort, Entity> m_ZonePrefabs;
        private PrefabSystem                  m_PrefabSystem;
        private ImageSystem                   m_ImageSystem;

        /// <summary>
        /// Gets the ZoneType for the custom Unzoned zone.
        /// </summary>
        public static ZoneType UnzonedZoneType { get; private set; } = ZoneType.None;

        /// <summary>
        /// Gets the current version of the cache. Increments whenever the cache changes.
        /// </summary>
        public int CacheVersion { get; private set; }

        public Dictionary<ushort, Color>                EdgeColors      { get; private set; }
        public Dictionary<ushort, Color>                FillColors      { get; private set; }
        public Dictionary<ushort, ZoneUIDataModel>      ZoneUIData      { get; private set; }
        public Dictionary<Entity, AssetPackUIDataModel> AssetPackUIData { get; private set; }
        public Dictionary<Entity, ZoneGroupUIDataModel> ZoneGroupUIData { get; private set; }
        public NativeHashMap<ushort, Entity>            ZonePrefabs     => m_ZonePrefabs;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ImageSystem  = World.GetOrCreateSystemManaged<ImageSystem>();

            m_ZonesModifiedQuery = SystemAPI.QueryBuilder()
                                       .WithAll<ZoneData, PrefabData>()
                                       .WithAny<Created, Updated, Deleted>()
                                       .Build();
            m_UIAssetCategoryPrefabCreatedQuery = SystemAPI.QueryBuilder()
                                                           .WithAll<UIAssetCategoryData>()
                                                           .WithAny<Created, Updated, Deleted>()
                                                           .Build();
            m_ZonesAllQuery = SystemAPI.QueryBuilder()
                                  .WithAll<ZoneData, PrefabData>()
                                  .Build();

            // Construct data structures
            m_ZonePrefabs   = new NativeHashMap<ushort, Entity>(256, Allocator.Persistent);
            FillColors      = new Dictionary<ushort, Color>();
            EdgeColors      = new Dictionary<ushort, Color>();
            ZoneUIData      = new Dictionary<ushort, ZoneUIDataModel>();
            AssetPackUIData = new Dictionary<Entity, AssetPackUIDataModel>();
            ZoneGroupUIData = new Dictionary<Entity, ZoneGroupUIDataModel>();

            RequireAnyForUpdate(m_ZonesModifiedQuery);
            RequireAnyForUpdate(m_UIAssetCategoryPrefabCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_ZonePrefabs.Dispose();
            base.OnDestroy();
        }

        private void Clear() {
            m_ZonePrefabs.Clear();
            FillColors.Clear();
            EdgeColors.Clear();
            ZoneUIData.Clear();
            AssetPackUIData.Clear();
            ZoneGroupUIData.Clear();
            CacheVersion++;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            // If any UI Asset Category Prefabs were created/modified/deleted, recache all zones to ensure proper groupings
            if (!m_UIAssetCategoryPrefabCreatedQuery.IsEmpty) {
                // Clear existing caches
                Clear();
                CacheZonePrefabs(m_ZonesAllQuery);
                return;
            }

            CacheZonePrefabs(m_ZonesModifiedQuery);
        }

        /// <inheritdoc/>
        protected override void OnGameLoaded(Context context) {
            base.OnGameLoaded(context);
            m_Log.Debug($"OnGameLoaded(context={context})");

            Clear();
            CacheZonePrefabs(m_ZonesAllQuery);
        }

        private void CacheZonePrefabs(EntityQuery query) {
            m_Log.Debug($"CacheZonePrefabs(query={query})");

            var chunkArray        = query.ToArchetypeChunkArray(Allocator.Temp);
            var prefabDataTh      = SystemAPI.GetComponentTypeHandle<PrefabData>();
            var zoneDataTh        = SystemAPI.GetComponentTypeHandle<ZoneData>();
            var deletedTh         = SystemAPI.GetComponentTypeHandle<Deleted>();
            var uIObjectDataTh    = SystemAPI.GetComponentTypeHandle<UIObjectData>();
            var assetPackBufferTh = SystemAPI.GetBufferTypeHandle<AssetPackElement>();
            var hasChanges        = false;

            CompleteDependency();

            foreach (var chunk in chunkArray) {
                var entityArray             = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                var prefabDataArray         = chunk.GetNativeArray(ref prefabDataTh);
                var zoneDataArray           = chunk.GetNativeArray(ref zoneDataTh);
                var uiObjectDataArray       = chunk.GetNativeArray(ref uIObjectDataTh);
                var assetPackBufferAccessor = chunk.GetBufferAccessor(ref assetPackBufferTh);

                for (var k = 0; k < zoneDataArray.Length; k++) {
                    var entity          = entityArray[k];
                    var zonePrefab      = m_PrefabSystem.GetPrefab<ZonePrefab>(prefabDataArray[k]);
                    var zoneData        = zoneDataArray[k];

                    var assetPacks = new List<Entity> { };
                    if (chunk.Has(ref assetPackBufferTh)) {
                        var assetPackBuffer = assetPackBufferAccessor[k];
                        foreach (var pack in assetPackBuffer) {
                            assetPacks.Add(pack.m_Pack);

                            // Exit if asset pack is already cached
                            if (AssetPackUIData.ContainsKey(pack.m_Pack)) continue;

                            var assetPackPrefab = m_PrefabSystem.GetPrefab<AssetPackPrefab>(pack.m_Pack);
                            var icon            = ImageSystem.GetIcon(assetPackPrefab) ?? m_ImageSystem.placeholderIcon;
                            AssetPackUIData[pack.m_Pack] = new AssetPackUIDataModel(assetPackPrefab, icon, pack.m_Pack);
                        }
                    }

                    UIObjectData uiObjectData = default;

                    if (chunk.Has(ref uIObjectDataTh)) {
                        uiObjectData = uiObjectDataArray[k];

                        // Cache zone group if not already cached
                        if (!ZoneGroupUIData.ContainsKey(uiObjectData.m_Group) && m_PrefabSystem.TryGetPrefab<UIAssetCategoryPrefab>(uiObjectData.m_Group, out var uiPrefab)) {
                            var uiObject = uiPrefab.GetComponent<UIObject>();
                            ZoneGroupUIData[uiObjectData.m_Group] = new ZoneGroupUIDataModel(
                                uiPrefab,
                                uiObject.m_Icon,
                                uiObjectData.m_Group
                            );
                        }
                    } else if (!IsCustomUnzoned(zonePrefab)) {
                        // Skip zonePrefabs without UI data unless it's our custom Unzoned
                        continue;                        
                    }

                    if (chunk.Has(ref deletedTh)) {
                        m_ZonePrefabs.Remove(zoneData.m_ZoneType.m_Index);
                        FillColors.Remove(zoneData.m_ZoneType.m_Index);
                        EdgeColors.Remove(zoneData.m_ZoneType.m_Index);
                        ZoneUIData.Remove(zoneData.m_ZoneType.m_Index);
                        hasChanges = true;
                    } else {
                        // Cache Zone
                        m_Log.Debug($"CacheZonePrefabs() -- {zonePrefab.name} -- Adding to cache.");
                        var category = zonePrefab.m_Office ? "Office" : zonePrefab.m_AreaType.ToString();

                        if (IsCustomUnzoned(zonePrefab)) {
                            UnzonedZoneType = zoneData.m_ZoneType;
                            category        = "None";
                        }
                        
                        // Cache
                        m_ZonePrefabs[zoneData.m_ZoneType.m_Index] = entity;
                        FillColors[zoneData.m_ZoneType.m_Index] = zonePrefab.m_Color;
                        EdgeColors[zoneData.m_ZoneType.m_Index] = zonePrefab.m_Edge;
                        ZoneUIData[zoneData.m_ZoneType.m_Index] = new ZoneUIDataModel(
                            zonePrefab.name,
                            GetThumbnail(zonePrefab),
                            category,
                            zoneData.m_ZoneType.m_Index,
                            uiObjectData.m_Group,
                            assetPacks
                        );
                        hasChanges = true;
                    }
                }
            }

            chunkArray.Dispose();
            
            // Increment version if any changes were made
            if (hasChanges) {
                CacheVersion++;
            }
        }

        private static bool IsCustomUnzoned(ZonePrefab zonePrefab) {
            return zonePrefab.name == "Unzoned" && zonePrefab.m_AreaType == Game.Zones.AreaType.Residential;
        }

        private static string GetThumbnail(ZonePrefab zonePrefab) {
            return zonePrefab.name == "Unzoned" ? "coui://platter/Unzoned.svg" : ImageSystem.GetThumbnail(zonePrefab);
        }

        /// <summary>
        /// Struct to store and send Zone Data and to the React UI.
        /// </summary>
        public readonly struct ZoneUIDataModel : IJsonWritable {
            public readonly string       Name;
            public readonly string       Thumbnail;
            public readonly string       AreaType;
            public readonly int          Index;
            public readonly Entity       Group;
            public readonly List<Entity> AssetPacks;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneUIDataModel"/> struct.
            /// </summary>
            public ZoneUIDataModel(string name,
                              string thumbnail,
                              string areaType,
                              int    index, 
                              Entity group,
                              List<Entity> assetPacks) {
                Name       = name;
                Thumbnail  = thumbnail;
                AreaType   = areaType;
                Index      = index;
                Group      = group;
                AssetPacks = assetPacks;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);

                writer.PropertyName("areaType");
                writer.Write(AreaType);

                writer.PropertyName("group");
                writer.Write(Group);

                writer.PropertyName("index");
                writer.Write(Index);

                writer.PropertyName("assetPacks");
                writer.ArrayBegin(AssetPacks.Count);
                foreach (var assetPack in AssetPacks) {
                    writer.Write(assetPack);
                }
                writer.ArrayEnd();

                writer.TypeEnd();
            }
        }

        public readonly struct AssetPackUIDataModel : IJsonWritable {
            public readonly AssetPackPrefab Prefab;
            public readonly string          Icon;
            public readonly Entity          Entity;

            /// <summary>
            /// Initializes a new instance of the <see cref="AssetPackUIDataModel"/> struct.
            /// </summary>
            public AssetPackUIDataModel(AssetPackPrefab prefab, string icon, Entity entity) {
                Prefab = prefab;
                Icon   = icon;
                Entity = entity;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("entity");
                writer.Write(Entity);

                writer.PropertyName("name");
                writer.Write(Prefab.name);

                writer.PropertyName("icon");
                writer.Write(Icon);

                writer.TypeEnd();
            }
        }

        public readonly struct ZoneGroupUIDataModel : IJsonWritable {
            public readonly UIAssetCategoryPrefab Prefab;
            public readonly string                Icon;
            public readonly Entity                Entity;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneGroupUIDataModel"/> struct.
            /// </summary>
            public ZoneGroupUIDataModel(UIAssetCategoryPrefab prefab, string icon, Entity entity) {
                Prefab = prefab;
                Icon   = icon;
                Entity = entity;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("entity");
                writer.Write(Entity);

                writer.PropertyName("name");
                writer.Write(Prefab.name);

                writer.PropertyName("icon");
                writer.Write(Icon);

                writer.TypeEnd();
            }
        }
    }
}