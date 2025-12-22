// <copyright file="P_RoadConnectionSystem.CreateEntitiesQueueJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;
    using Utils;

    #endregion

    public partial class P_RoadConnectionSystem : PlatterGameSystemBase {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct CreateEntitiesQueueJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                         m_EntityTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ConnectedParcel>        m_ConnectedParcelBufferTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Temp>                m_TempTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Deleted>             m_DeletedTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EdgeGeometry>        m_EdgeGeometryTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<StartNodeGeometry>   m_StartNodeGeometryTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EndNodeGeometry>     m_EndNodeGeometryTypeHandle;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ParcelSearchTree;
            [ReadOnly] public required ComponentLookup<PrefabRef>               m_PrefabRefComponentLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>            m_EdgeGeometryComponentLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry>       m_StartNodeGeometryComponentLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry>         m_EndNodeGeometryComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] public required ComponentLookup<Parcel>                  m_ParcelComponentLookup;
            [ReadOnly] public required ComponentLookup<Transform>               m_TransformComponentLookup;
            public required            NativeQueue<Entity>.ParallelWriter       m_ParcelEntitiesQueue;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var connectedParcelBufferAccessor = chunk.GetBufferAccessor(ref m_ConnectedParcelBufferTypeHandle);
                var entityArray                   = chunk.GetNativeArray(m_EntityTypeHandle);
                var enqueuedCount                 = 0;

                // Handle Roads first
                if (connectedParcelBufferAccessor.Length != 0) {
                    // Handle edge delete
                    if (chunk.Has(ref m_DeletedTypeHandle)) {

                        for (var i = 0; i < connectedParcelBufferAccessor.Length; i++) {
                            var connectedParcelsBuffer = connectedParcelBufferAccessor[i];
                            enqueuedCount += connectedParcelsBuffer.Length;
                            for (var j = 0; j < connectedParcelsBuffer.Length; j++) {
                                // Get parcel
                                var parcel = m_ParcelComponentLookup[connectedParcelsBuffer[j].m_Parcel];

                                // Clear road connection
                                parcel.m_RoadEdge                                  = Entity.Null;
                                m_ParcelComponentLookup[connectedParcelsBuffer[j].m_Parcel] = parcel;

                                // Enqueue parcel for re-evaluation
                                m_ParcelEntitiesQueue.Enqueue(connectedParcelsBuffer[j].m_Parcel);
                            }
                        }

                        BurstLogger.Debug("RCS", $"Processed {connectedParcelBufferAccessor.Length} deleted edges. Enqueued {enqueuedCount} parcels.");

                        return;
                    }

                    // Handle edge update
                    var edgeGeoArray      = chunk.GetNativeArray(ref m_EdgeGeometryTypeHandle);
                    var startNodeGeoArray = chunk.GetNativeArray(ref m_StartNodeGeometryTypeHandle);
                    var endNodeGeoArray   = chunk.GetNativeArray(ref m_EndNodeGeometryTypeHandle);

                    // Todo: don't run this when not needed (roads update a LOT)
                    for (var k = 0; k < edgeGeoArray.Length; k++) {
                        var entity         = entityArray[k];
                        var edgeGeometry   = edgeGeoArray[k];
                        var startNodeGeo   = startNodeGeoArray[k].m_Geometry;
                        var endNodeGeo     = endNodeGeoArray[k].m_Geometry;

                        var findParcelIterator = new FindParcelNextToRoadIterator() 
                        {
                            m_EntitiesQueue = m_ParcelEntitiesQueue,
                            m_Road = entity,
                            m_Bounds = MathUtils.Expand(edgeGeometry.m_Bounds | startNodeGeo.m_Bounds | endNodeGeo.m_Bounds, MaxDistanceFront),
                            m_EdgeGeometry = edgeGeometry,
                            m_StartGeometry = startNodeGeo,
                            m_EndGeometry = endNodeGeo,
                            m_PrefabRefComponentLookup = m_PrefabRefComponentLookup,
                            m_EdgeGeometryComponentLookup = m_EdgeGeometryComponentLookup,
                            m_StartNodeGeometryComponentLookup = m_StartNodeGeometryComponentLookup,
                            m_EndNodeGeometryComponentLookup = m_EndNodeGeometryComponentLookup,
                            m_ParcelComponentLookup = m_ParcelComponentLookup,
                            m_ParcelDataComponentLookup = m_ParcelDataComponentLookup,
                            m_TransformComponentLookup = m_TransformComponentLookup,
                            m_MinDistance = MaxDistanceFront ,
                        };

                        m_ParcelSearchTree.Iterate(ref findParcelIterator);
                    }

                    return;
                }

                // Otherwise it's a parcel - enqueue it.
                var tempArray     = chunk.GetNativeArray(ref m_TempTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];

                    // Ignore selected temp entities
                    if (chunk.Has(ref m_TempTypeHandle)) {
                        var temp        = tempArray[i];
                        if ((temp.m_Flags & (TempFlags.Create | TempFlags.Modify)) == 0) {
                            // No flags set, exit.
                            continue;
                        }
                    } 

                    m_ParcelEntitiesQueue.Enqueue(entity);
                }

                BurstLogger.Debug("RCS", $"Enqueued {enqueuedCount} parcels.");
            }

            private struct FindParcelNextToRoadIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public required NativeQueue<Entity>.ParallelWriter m_EntitiesQueue;
                public required Entity m_Road;
                public required Bounds3 m_Bounds;
                public required EdgeGeometry                       m_EdgeGeometry;
                public required EdgeNodeGeometry                   m_StartGeometry;
                public required EdgeNodeGeometry                   m_EndGeometry;
                public required ComponentLookup<PrefabRef>         m_PrefabRefComponentLookup;
                public required ComponentLookup<EdgeGeometry>      m_EdgeGeometryComponentLookup;
                public required ComponentLookup<StartNodeGeometry> m_StartNodeGeometryComponentLookup;
                public required ComponentLookup<EndNodeGeometry>   m_EndNodeGeometryComponentLookup;
                public required ComponentLookup<Parcel>            m_ParcelComponentLookup;
                public required ComponentLookup<ParcelData>        m_ParcelDataComponentLookup;
                public required ComponentLookup<Transform>         m_TransformComponentLookup;
                public required float                              m_MinDistance;

                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz); }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity parcelEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    if (!m_ParcelComponentLookup.TryGetComponent(parcelEntity, out var parcel)) {
                        return;
                    }

                    if (parcel.m_RoadEdge == m_Road) {
                        // Already connected to this road, skip.
                        return;
                    }

                    var parcelPrefabRef = m_PrefabRefComponentLookup[parcelEntity];
                    var parcelData      = m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                    var parcelTransform = m_TransformComponentLookup[parcelEntity];
                    var enqueuedCount   = 0;

                    // The "front position" is the point where a parcel is expected to connect to a road.
                    var size          = ParcelGeometryUtils.GetParcelSize(parcelData.m_LotSize);
                    var frontNode     = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.FrontAccess) * size;
                    var frontPosition = ParcelGeometryUtils.GetWorldPosition(parcelTransform, frontNode);

                    // Check if this parcel is within the bounds of the road
                    if (!MathUtils.Intersect(m_Bounds.xz, frontPosition.xz)) {
                        return;
                    }

                    // Calculate the distance between parcel and road
                    var distance = m_MinDistance;
                    CheckDistance(m_EdgeGeometry, m_StartGeometry, m_EndGeometry, frontPosition, true, ref distance);

                    // If valid...
                    if (distance > m_MinDistance) {
                        return;
                    }

                    // Check if we need to compare to an already connected road aka "Are we closer?"
                    if (parcel.m_RoadEdge != Entity.Null && 
                        // Not sure why it needs these additional checks,
                        // but some crashes are happening in very rare circumstances with null object ref
                        m_EdgeGeometryComponentLookup.HasComponent(parcel.m_RoadEdge) &&
                        m_StartNodeGeometryComponentLookup.HasComponent(parcel.m_RoadEdge) &&
                        m_EndNodeGeometryComponentLookup.HasComponent(parcel.m_RoadEdge)) {
                        var curEdgeGeo = m_EdgeGeometryComponentLookup[parcel.m_RoadEdge];
                        var curStartNodeGeo = m_StartNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                        var curEndNodeGeo   = m_EndNodeGeometryComponentLookup[parcel.m_RoadEdge].m_Geometry;
                        var curDistance= m_MinDistance;

                        CheckDistance(curEdgeGeo, curStartNodeGeo, curEndNodeGeo, frontPosition, false, ref curDistance);

                        // If new road is closer, add parcel to queue
                        if (distance < curDistance) {
                            m_EntitiesQueue.Enqueue(parcelEntity);
                            enqueuedCount++;
                        }
                    } else {
                        // New road just dropped! Add parcel to queue
                        m_EntitiesQueue.Enqueue(parcelEntity);
                        enqueuedCount++;
                    }

                    BurstLogger.Debug("RCS", $"Analyzed updated edge and enqueued {enqueuedCount} parcels.");
                }
            }
        }
    }
}