// <copyright file="P_BuildingToParcelReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// System responsible for connecting buildings to their parcels.
    /// </summary>
    public partial class P_BuildingToParcelReferenceSystem : GameSystemBase {
        // Queries
        private EntityQuery m_Query;

        // Systems
        private ModificationBarrier2 m_ModificationBarrier2;

        private P_ParcelSearchSystem m_ParcelSearchSystem;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingToParcelReferenceSystem));
            m_Log.Debug("OnCreate()");

            // Systems
            m_ModificationBarrier2 = World.GetOrCreateSystemManaged<ModificationBarrier2>();
            m_ParcelSearchSystem   = World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Building, LinkedParcel, GrowableBuilding>()
                               .WithAny<TransformUpdated>()
                               .WithNone<Temp, Hidden>()
                               .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            var updateJobHandle = new ProcessUpdatedBuildingsJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<TransformUpdated>(true),
                SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                SystemAPI.GetComponentTypeHandle<Transform>(true),
                m_ParcelSearchSystem.GetStaticSearchTree(true, out var parcelSearchJobHandle),
                SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                SystemAPI.GetComponentLookup<BuildingData>(true),
                SystemAPI.GetComponentLookup<Parcel>(),
                m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter(),
                SystemAPI.GetComponentTypeHandle<LinkedParcel>(true)
            ).ScheduleParallel(m_Query, JobHandle.CombineDependencies(Dependency, parcelSearchJobHandle));

            m_ParcelSearchSystem.AddSearchTreeReader(parcelSearchJobHandle);
            m_ModificationBarrier2.AddJobHandleForProducer(updateJobHandle);

            Dependency = updateJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        public struct ProcessUpdatedBuildingsJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                         m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<TransformUpdated>    m_TransformUpdatedComponentTypeHandle;
            [ReadOnly] private ComponentTypeHandle<PrefabRef>           m_PrefabRefComponentTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Transform>           m_TransformComponentTypeHandle;
            [ReadOnly] private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ParcelSearchTree;
            [ReadOnly] private ComponentLookup<ObjectGeometryData>      m_ObjectGeometryDataLookup;
            [ReadOnly] private ComponentLookup<BuildingData>            m_BuildingDataLookup;
            private            ComponentLookup<Parcel>                  m_ParcelLookup;
            private            EntityCommandBuffer.ParallelWriter       m_CommandBuffer;
            private            ComponentTypeHandle<LinkedParcel>        m_LinkedParcelComponentComponentTypeHandle;

            public ProcessUpdatedBuildingsJob(EntityTypeHandle entityTypeHandle, ComponentTypeHandle<TransformUpdated> transformUpdatedComponentTypeHandle,
                                              ComponentTypeHandle<PrefabRef> prefabRefComponentTypeHandle,
                                              ComponentTypeHandle<Transform> transformComponentTypeHandle,
                                              NativeQuadTree<Entity, QuadTreeBoundsXZ> parcelSearchTree,
                                              ComponentLookup<ObjectGeometryData> objectGeometryDataLookup, ComponentLookup<BuildingData> buildingDataLookup,
                                              ComponentLookup<Parcel> parcelLookup, EntityCommandBuffer.ParallelWriter commandBuffer,
                                              ComponentTypeHandle<LinkedParcel> linkedParcelComponentComponentTypeHandle) {
                m_EntityTypeHandle                         = entityTypeHandle;
                m_TransformUpdatedComponentTypeHandle      = transformUpdatedComponentTypeHandle;
                m_PrefabRefComponentTypeHandle             = prefabRefComponentTypeHandle;
                m_TransformComponentTypeHandle             = transformComponentTypeHandle;
                m_ParcelSearchTree                         = parcelSearchTree;
                m_ObjectGeometryDataLookup                 = objectGeometryDataLookup;
                m_BuildingDataLookup                       = buildingDataLookup;
                m_ParcelLookup                             = parcelLookup;
                m_CommandBuffer                            = commandBuffer;
                m_LinkedParcelComponentComponentTypeHandle = linkedParcelComponentComponentTypeHandle;
            }

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray       = chunk.GetNativeArray(m_EntityTypeHandle);
                var linkedParcelArray = chunk.GetNativeArray(ref m_LinkedParcelComponentComponentTypeHandle);
                var prefabRefArray    = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var transformArray    = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity       = entityArray[i];
                    var linkedParcel = linkedParcelArray[i];
                    var wasMoved     = chunk.Has(ref m_TransformUpdatedComponentTypeHandle);

                    if (wasMoved) {
                        m_CommandBuffer.RemoveComponent<TransformUpdated>(unfilteredChunkIndex, entity);
                    }

                    if (linkedParcel.m_Parcel != Entity.Null && !wasMoved) {
                        continue;
                    }

                    var prefab             = prefabRefArray[i];
                    var transform          = transformArray[i];
                    var objectGeometryData = m_ObjectGeometryDataLookup[prefab.m_Prefab];
                    var buildingData       = m_BuildingDataLookup[prefab.m_Prefab];
                    var bounds             = ObjectUtils.CalculateBounds(transform.m_Position, transform.m_Rotation, objectGeometryData);
                    var position           = BuildingUtils.CalculateFrontPosition(transform, buildingData.m_LotSize.y);

                    var findParcelIterator = new Iterator(
                        bounds,
                        position
                    );

                    m_ParcelSearchTree.Iterate(ref findParcelIterator);

                    if (findParcelIterator.MatchingParcel == Entity.Null ||
                        findParcelIterator.MatchingParcel == linkedParcel.m_Parcel) {
                        continue;
                    }

                    var parcel = m_ParcelLookup[findParcelIterator.MatchingParcel];
                    parcel.m_Building                                 = entity;
                    m_ParcelLookup[findParcelIterator.MatchingParcel] = parcel;
                    linkedParcelArray[i]                              = new LinkedParcel(findParcelIterator.MatchingParcel);
                }
            }

            private struct Iterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                private Bounds3 m_Bounds;
                private float   m_BestDistance;
                private float3  m_Position;
                public  Entity  MatchingParcel;

                public Iterator(Bounds3 bounds, float3 position) {
                    m_Bounds       = bounds;
                    m_Position     = position;
                    MatchingParcel = Entity.Null;
                    m_BestDistance = 30f;
                }

                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz); }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity parcelEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    var distance = MathUtils.Distance(bounds.m_Bounds, m_Position);

                    // If distance exceeds our "best", exit
                    if (distance >= m_BestDistance) {
                        return;
                    }

                    m_BestDistance = distance;
                    MatchingParcel = parcelEntity;
                }
            }
        }
    }
}