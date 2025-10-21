// <copyright file="P_BuildingToParcelReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Drawing;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Transform = Game.Objects.Transform;

    /// <summary>
    /// System responsible for connecting buildings to their parcels.
    /// </summary>
    public partial class P_BuildingToParcelReferenceSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_Query;

        // Systems
        private ModificationBarrier2 m_ModificationBarrier2;
        private P_ParcelSearchSystem m_ParcelSearchSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingToParcelReferenceSystem));
            m_Log.Debug($"OnCreate()");

            // Systems
            m_ModificationBarrier2 = World.GetOrCreateSystemManaged<ModificationBarrier2>();
            m_ParcelSearchSystem   = World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();

            // Queries
            // Todo this runs way too much, add filters!
            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Building, LinkedParcel, GrowableBuilding>()
                .WithAny<Updated, BatchesUpdated, TransformUpdated>()
                .WithNone<Temp>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate()");

            var updateJobHandle = new ProcessUpdatedBuildingsJob(
                entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
                linkedParcelComponentTypeHandle: SystemAPI.GetComponentTypeHandle<LinkedParcel>(false),
                transformUpdatedComponentTypeHandle: SystemAPI.GetComponentTypeHandle<TransformUpdated>(true),
                prefabRefComponentTypeHandle: SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                transformComponentTypeHandle: SystemAPI.GetComponentTypeHandle<Transform>(true),
                parcelSearchTree: m_ParcelSearchSystem.GetStaticSearchTree(true, out var parcelSearchJobHandle),
                prefabObjectGeometryDataLookup: SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                commandBuffer: m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter()
            ).ScheduleParallel(m_Query, JobHandle.CombineDependencies(base.Dependency, parcelSearchJobHandle));

            m_ParcelSearchSystem.AddSearchTreeReader(parcelSearchJobHandle);
            m_ModificationBarrier2.AddJobHandleForProducer(updateJobHandle);

            base.Dependency = updateJobHandle;
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
            [ReadOnly] private ComponentLookup<ObjectGeometryData>      m_PrefabObjectGeometryDataLookup;
            private            EntityCommandBuffer.ParallelWriter       m_CommandBuffer;
            private ComponentTypeHandle<LinkedParcel> m_LinkedParcelComponentComponentTypeHandle;

            public ProcessUpdatedBuildingsJob(EntityTypeHandle entityTypeHandle, ComponentTypeHandle<LinkedParcel> linkedParcelComponentTypeHandle, ComponentTypeHandle<TransformUpdated> transformUpdatedComponentTypeHandle, ComponentTypeHandle<PrefabRef> prefabRefComponentTypeHandle, ComponentTypeHandle<Transform> transformComponentTypeHandle, NativeQuadTree<Entity, QuadTreeBoundsXZ> parcelSearchTree, ComponentLookup<ObjectGeometryData> prefabObjectGeometryDataLookup, EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle = entityTypeHandle;
                m_LinkedParcelComponentComponentTypeHandle = linkedParcelComponentTypeHandle;
                m_TransformUpdatedComponentTypeHandle = transformUpdatedComponentTypeHandle;
                m_PrefabRefComponentTypeHandle = prefabRefComponentTypeHandle;
                m_TransformComponentTypeHandle = transformComponentTypeHandle;
                m_ParcelSearchTree = parcelSearchTree;
                m_PrefabObjectGeometryDataLookup = prefabObjectGeometryDataLookup;
                m_CommandBuffer = commandBuffer;
            }

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray          = chunk.GetNativeArray(m_EntityTypeHandle);
                var linkedParcelArray    = chunk.GetNativeArray(ref m_LinkedParcelComponentComponentTypeHandle);
                var prefabRefArray       = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var transformArray       = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity  = entityArray[i];
                    var linkedParcel = linkedParcelArray[i];
                    var wasMoved = chunk.Has(ref m_TransformUpdatedComponentTypeHandle);

                    if (wasMoved) {
                        m_CommandBuffer.RemoveComponent<TransformUpdated>(unfilteredChunkIndex, entity);
                    }

                    if (linkedParcel.m_Parcel != Entity.Null && !wasMoved) {
                        continue;
                    }

                    var prefab             = prefabRefArray[i].m_Prefab;
                    var transform          = transformArray[i];
                    var objectGeometryData = m_PrefabObjectGeometryDataLookup[prefab];
                    var bounds = ObjectUtils.CalculateBounds(transform.m_Position, transform.m_Rotation, objectGeometryData);

                    var findParcelIterator = new Iterator(
                        bounds: bounds
                    );

                    m_ParcelSearchTree.Iterate(ref findParcelIterator, 0);

                    if (findParcelIterator.MatchingParcel != Entity.Null &&
                        findParcelIterator.MatchingParcel != linkedParcel.m_Parcel) {
                        linkedParcelArray[i] = new LinkedParcel(findParcelIterator.MatchingParcel);
                    }
                }
            }

            private struct Iterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                private Bounds3 m_Bounds;
                public Entity   MatchingParcel;

                public Iterator(Bounds3 bounds) {
                    m_Bounds = bounds;
                    MatchingParcel = Entity.Null;
                }

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity parcelEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    MatchingParcel = parcelEntity;
                }
            }
        }
    }
}
