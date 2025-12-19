// <copyright file="P_BuildingSpawnSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Entities;
    using Components;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using SubArea = Game.Areas.SubArea;
    using SubObject = Game.Objects.SubObject;

    #endregion

    /// <summary>
    /// System responsible for adding the GrowableBuilding and LinkedParcel components to buildings.
    /// </summary>
    public partial class P_BuildingSpawnSystem : PlatterGameSystemBase {
        // Queries
        private EntityQuery m_SpawnedBuildingQuery;

        private PrefabSystem m_PrefabSystem;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingSpawnSystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_SpawnedBuildingQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Building, GrowableBuilding, LinkedParcel>()
                                              .WithAny<Updated>()
                                              .WithNone<Temp, Deleted, BoundaryShifted>()
                                              .Build();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Update Cycle
            RequireForUpdate(m_SpawnedBuildingQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            var entities = m_SpawnedBuildingQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                m_Log.Debug($"OnUpdate() -- {entity}");

                var prefabRef                 = EntityManager.GetComponentData<PrefabRef>(entity);
                var prefab                    = m_PrefabSystem.GetPrefab<BuildingPrefab>(prefabRef);
                var prefabEntity              = m_PrefabSystem.GetEntity(prefab);
                var boundarySubObjectBuffer   = EntityManager.GetBuffer<BoundarySubObjectData>(prefabEntity);
                var boundarySubAreaNodeBuffer = EntityManager.GetBuffer<BoundarySubAreaNodeData>(prefabEntity);
                var boundarySubLaneBuffer     = EntityManager.GetBuffer<BoundarySubLaneData>(prefabEntity);
                var boundarySubNetBuffer      = EntityManager.GetBuffer<BoundarySubNetData>(prefabEntity);
                var subAreasBuffer            = EntityManager.GetBuffer<SubArea>(entity);
                var subObjectBuffer           = EntityManager.GetBuffer<SubObject>(entity);

                var tempShiftBy = 10f;

                foreach (var nodeConfig in boundarySubAreaNodeBuffer) {
                    if (!EntityManager.TryGetBuffer<Node>(subAreasBuffer[nodeConfig.areaIndex].m_Area, false, out var nodesBuffer) ||
                        nodesBuffer.Length <= nodeConfig.relIndex) {
                        continue;
                    }

                    ;

                    var node = nodesBuffer[nodeConfig.relIndex];

                    if (nodeConfig.projectionFilter.x) {
                        node.m_Position.z -= tempShiftBy; // front
                    }

                    if (nodeConfig.projectionFilter.y) {
                        node.m_Position.x -= tempShiftBy; // left
                    }

                    if (nodeConfig.projectionFilter.z) {
                        node.m_Position.z += tempShiftBy; // rear
                    }

                    if (nodeConfig.projectionFilter.w) {
                        node.m_Position.x += tempShiftBy; // right
                    }

                    nodesBuffer[nodeConfig.relIndex] = node;
                }

                foreach (var subObjectConfig in boundarySubObjectBuffer) {
                    if (!EntityManager.TryGetComponent<Transform>(subObjectBuffer[subObjectConfig.index].m_SubObject, out var transform)) {
                        continue;
                    }

                    // todo this unfortunately needs to happen at runtime unless we can figure out the random factor mapping
                    // todo also check SpawnLocationElement
                    //if (subObjectConfig.projectionFilter.x) {
                    //    transform.m_Position.z -= tempShiftBy; // front
                    //}
                    //if (subObjectConfig.projectionFilter.y) {
                    //    transform.m_Position.x -= tempShiftBy; // left
                    //}
                    //if (subObjectConfig.projectionFilter.z) {
                    //    transform.m_Position.z += tempShiftBy; // rear
                    //}
                    //if (subObjectConfig.projectionFilter.w) {
                    //    transform.m_Position.x += tempShiftBy; // right
                    //}

                    EntityManager.SetComponentData(entity, transform);
                }

                EntityManager.AddComponent<BoundaryShifted>(entity);
            }
        }

        public struct ProcessBuildingJob : IJob {
            public void Execute() { }
        }
    }
}