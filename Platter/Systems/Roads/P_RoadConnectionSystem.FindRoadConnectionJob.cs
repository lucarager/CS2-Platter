// <copyright file="P_RoadConnectionSystem.FindRoadConnectionJob.cs" company="Luca Rager">
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
    using Game.Zones;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;

    #endregion

    public partial class P_RoadConnectionSystem : PlatterGameSystemBase {
        /// <summary>
        /// Find the best and eligible road for a given parcel.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct FindRoadConnectionJob : IJobParallelForDefer {
            [ReadOnly] public required ComponentLookup<Deleted>                 m_DeletedDataComponentLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>               m_PrefabRefComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] public required ComponentLookup<Transform>               m_TransformComponentLookup;
            [ReadOnly] public required BufferLookup<ConnectedParcel>            m_ConnectedParcelBufferLookup;
            [ReadOnly] public required ComponentLookup<Curve>                   m_CurveDataComponentLookup;
            [ReadOnly] public required ComponentLookup<Composition>             m_CompositionDataComponentLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>            m_EdgeGeometryDataComponentLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry>       m_StartNodeGeometryDataComponentLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry>         m_EndNodeGeometryDataComponentLookup;
            [ReadOnly] public required ComponentLookup<NetCompositionData>      m_PrefabNetCompositionDataComponentLookup;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly] public required NativeList<ArchetypeChunk>               m_UpdatedNetChunks;
            [ReadOnly] public required EntityTypeHandle                         m_EntityTypeHandle;
            [ReadOnly] public required BufferLookup<SubBlock>                   m_SubBlockBufferLookup;
            public required NativeArray<RCData> m_ParcelEntitiesList;

            /// <inheritdoc/>
            public void Execute(int index) {
                // Retrieve the data
                var currentEntityData = m_ParcelEntitiesList[index];

                // If entity has DELETED component
                // mark its entry in the list as deleted and exit early
                if (m_DeletedDataComponentLookup.HasComponent(currentEntityData.m_Parcel)) {
                    currentEntityData.m_Deleted = true;
                    m_ParcelEntitiesList[index] = currentEntityData;
                    return;
                }

                var parcelPrefabRef = m_PrefabRefComponentLookup[currentEntityData.m_Parcel];
                var parcelData      = m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                var parcelTransform = m_TransformComponentLookup[currentEntityData.m_Parcel];
                var parcelSize= ParcelGeometryUtils.GetParcelSize(parcelData.m_LotSize);

                // Find road for front access
                FindBestRoadForAccessNode(
                    ParcelGeometryUtils.ParcelNode.FrontAccess, parcelSize, parcelTransform, MaxDistanceFront,
                    out currentEntityData.m_FrontRoad, out currentEntityData.m_FrontPos, out currentEntityData.m_FrontCurvePos);

                // Find road for left access
                // Todo check how cells check for road access to get more reliable results here
                FindBestRoadForAccessNode(
                    ParcelGeometryUtils.ParcelNode.LeftAccess, parcelSize, parcelTransform, MaxDistanceSides,
                    out currentEntityData.m_LeftRoad, out currentEntityData.m_LeftPos, out currentEntityData.m_LeftCurvePos);

                // Find road for right access
                FindBestRoadForAccessNode(
                    ParcelGeometryUtils.ParcelNode.RightAccess, parcelSize, parcelTransform, MaxDistanceSides,
                    out currentEntityData.m_RightRoad, out currentEntityData.m_RightPos, out currentEntityData.m_RightCurvePos);

                // Update the data in the list with what we found
                m_ParcelEntitiesList[index] = currentEntityData;
            }

            private void FindBestRoadForAccessNode(
                ParcelGeometryUtils.ParcelNode accessNode, float3 parcelSize, Transform parcelTransform, float maxDistance,
                out Entity road, out float3 position, out float curvePos) {

                var nodeOffset     = ParcelGeometryUtils.NodeMult(accessNode) * parcelSize;
                var accessPosition = ParcelGeometryUtils.GetWorldPosition(parcelTransform, nodeOffset);

                var iterator = new FindRoadConnectionIterator(
                    bestCurvePos: 0f,
                    bestRoad: Entity.Null,
                    canBeOnRoad: true,
                    subBlockBufferLookup: m_SubBlockBufferLookup,
                    connectedParcelsBufferLookup: m_ConnectedParcelBufferLookup,
                    curveDataComponentLookup: m_CurveDataComponentLookup,
                    compositionDataComponentLookup: m_CompositionDataComponentLookup,
                    edgeGeometryDataComponentLookup: m_EdgeGeometryDataComponentLookup,
                    startNodeGeometryDataComponentLookup: m_StartNodeGeometryDataComponentLookup,
                    endNodeGeometryDataComponentLookup: m_EndNodeGeometryDataComponentLookup,
                    prefabNetCompositionDataComponentLookup: m_PrefabNetCompositionDataComponentLookup,
                    deletedDataComponentLookup: m_DeletedDataComponentLookup,
                    bounds: new Bounds3(accessPosition - maxDistance, accessPosition + maxDistance),
                    minDistance: maxDistance,
                    frontPosition: accessPosition
                );

                // Find suitable roads via quad tree
                m_NetSearchTree.Iterate(ref iterator);

                // Also check updated net chunks
                for (var k = 0; k < m_UpdatedNetChunks.Length; k++) {
                    var netArray = m_UpdatedNetChunks[k].GetNativeArray(m_EntityTypeHandle);
                    foreach (var net in netArray) iterator.CheckEdge(net);
                }

                road     = iterator.BestRoad;
                position = iterator.FrontPosition;
                curvePos = iterator.BestCurvePos;
            }

            public struct FindRoadConnectionIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public  float                               BestCurvePos;
                public  Entity                              BestRoad;
                public  float3                              FrontPosition;
                private Bounds3                             m_Bounds;
                private float                               m_MinDistance;
                private bool                                m_CanBeOnRoad;
                private BufferLookup<ConnectedParcel>       m_ConnectedParcelsBufferLookup;
                private BufferLookup<SubBlock>              m_SubBlockBufferLookup;
                private ComponentLookup<Curve>              m_CurveDataComponentLookup;
                private ComponentLookup<Composition>        m_CompositionDataComponentLookup;
                private ComponentLookup<EdgeGeometry>       m_EdgeGeometryDataComponentLookup;
                private ComponentLookup<StartNodeGeometry>  m_StartNodeGeometryDataComponentLookup;
                private ComponentLookup<EndNodeGeometry>    m_EndNodeGeometryDataComponentLookup;
                private ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;
                private ComponentLookup<Deleted>            m_DeletedDataComponentLookup;

                public FindRoadConnectionIterator(Bounds3 bounds, float minDistance, float bestCurvePos, Entity bestRoad, float3 frontPosition,
                                                  bool canBeOnRoad, BufferLookup<ConnectedParcel> connectedParcelsBufferLookup, BufferLookup<SubBlock> subBlockBufferLookup,
                                                  ComponentLookup<Curve> curveDataComponentLookup, ComponentLookup<Composition> compositionDataComponentLookup,
                                                  ComponentLookup<EdgeGeometry> edgeGeometryDataComponentLookup,
                                                  ComponentLookup<StartNodeGeometry> startNodeGeometryDataComponentLookup,
                                                  ComponentLookup<EndNodeGeometry> endNodeGeometryDataComponentLookup,
                                                  ComponentLookup<NetCompositionData> prefabNetCompositionDataComponentLookup,
                                                  ComponentLookup<Deleted> deletedDataComponentLookup) {
                    m_Bounds                                  = bounds;
                    m_MinDistance                             = minDistance;
                    BestCurvePos                              = bestCurvePos;
                    BestRoad                                  = bestRoad;
                    FrontPosition                             = frontPosition;
                    m_CanBeOnRoad                             = canBeOnRoad;
                    m_ConnectedParcelsBufferLookup            = connectedParcelsBufferLookup;
                    m_SubBlockBufferLookup                    = subBlockBufferLookup;
                    m_CurveDataComponentLookup                = curveDataComponentLookup;
                    m_CompositionDataComponentLookup          = compositionDataComponentLookup;
                    m_EdgeGeometryDataComponentLookup         = edgeGeometryDataComponentLookup;
                    m_StartNodeGeometryDataComponentLookup    = startNodeGeometryDataComponentLookup;
                    m_EndNodeGeometryDataComponentLookup      = endNodeGeometryDataComponentLookup;
                    m_PrefabNetCompositionDataComponentLookup = prefabNetCompositionDataComponentLookup;
                    m_DeletedDataComponentLookup              = deletedDataComponentLookup;
                }

                /// <inheritdoc/>
                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz); }

                /// <inheritdoc/>
                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    CheckEdge(edgeEntity);
                }

                public void CheckEdge(Entity edgeEntity) {
                    // Exit early if the edge has been deleted
                    if (m_DeletedDataComponentLookup.HasComponent(edgeEntity)) {
                        return;
                    }

                    // Exit early if the edge doesn't have a "ConnectedParcels" buffer
                    if (!m_ConnectedParcelsBufferLookup.HasBuffer(edgeEntity)) {
                        return;
                    }

                    // Exit early if the edge doesn't have a "SubBlock" buffer
                    if (!m_SubBlockBufferLookup.HasBuffer(edgeEntity)) {
                        return;
                    }

                    // Retrieve composition data
                    // Exit early if the road is elevated or a tunnel.
                    var netCompositionData = default(NetCompositionData);
                    if (
                        m_CompositionDataComponentLookup.TryGetComponent(edgeEntity, out var composition)                     &&
                        m_PrefabNetCompositionDataComponentLookup.TryGetComponent(composition.m_Edge, out netCompositionData) &&
                        (netCompositionData.m_Flags.m_General & (CompositionFlags.General.Elevated | CompositionFlags.General.Tunnel)) != 0U) {
                        return;
                    }

                    // Check whether the entity can be connected to the road based on a maximum distance.
                    var edgeGeo         = m_EdgeGeometryDataComponentLookup[edgeEntity];
                    var startNodeGeo    = m_StartNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    var endNodeGeo      = m_EndNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    var distanceToFront = m_MinDistance;
                    CheckDistance(edgeGeo, startNodeGeo, endNodeGeo, FrontPosition, m_CanBeOnRoad, ref distanceToFront);

                    // If the distanceToFront is less than the max
                    if (!(distanceToFront < m_MinDistance)) {
                        return;
                    }

                    // Retrieves the SelectedCurve data for the road edge, which represents the road's shape as a Bezier curve.
                    var curve = m_CurveDataComponentLookup[edgeEntity];

                    // Finds the nearest point on the curve to the entity's front position.
                    MathUtils.Distance(curve.m_Bezier.xz, FrontPosition.xz, out var nearestPointToFront);
                    var positionOfNearestPointToBuildingFront = MathUtils.Position(curve.m_Bezier, nearestPointToFront);

                    // Compute the tangent vector and determine the side of the curve (right or left)
                    var tangent     = MathUtils.Tangent(curve.m_Bezier, nearestPointToFront).xz;
                    var toFront     = FrontPosition.xz - positionOfNearestPointToBuildingFront.xz;
                    var isRightSide = math.dot(MathUtils.Right(tangent), toFront) >= 0f;

                    // Determine the relevant flags based on the side
                    var relevantFlags = isRightSide ? netCompositionData.m_Flags.m_Right : netCompositionData.m_Flags.m_Left;

                    // Check if the edge is raised or lowered
                    var isRaisedOrLowered = (relevantFlags & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) != 0U;

                    if (isRaisedOrLowered) {
                        return;
                    }

                    // If we got here, we found a valid best road entity, so store it
                    m_Bounds      = new Bounds3(FrontPosition - distanceToFront, FrontPosition + distanceToFront);
                    m_MinDistance = distanceToFront;
                    BestCurvePos  = nearestPointToFront;
                    BestRoad      = edgeEntity;
                }
            }
        }
    }
}