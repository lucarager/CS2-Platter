// <copyright file="P_RoadConnectionSystem.FindRoadConnectionJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

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
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find the best and eligible road for a given parcel.
        /// </summary>
#if USE_BURST
    [BurstCompile]
#endif
        public struct FindRoadConnectionJob : IJobParallelForDefer {
            private NativeArray<UpdateData> m_ParcelEntitiesList;
            [ReadOnly] private ComponentLookup<Deleted> m_DeletedDataComponentLookup;
            [ReadOnly] private ComponentLookup<PrefabRef> m_PrefabRefComponentLookup;
            [ReadOnly] private ComponentLookup<ParcelData> m_ParcelDataComponentLookup;
            [ReadOnly] private ComponentLookup<Transform> m_TransformComponentLookup;
            [ReadOnly] private BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;
            [ReadOnly] private ComponentLookup<Curve> m_CurveDataComponentLookup;
            [ReadOnly] private ComponentLookup<Composition> m_CompositionDataComponentLookup;
            [ReadOnly] private ComponentLookup<EdgeGeometry> m_EdgeGeometryDataComponentLookup;
            [ReadOnly] private ComponentLookup<StartNodeGeometry> m_StartNodeGeometryDataComponentLookup;
            [ReadOnly] private ComponentLookup<EndNodeGeometry> m_EndNodeGeometryDataComponentLookup;
            [ReadOnly] private ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;
            [ReadOnly] private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly] private NativeList<ArchetypeChunk> m_UpdatedNetChunks;
            [ReadOnly] private EntityTypeHandle m_EntityTypeHandle;

            /// <summary>
            /// Initializes a new instance of the <see cref="FindRoadConnectionJob"/> struct.
            /// </summary>
            /// <param name="parcelEntitiesList"></param>
            /// <param name="deletedDataComponentLookup"></param>
            /// <param name="prefabRefComponentLookup"></param>
            /// <param name="parcelDataComponentLookup"></param>
            /// <param name="transformComponentLookup"></param>
            /// <param name="connectedParcelsBufferLookup"></param>
            /// <param name="curveDataComponentLookup"></param>
            /// <param name="compositionDataComponentLookup"></param>
            /// <param name="edgeGeometryDataComponentLookup"></param>
            /// <param name="startNodeGeometryDataComponentLookup"></param>
            /// <param name="endNodeGeometryDataComponentLookup"></param>
            /// <param name="prefabNetCompositionDataComponentLookup"></param>
            /// <param name="netSearchTree"></param>
            /// <param name="updatedNetChunks"></param>
            /// <param name="entityTypeHandle"></param>
            public FindRoadConnectionJob(NativeArray<UpdateData> parcelEntitiesList, ComponentLookup<Deleted> deletedDataComponentLookup, ComponentLookup<PrefabRef> prefabRefComponentLookup, ComponentLookup<ParcelData> parcelDataComponentLookup, ComponentLookup<Transform> transformComponentLookup, BufferLookup<ConnectedParcel> connectedParcelsBufferLookup, ComponentLookup<Curve> curveDataComponentLookup, ComponentLookup<Composition> compositionDataComponentLookup, ComponentLookup<EdgeGeometry> edgeGeometryDataComponentLookup, ComponentLookup<StartNodeGeometry> startNodeGeometryDataComponentLookup, ComponentLookup<EndNodeGeometry> endNodeGeometryDataComponentLookup, ComponentLookup<NetCompositionData> prefabNetCompositionDataComponentLookup, NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree, NativeList<ArchetypeChunk> updatedNetChunks, EntityTypeHandle entityTypeHandle) {
                m_ParcelEntitiesList = parcelEntitiesList;
                m_DeletedDataComponentLookup = deletedDataComponentLookup;
                m_PrefabRefComponentLookup = prefabRefComponentLookup;
                m_ParcelDataComponentLookup = parcelDataComponentLookup;
                m_TransformComponentLookup = transformComponentLookup;
                m_ConnectedParcelsBufferLookup = connectedParcelsBufferLookup;
                m_CurveDataComponentLookup = curveDataComponentLookup;
                m_CompositionDataComponentLookup = compositionDataComponentLookup;
                m_EdgeGeometryDataComponentLookup = edgeGeometryDataComponentLookup;
                m_StartNodeGeometryDataComponentLookup = startNodeGeometryDataComponentLookup;
                m_EndNodeGeometryDataComponentLookup = endNodeGeometryDataComponentLookup;
                m_PrefabNetCompositionDataComponentLookup = prefabNetCompositionDataComponentLookup;
                m_NetSearchTree = netSearchTree;
                m_UpdatedNetChunks = updatedNetChunks;
                m_EntityTypeHandle = entityTypeHandle;
            }

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
                var parcelData = m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                var parcelTransform = m_TransformComponentLookup[currentEntityData.m_Parcel];

                // The "front position" is the point where a parcel is expected to connect to a road.
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);
                var frontPosition = ParcelUtils.GetWorldPosition(parcelTransform, parcelGeo.FrontNode);

                // Initializes a FindRoadConnectionIterator, used to iterate through potential road connections.
                var findRoadConnectionIterator = new FindRoadConnectionIterator(
                    bestCurvePos: 0f,
                    bestRoad: Entity.Null,
                    canBeOnRoad: true,
                    connectedParcelsBufferLookup: m_ConnectedParcelsBufferLookup,
                    curveDataComponentLookup: m_CurveDataComponentLookup,
                    compositionDataComponentLookup: m_CompositionDataComponentLookup,
                    edgeGeometryDataComponentLookup: m_EdgeGeometryDataComponentLookup,
                    startNodeGeometryDataComponentLookup: m_StartNodeGeometryDataComponentLookup,
                    endNodeGeometryDataComponentLookup: m_EndNodeGeometryDataComponentLookup,
                    prefabNetCompositionDataComponentLookup: m_PrefabNetCompositionDataComponentLookup,
                    deletedDataComponentLookup: m_DeletedDataComponentLookup,
                    bounds: new Bounds3(
                        frontPosition - P_RoadConnectionSystem.MaxDistance,
                        frontPosition + P_RoadConnectionSystem.MaxDistance
                    ),
                    minDistance: P_RoadConnectionSystem.MaxDistance,
                    frontPosition: frontPosition
                );

                // Find suitable roads, iterate over roads and check which is best
                m_NetSearchTree.Iterate<FindRoadConnectionIterator>(ref findRoadConnectionIterator, 0);

                for (var k = 0; k < m_UpdatedNetChunks.Length; k++) {
                    var netArray = m_UpdatedNetChunks[k].GetNativeArray(m_EntityTypeHandle);
                    for (var l = 0; l < netArray.Length; l++) {
                        findRoadConnectionIterator.CheckEdge(netArray[l]);
                    }
                }

                // Update our BuildingRoadUpdateData struct with the new info
                currentEntityData.m_NewRoad = findRoadConnectionIterator.BestRoad;
                currentEntityData.m_FrontPos = findRoadConnectionIterator.FrontPosition;
                currentEntityData.m_CurvePos = findRoadConnectionIterator.BestCurvePos;

                // Update the data in the list with what we found
                m_ParcelEntitiesList[index] = currentEntityData;
            }

            public struct FindRoadConnectionIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public  float                               BestCurvePos;
                public  Entity                              BestRoad;
                public  float3                              FrontPosition;
                private Bounds3                             m_Bounds;
                private float                               m_MinDistance;
                private bool                                m_CanBeOnRoad;
                private BufferLookup<ConnectedParcel>       m_ConnectedParcelsBufferLookup;
                private ComponentLookup<Curve>              m_CurveDataComponentLookup;
                private ComponentLookup<Composition>        m_CompositionDataComponentLookup;
                private ComponentLookup<EdgeGeometry>       m_EdgeGeometryDataComponentLookup;
                private ComponentLookup<StartNodeGeometry>  m_StartNodeGeometryDataComponentLookup;
                private ComponentLookup<EndNodeGeometry>    m_EndNodeGeometryDataComponentLookup;
                private ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;
                private ComponentLookup<Deleted>            m_DeletedDataComponentLookup;

                public FindRoadConnectionIterator(Bounds3 bounds, float minDistance, float bestCurvePos, Entity bestRoad, float3 frontPosition, bool canBeOnRoad, BufferLookup<ConnectedParcel> connectedParcelsBufferLookup, ComponentLookup<Curve> curveDataComponentLookup, ComponentLookup<Composition> compositionDataComponentLookup, ComponentLookup<EdgeGeometry> edgeGeometryDataComponentLookup, ComponentLookup<StartNodeGeometry> startNodeGeometryDataComponentLookup, ComponentLookup<EndNodeGeometry> endNodeGeometryDataComponentLookup, ComponentLookup<NetCompositionData> prefabNetCompositionDataComponentLookup, ComponentLookup<Deleted> deletedDataComponentLookup) {
                    m_Bounds = bounds;
                    m_MinDistance = minDistance;
                    BestCurvePos = bestCurvePos;
                    BestRoad = bestRoad;
                    FrontPosition = frontPosition;
                    m_CanBeOnRoad = canBeOnRoad;
                    m_ConnectedParcelsBufferLookup = connectedParcelsBufferLookup;
                    m_CurveDataComponentLookup = curveDataComponentLookup;
                    m_CompositionDataComponentLookup = compositionDataComponentLookup;
                    m_EdgeGeometryDataComponentLookup = edgeGeometryDataComponentLookup;
                    m_StartNodeGeometryDataComponentLookup = startNodeGeometryDataComponentLookup;
                    m_EndNodeGeometryDataComponentLookup = endNodeGeometryDataComponentLookup;
                    m_PrefabNetCompositionDataComponentLookup = prefabNetCompositionDataComponentLookup;
                    m_DeletedDataComponentLookup = deletedDataComponentLookup;
                }

                /// <inheritdoc/>
                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
                }

                /// <inheritdoc/>
                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
                        return;
                    }

                    if (m_DeletedDataComponentLookup.HasComponent(edgeEntity)) {
                        return;
                    }

                    CheckEdge(edgeEntity);
                }

                /// <param name="edgeEntity">Todo.</param>
                public void CheckEdge(Entity edgeEntity) {
                    // Exit early if the edge doesn't have a "Connected Parcels" buffer
                    if (!m_ConnectedParcelsBufferLookup.HasBuffer(edgeEntity)) {
                        return;
                    }

                    // Retrieve composition data
                    // Exit early if the road is elevated or a tunnel.
                    NetCompositionData netCompositionData = default;
                    if (
                        m_CompositionDataComponentLookup.TryGetComponent(edgeEntity, out var composition) &&
                        m_PrefabNetCompositionDataComponentLookup.TryGetComponent(composition.m_Edge, out netCompositionData) &&
                        (netCompositionData.m_Flags.m_General & (CompositionFlags.General.Elevated | CompositionFlags.General.Tunnel)) != 0U) {
                        return;
                    }

                    // Check whether the entity can be connected to the road based on a maximum distance
                    // Calls RoadConnectionSystem.CheckDistance, which likely checks the distance from the entity to a road and updates the distanceToRoad if necessary.
                    var edgeGeo = m_EdgeGeometryDataComponentLookup[edgeEntity];
                    var startNodeGeo = m_StartNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    var endNodeGeo = m_EndNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    var distanceToFront = m_MinDistance;
                    P_RoadConnectionSystem.CheckDistance(edgeGeo, startNodeGeo, endNodeGeo, FrontPosition, m_CanBeOnRoad, ref distanceToFront);

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
