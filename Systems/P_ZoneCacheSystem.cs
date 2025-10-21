// <copyright file="P_ZoneCacheSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using Game.Common;
    using Game.Prefabs;
    using Game.UI;
    using Game.UI.Tooltip;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
    using Platter.Systems;

    /// <summary>
    /// System responsible for caching Zone Information for other systems.
    /// </summary>
    public partial class P_ZoneCacheSystem : TooltipSystemBase {
        public NativeList<Entity> ZonePrefabs => m_ZonePrefabs;

        public Dictionary<ushort, Color> FillColors => m_FillColors;

        public Dictionary<ushort, Color> EdgeColors => m_EdgeColors;

        public Dictionary<ushort, P_UISystem.ZoneUIData> ZoneUIData => m_ZoneUIData;

        // Systems
        private PrefabSystem m_PrefabSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_CreatedQuery;

        // Data
        private NativeList<Entity>                        m_ZonePrefabs;
        private Dictionary<ushort, Color>                 m_FillColors;
        private Dictionary<ushort, Color>                 m_EdgeColors;
        private Dictionary<ushort, P_UISystem.ZoneUIData> m_ZoneUIData;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ZoneCacheSystem));
            m_Log.Debug($"OnCreate()");

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_CreatedQuery = SystemAPI.QueryBuilder()
                .WithAll<ZoneData>()
                .WithAll<PrefabData>()
                .WithAny<Created>()
                .WithAny<Deleted>()
                .Build();

            m_ZonePrefabs = new NativeList<Entity>(Allocator.Persistent);
            m_FillColors = new Dictionary<ushort, Color>();
            m_EdgeColors = new Dictionary<ushort, Color>();
            m_ZoneUIData = new Dictionary<ushort, P_UISystem.ZoneUIData>();
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_ZonePrefabs.Dispose();
            base.OnDestroy();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_CreatedQuery.IsEmptyIgnoreFilter) {
                return;
            }

            m_Log.Debug($"OnUpdate()");
            CacheZonePrefabs();
        }

        private void CacheZonePrefabs() {
            var chunkArray = m_CreatedQuery.ToArchetypeChunkArray(Allocator.TempJob);
            var prefabDataTh = SystemAPI.GetComponentTypeHandle<PrefabData>();
            var zoneDataTh = SystemAPI.GetComponentTypeHandle<ZoneData>();
            var deletedTh = SystemAPI.GetComponentTypeHandle<Deleted>();
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
                        if (zoneData.m_ZoneType.m_Index                >= m_ZonePrefabs.Length ||
                            m_ZonePrefabs[zoneData.m_ZoneType.m_Index] != entity) {
                            continue;
                        }

                        m_Log.Debug($"OnUpdate() -- {zonePrefab.name} -- Deleting from cache.");
                        m_ZonePrefabs[zoneData.m_ZoneType.m_Index] = Entity.Null;
                        m_FillColors.Remove(zoneData.m_ZoneType.m_Index);
                        m_EdgeColors.Remove(zoneData.m_ZoneType.m_Index);
                        m_ZoneUIData.Remove(zoneData.m_ZoneType.m_Index);
                    } else {
                        // Cache Zone
                        m_Log.Debug($"OnUpdate() -- {zonePrefab.name} -- Adding to cache.");

                        if (zoneData.m_ZoneType.m_Index < m_ZonePrefabs.Length) {
                            m_ZonePrefabs[zoneData.m_ZoneType.m_Index] = entity;
                        } else {
                            while (zoneData.m_ZoneType.m_Index > m_ZonePrefabs.Length) {
                                ref var zonePrefabs = ref m_ZonePrefabs;
                                var     nullEntity  = Entity.Null;
                                zonePrefabs.Add(in nullEntity);
                            }

                            m_ZonePrefabs.Add(in entity);
                        }

                        // Cache
                        m_FillColors.Add(zoneData.m_ZoneType.m_Index, zonePrefab.m_Color);
                        m_EdgeColors.Add(zoneData.m_ZoneType.m_Index, zonePrefab.m_Edge);
                        m_ZoneUIData.Add(
                            zoneData.m_ZoneType.m_Index, 
                            new P_UISystem.ZoneUIData(
                                zonePrefab.name,
                                ImageSystem.GetThumbnail(zonePrefab),
                                "todo category",
                                zoneData.m_ZoneType.m_Index
                            ));
                    }
                }
            }

            chunkArray.Dispose();
        }
    }
}
