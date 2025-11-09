// <copyright file="P_BuildingCacheSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Game.Prefabs; // Added to reference SpawnableBuildingData
using Platter.Components; // Added to reference ParcelData

namespace Platter.Systems {
    using Unity.Mathematics;

    /// <summary>
    /// System responsible for caching Building Information for other systems.
    /// </summary>
    public partial class P_BuildingCacheSystem : PlatterGameSystemBase {
        // Data
        private NativeHashMap<float3, int> m_BuildingCount;

        // Queries
        private EntityQuery                m_BuildingQuery;

        public int GetBuildingCount(ushort zoneIndex, int lotWidth, int lotDepth) {
            var key = new float3(
                zoneIndex,
                lotWidth,
                lotDepth
            );
            return !m_BuildingCount.ContainsKey(key) ? 0 : m_BuildingCount[key];
        }

        public bool HasBuildings(ushort zoneIndex, int lotWidth, int lotDepth) {
            var key = new float3(
                zoneIndex,
                lotWidth,
                lotDepth
            );
            return m_BuildingCount.ContainsKey(key) && m_BuildingCount[key] > 0;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_BuildingQuery = SystemAPI.QueryBuilder()
                                       .WithAll<BuildingData, SpawnableBuildingData>() 
                                       .Build();

            m_BuildingCount = new NativeHashMap<float3, int>(5, Allocator.Persistent);

            RequireForUpdate(m_BuildingQuery);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_BuildingCount.Dispose();
            base.OnDestroy();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            // Reset counts for this update cycle
            m_BuildingCount.Clear();

            var chunkArray     = m_BuildingQuery.ToArchetypeChunkArray(Allocator.Temp);
            var buildingDataTH = SystemAPI.GetComponentTypeHandle<BuildingData>();
            var spawnableTH    = SystemAPI.GetComponentTypeHandle<SpawnableBuildingData>();
            CompleteDependency();

            foreach (var chunk in chunkArray) {
                var entityArray    = chunk.GetNativeArray(SystemAPI.GetEntityTypeHandle());
                var buildingArray  = chunk.GetNativeArray(ref buildingDataTH);
                var spawnableArray = chunk.GetNativeArray(ref spawnableTH);

                for (var i = 0; i < entityArray.Length; i++) {
                    var building  = buildingArray[i];
                    var spawnable = spawnableArray[i];
                    var zoneData  = EntityManager.GetComponentData<ZoneData>(spawnable.m_ZonePrefab);

                    // Store building
                    var key = new float3(
                        zoneData.m_ZoneType.m_Index,
                        building.m_LotSize.x,
                        building.m_LotSize.y);
                    if (m_BuildingCount.ContainsKey(key)) {
                        m_BuildingCount[key]++;
                    } else {
                        m_BuildingCount.Add(key, 1);
                    }
                }
            }

            chunkArray.Dispose();
        }
    }
}