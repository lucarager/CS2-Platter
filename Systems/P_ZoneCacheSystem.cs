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
    using Game.Common;
    using Game.Prefabs;
    using Game.UI;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;
    using UnityEngine;

    #endregion

    /// <summary>
    /// System responsible for caching Zone Information for other systems.
    /// </summary>
    public partial class P_ZoneCacheSystem : PlatterGameSystemBase {
        private EntityQuery                         m_ModifiedQuery;
        private EntityQuery                         m_AllQuery;
        private EntityQuery                         m_AssetPackQuery;
        private NativeHashMap<ushort, Entity>       m_ZonePrefabs;
        private PrefabSystem                        m_PrefabSystem;
        private ImageSystem                         m_ImageSystem;
        public  Dictionary<ushort, Color>           EdgeColors      { get; private set; }
        public  Dictionary<ushort, Color>           FillColors      { get; private set; }
        public  Dictionary<ushort, ZoneUIDataModel> ZoneUIData      { get; private set; }
        public  List<AssetPackUIDataModel>          AssetPackUIData { get; private set; }
        public  NativeHashMap<ushort, Entity>       ZonePrefabs     => m_ZonePrefabs;

        private bool m_Zones_InitialCacheComplete = false;
        private bool m_Packs_InitialCacheComplete = false;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ImageSystem  = World.GetOrCreateSystemManaged<ImageSystem>();

            m_ModifiedQuery = SystemAPI.QueryBuilder()
                                       .WithAll<ZoneData, PrefabData>()
                                       .WithAny<Created, Updated, Deleted>()
                                       .Build();

            m_AllQuery = SystemAPI.QueryBuilder()
                                  .WithAll<ZoneData, PrefabData>()
                                  .Build();

            m_AssetPackQuery = SystemAPI.QueryBuilder()
                                        .WithAll<AssetPackData, PrefabData>()
                                        .Build();


            m_ZonePrefabs   = new NativeHashMap<ushort, Entity>(256, Allocator.Persistent);
            FillColors      = new Dictionary<ushort, Color>();
            EdgeColors      = new Dictionary<ushort, Color>();
            ZoneUIData      = new Dictionary<ushort, ZoneUIDataModel>();
            AssetPackUIData = new List<AssetPackUIDataModel>();

            RequireForUpdate(m_ModifiedQuery);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_ZonePrefabs.Dispose();
            base.OnDestroy();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            if (m_ModifiedQuery.IsEmptyIgnoreFilter) {
                return;
            }

            CacheZonePrefabs(m_ModifiedQuery);
        }

        /// <inheritdoc/>
        protected override void OnGameLoaded(Context context) {
            base.OnGameLoaded(context);
            m_Log.Debug($"OnGameLoaded(context={context})");


            if (!m_Zones_InitialCacheComplete) {
                CacheZonePrefabs(m_AllQuery);
            }
            if (!m_Packs_InitialCacheComplete) {
                CacheAssetPacks();
            }
        }

        private void CacheAssetPacks() {
            m_Packs_InitialCacheComplete = true;

            var assetPacksArray = m_AssetPackQuery.ToEntityArray(Allocator.Temp);
            foreach (var assetPackEntity in assetPacksArray) {
                var prefab = m_PrefabSystem.GetPrefab<AssetPackPrefab>(assetPackEntity);
                var icon   = ImageSystem.GetIcon(prefab) ?? m_ImageSystem.placeholderIcon;

                AssetPackUIData.Add(new AssetPackUIDataModel(prefab, icon, assetPackEntity));
            }
        }

        private void CacheZonePrefabs(EntityQuery query) {
            m_Zones_InitialCacheComplete = true;

            var chunkArray        = query.ToArchetypeChunkArray(Allocator.Temp);
            var prefabDataTh      = SystemAPI.GetComponentTypeHandle<PrefabData>();
            var zoneDataTh        = SystemAPI.GetComponentTypeHandle<ZoneData>();
            var deletedTh         = SystemAPI.GetComponentTypeHandle<Deleted>();
            var uIObjectDataTh    = SystemAPI.GetComponentTypeHandle<UIObjectData>();
            var assetPackBufferTh = SystemAPI.GetBufferTypeHandle<AssetPackElement>();

            CompleteDependency();

            foreach (var chunk in chunkArray) {
                var entityArray             = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                var prefabDataArray         = chunk.GetNativeArray(ref prefabDataTh);
                var zoneDataArray           = chunk.GetNativeArray(ref zoneDataTh);
                var assetPackBufferAccessor = chunk.GetBufferAccessor(ref assetPackBufferTh);

                for (var k = 0; k < zoneDataArray.Length; k++) {
                    var entity          = entityArray[k];
                    var zonePrefab      = m_PrefabSystem.GetPrefab<ZonePrefab>(prefabDataArray[k]);
                    var zoneData        = zoneDataArray[k];

                    var assetPacks = new List<Entity> { };
                    if (chunk.Has(ref assetPackBufferTh)) {
                        var assetPackBuffer = assetPackBufferAccessor[k];
                        for (var i = 0; i < assetPackBuffer.Length; i++) {
                            var pack = assetPackBuffer[i];
                            assetPacks.Add(pack.m_Pack);
                        }
                    }

                    if (!chunk.Has(ref uIObjectDataTh) && zonePrefab.name != "Unzoned") {
                        continue;
                    }

                    if (chunk.Has(ref deletedTh)) {
                        m_ZonePrefabs.Remove(zoneData.m_ZoneType.m_Index);
                        FillColors.Remove(zoneData.m_ZoneType.m_Index);
                        EdgeColors.Remove(zoneData.m_ZoneType.m_Index);
                        ZoneUIData.Remove(zoneData.m_ZoneType.m_Index);
                    } else {
                        // Cache Zone
                        m_Log.Debug($"CacheZonePrefabs() -- {zonePrefab.name} -- Adding to cache.");

                        var category = zonePrefab.m_Office ? "Office" : zonePrefab.m_AreaType.ToString();

                        // Cache
                        m_ZonePrefabs[zoneData.m_ZoneType.m_Index] = entity;
                        FillColors[zoneData.m_ZoneType.m_Index]    = zonePrefab.m_Color;
                        EdgeColors[zoneData.m_ZoneType.m_Index]    = zonePrefab.m_Edge;
                        ZoneUIData[zoneData.m_ZoneType.m_Index] = new ZoneUIDataModel(
                            zonePrefab.name,
                            GetThumbnail(zonePrefab),
                            category,
                            zoneData.m_ZoneType.m_Index,
                            assetPacks
                        );
                    }
                }
            }

            chunkArray.Dispose();
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
            public readonly string       Category;
            public readonly int          Index;
            public readonly List<Entity> AssetPacks;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneUIDataModel"/> struct.
            /// </summary>
            public ZoneUIDataModel(string name,
                              string thumbnail,
                              string category,
                              int    index, 
                              List<Entity> assetPacks) {
                Name       = name;
                Thumbnail  = thumbnail;
                Category   = category;
                Index      = index;
                AssetPacks = assetPacks;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);

                writer.PropertyName("category");
                writer.Write(Category);

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
            /// Initializes a new instance of the <see cref="ZoneUIDataModel"/> struct.
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
    }
}