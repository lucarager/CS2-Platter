// <copyright file="P_RoadConnectionSystem.CreateEntitiesQueueJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Unity.Burst;

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct CreateEntitiesQueueJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                         m_EntityTypeHandle;
            [ReadOnly] private BufferTypeHandle<ConnectedParcel>        m_ConnectedParcelBufferTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Deleted>             m_DeletedTypeHandle;
            [ReadOnly] private ComponentTypeHandle<EdgeGeometry>        m_EdgeGeometryTypeHandle;
            [ReadOnly] private ComponentTypeHandle<StartNodeGeometry>   m_StartNodeGeometryTypeHandle;
            [ReadOnly] private ComponentTypeHandle<EndNodeGeometry>     m_EndNodeGeometryTypeHandle;
            [ReadOnly] private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ParcelSearchTree;
            [ReadOnly] private ComponentLookup<PrefabRef>               m_PrefabRefComponentLookup;
            [ReadOnly] private ComponentLookup<EdgeGeometry>            m_EdgeGeometryComponentLookup;
            [ReadOnly] private ComponentLookup<StartNodeGeometry>       m_StartNodeGeometryComponentLookup;
            [ReadOnly] private ComponentLookup<EndNodeGeometry>         m_EndNodeGeometryComponentLookup;
            [ReadOnly] private ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] private ComponentLookup<Parcel>                  m_ParcelComponentLookup;
            [ReadOnly] private ComponentLookup<Transform>               m_TransformComponentLookup;
            private            NativeQueue<Entity>.ParallelWriter       m_ParcelEntitiesQueue;

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateEntitiesQueueJob"/> struct.
            /// </summary>
            /// <param name="parcelEntitiesQueue"></param>
            /// <param name="entityTypeHandle"></param>
            /// <param name="connectedParcelBufferTypeHandle"></param>
            /// <param name="deletedTypeHandle"></param>
            /// <param name="edgeGeometryTypeHandle"></param>
            /// <param name="startNodeGeometryTypeHandle"></param>
            /// <param name="endNodeGeometryTypeHandle"></param>
            /// <param name="parcelSearchTree"></param>
            /// <param name="prefabRefComponentLookup"></param>
            /// <param name="edgeGeometryComponentLookup"></param>
            /// <param name="startNodeGeometryComponentLookup"></param>
            /// <param name="endNodeGeometryComponentLookup"></param>
            /// <param name="parcelDataComponentLookup"></param>
            /// <param name="parcelComponentLookup"></param>
            /// <param name="transformComponentLookup"></param>
            public CreateEntitiesQueueJob(NativeQueue<Entity>.ParallelWriter parcelEntitiesQueue, EntityTypeHandle entityTypeHandle, BufferTypeHandle<ConnectedParcel> connectedParcelBufferTypeHandle, ComponentTypeHandle<Deleted> deletedTypeHandle, ComponentTypeHandle<EdgeGeometry> edgeGeometryTypeHandle, ComponentTypeHandle<StartNodeGeometry> startNodeGeometryTypeHandle, ComponentTypeHandle<EndNodeGeometry> endNodeGeometryTypeHandle, NativeQuadTree<Entity, QuadTreeBoundsXZ> parcelSearchTree, ComponentLookup<PrefabRef> prefabRefComponentLookup, ComponentLookup<EdgeGeometry> edgeGeometryComponentLookup, ComponentLookup<StartNodeGeometry> startNodeGeometryComponentLookup, ComponentLookup<EndNodeGeometry> endNodeGeometryComponentLookup, ComponentLookup<ParcelData> parcelDataComponentLookup, ComponentLookup<Parcel> parcelComponentLookup, ComponentLookup<Transform> transformComponentLookup) {
                m_ParcelEntitiesQueue = parcelEntitiesQueue;
                m_EntityTypeHandle = entityTypeHandle;
                m_ConnectedParcelBufferTypeHandle = connectedParcelBufferTypeHandle;
                m_DeletedTypeHandle = deletedTypeHandle;
                m_EdgeGeometryTypeHandle = edgeGeometryTypeHandle;
                m_StartNodeGeometryTypeHandle = startNodeGeometryTypeHandle;
                m_EndNodeGeometryTypeHandle = endNodeGeometryTypeHandle;
                m_ParcelSearchTree = parcelSearchTree;
                m_PrefabRefComponentLookup = prefabRefComponentLookup;
                m_EdgeGeometryComponentLookup = edgeGeometryComponentLookup;
                m_StartNodeGeometryComponentLookup = startNodeGeometryComponentLookup;
                m_EndNodeGeometryComponentLookup = endNodeGeometryComponentLookup;
                m_ParcelDataComponentLookup = parcelDataComponentLookup;
                m_ParcelComponentLookup = parcelComponentLookup;
                m_TransformComponentLookup = transformComponentLookup;
            }

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var connectedParcelBufferAccessor = chunk.GetBufferAccessor<ConnectedParcel>(ref m_ConnectedParcelBufferTypeHandle);

                if (connectedParcelBufferAccessor.Length != 0) {
#if !USE_BURST
                    PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] CreateEntitiesQueueJob() -- connectedParcelBufferAccessor {connectedParcelBufferAccessor.Length} entries. Deleted: {chunk.Has<Deleted>(ref m_DeletedTypeHandle)}");
#endif

                    // Handle edge delete
                    if (chunk.Has<Deleted>(ref m_DeletedTypeHandle)) {
                        for (var i = 0; i < connectedParcelBufferAccessor.Length; i++) {
                            var dynamicBuffer = connectedParcelBufferAccessor[i];
                            for (var j = 0; j < dynamicBuffer.Length; j++) {
                                var parcel = m_ParcelComponentLookup[dynamicBuffer[j].m_Parcel];
                                parcel.m_RoadEdge = Entity.Null;
                                m_ParcelComponentLookup[dynamicBuffer[j].m_Parcel] = parcel;
                                m_ParcelEntitiesQueue.Enqueue(dynamicBuffer[j].m_Parcel);
                            }
                        }

                        return;
                    }

                    // Handle edge update
                    var edgeGeoArray = chunk.GetNativeArray<EdgeGeometry>(ref m_EdgeGeometryTypeHandle);
                    var startNodeGeoArray = chunk.GetNativeArray<StartNodeGeometry>(ref m_StartNodeGeometryTypeHandle);
                    var endNodeGeoArray = chunk.GetNativeArray<EndNodeGeometry>(ref m_EndNodeGeometryTypeHandle);

                    // Todo don't run this when not needed (roads update a LOT)
                    for (var k = 0; k < edgeGeoArray.Length; k++) {
                        var edgeGeometry = edgeGeoArray[k];
                        var startNodeGeo = startNodeGeoArray[k].m_Geometry;
                        var endNodeGeo = endNodeGeoArray[k].m_Geometry;

                        var findParcelIterator = new FindParcelNextToRoadIterator(
                            bounds: MathUtils.Expand(edgeGeometry.m_Bounds | startNodeGeo.m_Bounds | endNodeGeo.m_Bounds, P_RoadConnectionSystem.MaxDistance),
                            edgeGeometry: edgeGeometry,
                            startGeometry: startNodeGeo,
                            endGeometry: endNodeGeo,
                            minDistance: P_RoadConnectionSystem.MaxDistance,
                            parcelEntitiesQueue: m_ParcelEntitiesQueue,
                            prefabRefComponentLookup: m_PrefabRefComponentLookup,
                            edgeGeometryComponentLookup: m_EdgeGeometryComponentLookup,
                            startNodeGeometryComponentLookup: m_StartNodeGeometryComponentLookup,
                            endNodeGeometryComponentLookup: m_EndNodeGeometryComponentLookup,
                            parcelDataComponentLookup: m_ParcelDataComponentLookup,
                            parcelComponentLookup: m_ParcelComponentLookup,
                            transformComponentLookup: m_TransformComponentLookup
                        );

                        m_ParcelSearchTree.Iterate<FindParcelNextToRoadIterator>(ref findParcelIterator, 0);
                    }

                    return;
                }

                foreach (var entity in chunk.GetNativeArray(m_EntityTypeHandle)) {
                    m_ParcelEntitiesQueue.Enqueue(entity);
                }
                
            }

           private struct FindParcelNextToRoadIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                private NativeQueue<Entity>.ParallelWriter m_EntitiesQueue;
                private Bounds3                            m_Bounds;
                private EdgeGeometry                       m_EdgeGeometry;
                private EdgeNodeGeometry                   m_StartGeometry;
                private EdgeNodeGeometry                   m_EndGeometry;
                private ComponentLookup<PrefabRef>         m_PrefabRefComponentLookup;
                private ComponentLookup<EdgeGeometry>      m_EdgeGeometryComponentLookup;
                private ComponentLookup<StartNodeGeometry> m_StartNodeGeometryComponentLookup;
                private ComponentLookup<EndNodeGeometry>   m_EndNodeGeometryComponentLookup;
                private ComponentLookup<Parcel>            m_ParcelComponentLookup;
                private ComponentLookup<ParcelData>        m_ParcelDataComponentLookup;
                private ComponentLookup<Transform>         m_TransformComponentLookup;
                private float                              m_MinDistance;

                public FindParcelNextToRoadIterator(Bounds3 bounds, EdgeGeometry edgeGeometry, EdgeNodeGeometry startGeometry, EdgeNodeGeometry endGeometry, ComponentLookup<PrefabRef> prefabRefComponentLookup, ComponentLookup<EdgeGeometry> edgeGeometryComponentLookup, ComponentLookup<StartNodeGeometry> startNodeGeometryComponentLookup, ComponentLookup<EndNodeGeometry> endNodeGeometryComponentLookup, ComponentLookup<Parcel> parcelComponentLookup, ComponentLookup<ParcelData> parcelDataComponentLookup, ComponentLookup<Transform> transformComponentLookup, float minDistance, NativeQueue<Entity>.ParallelWriter parcelEntitiesQueue) {
                    m_Bounds = bounds;
                    m_EdgeGeometry = edgeGeometry;
                    m_StartGeometry = startGeometry;
                    m_EndGeometry = endGeometry;
                    m_PrefabRefComponentLookup = prefabRefComponentLookup;
                    m_EdgeGeometryComponentLookup = edgeGeometryComponentLookup;
                    m_StartNodeGeometryComponentLookup = startNodeGeometryComponentLookup;
                    m_EndNodeGeometryComponentLookup = endNodeGeometryComponentLookup;
                    m_ParcelComponentLookup = parcelComponentLookup;
                    m_ParcelDataComponentLookup = parcelDataComponentLookup;
                    m_TransformComponentLookup = transformComponentLookup;
                    m_MinDistance = minDistance;
                    m_EntitiesQueue = parcelEntitiesQueue;
                }

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity parcelEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    if (!m_ParcelComponentLookup.HasComponent(parcelEntity)) {
                        return;
                    }

                    var parcelPrefabRef = m_PrefabRefComponentLookup[parcelEntity];
                    var parcelData = m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                    var parcelTransform = m_TransformComponentLookup[parcelEntity];

                    // The "front position" is the point where a parcel is expected to connect to a road.
                    var size          = ParcelUtils.GetParcelSize(parcelData);
                    var frontNode     = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.Front) * size;
                    var frontPosition = ParcelUtils.GetWorldPosition(parcelTransform, frontNode);

                    // Check if this parcel is within the bounds of the road
                    if (!MathUtils.Intersect(m_Bounds.xz, frontPosition.xz)) {
                        return;
                    }

                    // Calculate the distance between parcel and road
                    var distance = m_MinDistance;
                    P_RoadConnectionSystem.CheckDistance(m_EdgeGeometry, m_StartGeometry, m_EndGeometry, frontPosition, true, ref distance);

                    // If valid...
                    if (distance > m_MinDistance) {
                        return;
                    }

                    var parcel = m_ParcelComponentLookup[parcelEntity];

                    // Check if we need to compare to an already connected road aka "Are we closer?"
                    if (parcel.m_RoadEdge != Entity.Null) {
                        var curEdgeGeo      = m_EdgeGeometryComponentLookup[parcel.m_RoadEdge];
                        var curStartNodeGeo = m_StartNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                        var curEndNodeGeo   = m_EndNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                        var curDistance     = m_MinDistance;
                        P_RoadConnectionSystem.CheckDistance(curEdgeGeo, curStartNodeGeo, curEndNodeGeo, frontPosition, true, ref curDistance);

                        // If new road is closer, add parcel to queue
                        if (distance < curDistance) {
                            m_EntitiesQueue.Enqueue(parcelEntity);
                        }
                    } else {
                        // New road just dropped! Add parcel to queue
                        m_EntitiesQueue.Enqueue(parcelEntity);
                    }
                }
            }
        }
    }
}
