// <copyright file="RoadConnectionSystem.CreateEntitiesQueueJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct CreateEntitiesQueueJob : IJobChunk {
            /// <summary>
            /// todo.
            /// </summary>
            public NativeQueue<Entity>.ParallelWriter m_ParcelEntitiesQueue;

            /// <summary>
            /// todo.
            /// </summary>
            public NativeQueue<Entity>.ParallelWriter m_EdgeEntitiesQueue;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingBufferTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<Deleted> m_DeletedTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ObjectSearchTree;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<EdgeGeometry> m_EdgeGeometryComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Parcel> m_ParcelComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Transform> m_TransformComponentLookup;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var connectedBuildingsBufferAccessor = chunk.GetBufferAccessor<ConnectedBuilding>(ref m_ConnectedBuildingBufferTypeHandle);

                if (connectedBuildingsBufferAccessor.Length != 0) {
#if !USE_BURST
                    PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] CreateEntitiesQueueJob() -- connectedBuildingsBufferAccessor {connectedBuildingsBufferAccessor.Length} entries.");
#endif

                    // todo handle deletion
                    if (!chunk.Has<Deleted>(ref m_DeletedTypeHandle)) {
                        var edgeGeoArray = chunk.GetNativeArray<EdgeGeometry>(ref m_EdgeGeometryTypeHandle);
                        var startNodeGeoArray = chunk.GetNativeArray<StartNodeGeometry>(ref m_StartNodeGeometryTypeHandle);
                        var endNodeGeoArray = chunk.GetNativeArray<EndNodeGeometry>(ref m_EndNodeGeometryTypeHandle);

#if !USE_BURST
                        PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] CreateEntitiesQueueJob() -- edgeGeoArray {edgeGeoArray.Length} entries.");
#endif

                        for (var k = 0; k < edgeGeoArray.Length; k++) {
                            var edgeGeometry = edgeGeoArray[k];
                            var startNodeGeo = startNodeGeoArray[k].m_Geometry;
                            var endNodeGeo = endNodeGeoArray[k].m_Geometry;

                            FindParcelNextToRoadIterator findParcelIterator = default;

                            findParcelIterator.m_Bounds = MathUtils.Expand(edgeGeometry.m_Bounds | startNodeGeo.m_Bounds | endNodeGeo.m_Bounds, RoadConnectionSystem.MaxDistance);
                            findParcelIterator.m_EdgeGeometry = edgeGeometry;
                            findParcelIterator.m_StartGeometry = startNodeGeo;
                            findParcelIterator.m_EndGeometry = endNodeGeo;
                            findParcelIterator.m_MinDistance = RoadConnectionSystem.MaxDistance;
                            findParcelIterator.m_ParcelEntitiesQueue = m_ParcelEntitiesQueue;
                            findParcelIterator.m_PrefabRefComponentLookup = m_PrefabRefComponentLookup;
                            findParcelIterator.m_EdgeGeometryComponentLookup = m_EdgeGeometryComponentLookup;
                            findParcelIterator.m_StartNodeGeometryComponentLookup = m_StartNodeGeometryComponentLookup;
                            findParcelIterator.m_EndNodeGeometryComponentLookup = m_EndNodeGeometryComponentLookup;
                            findParcelIterator.m_ParcelDataComponentLookup = m_ParcelDataComponentLookup;
                            findParcelIterator.m_ParcelComponentLookup = m_ParcelComponentLookup;
                            findParcelIterator.m_TransformComponentLookup = m_TransformComponentLookup;

                            m_ObjectSearchTree.Iterate<FindParcelNextToRoadIterator>(ref findParcelIterator, 0);
                        }

                        return;
                    }
                } else {
                    var parcelEntityArray = chunk.GetNativeArray(m_EntityTypeHandle);
#if !USE_BURST
                    PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] CreateEntitiesQueueJob() -- parcelEntityArray {parcelEntityArray.Length} entries.");
#endif
                    for (var m = 0; m < parcelEntityArray.Length; m++) {
                        m_ParcelEntitiesQueue.Enqueue(parcelEntityArray[m]);
                    }
                }
            }

            private struct FindParcelNextToRoadIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                /// <summary>
                /// todo.
                /// </summary>
                public Bounds3 m_Bounds;

                /// <summary>
                /// todo.
                /// </summary>
                public EdgeGeometry m_EdgeGeometry;

                /// <summary>
                /// todo.
                /// </summary>
                public EdgeNodeGeometry m_StartGeometry;

                /// <summary>
                /// todo.
                /// </summary>
                public EdgeNodeGeometry m_EndGeometry;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<PrefabRef> m_PrefabRefComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<Parcel> m_ParcelComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<Transform> m_TransformComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public float m_MinDistance;

                /// <summary>
                /// todo.
                /// </summary>
                public NativeQueue<Entity>.ParallelWriter m_ParcelEntitiesQueue;

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity parcelEntity) {
#if !USE_BURST
                    PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] FindParcelNextToRoadIterator.Iterate() -- parcelEntity {parcelEntity}.");
#endif

                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    var parcelPrefabRef = m_PrefabRefComponentLookup[parcelEntity];
                    var parcelData = m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                    var parcelTransform = m_TransformComponentLookup[parcelEntity];

                    // The "front position" is the point where a parcel is expected to connect to a road.
                    var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);
                    var frontPosition = ParcelUtils.GetWorldPosition(parcelTransform, parcelGeo.FrontNode);

                    // Check if this parcel is within the bounds of the road
                    if (!MathUtils.Intersect(m_Bounds.xz, frontPosition.xz)) {
                        return;
                    }

                    // Calculate the distance between parcel and road
                    var distance = m_MinDistance;
                    RoadConnectionSystem.CheckDistance(m_EdgeGeometry, m_StartGeometry, m_EndGeometry, frontPosition, true, ref distance);

                    // If valid...
                    if (distance < m_MinDistance) {
                        var parcel = m_ParcelComponentLookup[parcelEntity];

                        // Check if we need to compare to an already connected road aka "Are we closer?"
                        if (parcel.m_RoadEdge != Entity.Null) {
                            var curEdgeGeo = m_EdgeGeometryComponentLookup[parcel.m_RoadEdge];
                            var curStartNodeGeo = m_StartNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                            var curEndNodeGeo = m_EndNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                            var curDistance = m_MinDistance;
                            RoadConnectionSystem.CheckDistance(curEdgeGeo, curStartNodeGeo, curEndNodeGeo, frontPosition, true, ref curDistance);

                            // If new road is closer, add parcel to queue
                            if (distance < curDistance) {
                                m_ParcelEntitiesQueue.Enqueue(parcelEntity);
                                return;
                            }
                        } else {
                            // New road just dropped! Add parcel to queue
                            m_ParcelEntitiesQueue.Enqueue(parcelEntity);
                        }
                    }
                }
            }
        }
    }
}
