// <copyright file="P_BuildingCacheSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// System responsible for caching Building Information for other systems.
    /// </summary>
    public partial class P_BuildingCacheSystem  : PlatterGameSystemBase {
        /// <summary>
        /// Struct to hold building access count data for a zone/size combination.
        /// </summary>
        public struct BuildingAccessCount {
            public int Total;
            public int FrontAccessOnly;
            public int LeftAccess;
            public int RightAccess;
            public int Corner;
            public int BackAccess;
        }

        // Queries
        private EntityQuery m_BuildingQuery;

        // Data
        private NativeHashMap<float3, BuildingAccessCount> m_BuildingCount;

        public int GetBuildingCount(ushort zoneIndex, int lotWidth, int lotDepth) {
            var key = new float3(
                zoneIndex,
                lotWidth,
                lotDepth
            );
            return !m_BuildingCount.ContainsKey(key) ? 0 : m_BuildingCount[key].Total;
        }

        public bool HasBuildings(ushort zoneIndex, int lotWidth, int lotDepth) {
            var key = new float3(
                zoneIndex,
                lotWidth,
                lotDepth
            );
            return m_BuildingCount.ContainsKey(key) && m_BuildingCount[key].Total > 0;
        }

        /// <summary>
        /// Get all building access counts for a given zone and lot size.
        /// </summary>
        public BuildingAccessCount GetBuildingAccessCount(ushort zoneIndex, int lotWidth, int lotDepth) {
            var key = new float3(
                zoneIndex,
                lotWidth,
                lotDepth
            );
            return m_BuildingCount.ContainsKey(key) ? m_BuildingCount[key] : default;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_BuildingQuery = SystemAPI.QueryBuilder()
                                       .WithAll<BuildingData, SpawnableBuildingData>()
                                       .Build();

            m_BuildingCount = new NativeHashMap<float3, BuildingAccessCount>(5, Allocator.Persistent);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_BuildingCount.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate() { }

        protected override void OnGameLoadingComplete(Purpose  purpose,
                                                      GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);

            m_Log.Debug("OnGameLoadingComplete()");

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

                    if (spawnable.m_Level != 1) {
                        continue;
                    }

                    var zoneData  = EntityManager.GetComponentData<ZoneData>(spawnable.m_ZonePrefab);
                    var hasLeft   = (building.m_Flags & BuildingFlags.LeftAccess)  != 0;
                    var hasRight  = (building.m_Flags & BuildingFlags.RightAccess) != 0;

                    // Construct key from zone, then size x and y
                    var key = new float3(
                        zoneData.m_ZoneType.m_Index,
                        building.m_LotSize.x,
                        building.m_LotSize.y);

                    if (m_BuildingCount.ContainsKey(key)) {
                        var count = m_BuildingCount[key];
                        count.Total++;

                        if (hasLeft) {
                            count.LeftAccess++;
                        }
                        if (hasRight) {
                            count.RightAccess++;
                        }
                        if (!hasLeft && !hasRight) {
                            count.FrontAccessOnly++;
                        }
                        if (hasLeft || hasRight) {
                            count.Corner++;
                        }
                        if ((building.m_Flags & BuildingFlags.BackAccess) != 0) {
                            count.BackAccess++;
                        }
                        
                        m_BuildingCount[key] = count;
                    } else {
                        var count = new BuildingAccessCount { Total = 1 };
                        
                        // Count access flags
                        if (hasLeft) {
                            count.LeftAccess = 1;
                        }
                        if (hasRight) {
                            count.RightAccess = 1;
                        }
                        if (!hasLeft && !hasRight) {
                            count.FrontAccessOnly = 1;
                        }
                        if (hasLeft || hasRight) {
                            count.Corner = 1;
                        }
                        if ((building.m_Flags & BuildingFlags.BackAccess) != 0) {
                            count.BackAccess = 1;
                        }
                        
                        m_BuildingCount.Add(key, count);
                    }
                }
            }

            chunkArray.Dispose();
        }
    }
}