// <copyright file="P_ZoneCacheSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game.Common;
    using Game.Prefabs;
    using Game.UI;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    #endregion

    /// <summary>
    /// System responsible for caching Zone Information for other systems.
    /// </summary>
    public partial class P_ZoneCacheSystem : PlatterGameSystemBase {
        private EntityQuery m_AllQuery;

        // Queries
        private EntityQuery m_ModifiedQuery;

        // Data
        private NativeHashMap<ushort, Entity> m_ZonePrefabs;

        // Systems
        private PrefabSystem              m_PrefabSystem;
        public  Dictionary<ushort, Color> EdgeColors { get; private set; }

        public Dictionary<ushort, Color> FillColors { get; private set; }

        public Dictionary<ushort, P_UISystem.ZoneUIData> ZoneUIData { get; private set; }

        public NativeHashMap<ushort, Entity> ZonePrefabs => m_ZonePrefabs;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_ModifiedQuery = SystemAPI.QueryBuilder()
                                       .WithAll<ZoneData, PrefabData>()
                                       .WithAny<Created, Updated, Deleted>()
                                       .Build();

            m_AllQuery = SystemAPI.QueryBuilder()
                                  .WithAll<ZoneData, PrefabData>()
                                  .Build();

            m_ZonePrefabs = new NativeHashMap<ushort, Entity>(256, Allocator.Persistent);
            FillColors    = new Dictionary<ushort, Color>();
            EdgeColors    = new Dictionary<ushort, Color>();
            ZoneUIData    = new Dictionary<ushort, P_UISystem.ZoneUIData>();

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

            CacheZonePrefabs(m_AllQuery);
        }

        private void CacheZonePrefabs(EntityQuery query) {
            var chunkArray     = query.ToArchetypeChunkArray(Allocator.Temp);
            var prefabDataTh   = SystemAPI.GetComponentTypeHandle<PrefabData>();
            var zoneDataTh     = SystemAPI.GetComponentTypeHandle<ZoneData>();
            var deletedTh      = SystemAPI.GetComponentTypeHandle<Deleted>();
            var uIObjectDataTh = SystemAPI.GetComponentTypeHandle<UIObjectData>();

            CompleteDependency();

            foreach (var archetypeChunk in chunkArray) {
                var entityArray     = archetypeChunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                var prefabDataArray = archetypeChunk.GetNativeArray(ref prefabDataTh);
                var zoneDataArray   = archetypeChunk.GetNativeArray(ref zoneDataTh);

                for (var k = 0; k < zoneDataArray.Length; k++) {
                    var entity     = entityArray[k];
                    var zonePrefab = m_PrefabSystem.GetPrefab<ZonePrefab>(prefabDataArray[k]);
                    var zoneData   = zoneDataArray[k];

                    if (!archetypeChunk.Has(ref uIObjectDataTh) && zonePrefab.name != "Unzoned") {
                        continue;
                    }

                    if (archetypeChunk.Has(ref deletedTh)) {
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
                        ZoneUIData[zoneData.m_ZoneType.m_Index] = new P_UISystem.ZoneUIData(
                            zonePrefab.name,
                            ImageSystem.GetThumbnail(zonePrefab),
                            category,
                            zoneData.m_ZoneType.m_Index
                        );
                    }
                }
            }

            chunkArray.Dispose();
        }
    }
}