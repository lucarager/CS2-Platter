// <copyright file="RoadConnectionSystem.FindRoadConnectionJob.cs" company="Luca Rager">
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
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
#if USE_BURST
    [BurstCompile]
#endif
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find the best and eligible road for a given parcel.
        /// </summary>
        public struct FindRoadConnectionJob : IJobParallelForDefer {
            /// <summary>
            /// todo.
            /// </summary>
            public NativeArray<RoadConnectionSystem.ConnectionUpdateDataJob> m_ParcelEntitiesList;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Deleted> m_DeletedDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Transform> m_TransformComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Curve> m_CurveDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Composition> m_CompositionDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<EdgeGeometry> m_EdgeGeometryDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public NativeList<ArchetypeChunk> m_UpdatedNetChunks;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;

            /// <inheritdoc/>
            public void Execute(int index) {
#if !USE_BURST
                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] FindRoadConnectionJob(index: {index})");
#endif

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
                FindRoadConnectionIterator findRoadConnectionIterator = default;
                findRoadConnectionIterator.m_BestCurvePos = 0f;
                findRoadConnectionIterator.m_BestRoad = Entity.Null;
                findRoadConnectionIterator.m_CanBeOnRoad = true;
                findRoadConnectionIterator.m_ConnectedParcelsBufferLookup = m_ConnectedParcelsBufferLookup;
                findRoadConnectionIterator.m_CurveDataComponentLookup = m_CurveDataComponentLookup;
                findRoadConnectionIterator.m_CompositionDataComponentLookup = m_CompositionDataComponentLookup;
                findRoadConnectionIterator.m_EdgeGeometryDataComponentLookup = m_EdgeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_StartNodeGeometryDataComponentLookup = m_StartNodeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_EndNodeGeometryDataComponentLookup = m_EndNodeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_PrefabNetCompositionDataComponentLookup = m_PrefabNetCompositionDataComponentLookup;
                findRoadConnectionIterator.m_DeletedDataComponentLookup = m_DeletedDataComponentLookup;
                findRoadConnectionIterator.m_Bounds = new Bounds3(
                    frontPosition - RoadConnectionSystem.MaxDistance,
                    frontPosition + RoadConnectionSystem.MaxDistance
                );
                findRoadConnectionIterator.m_MinDistance = RoadConnectionSystem.MaxDistance;
                findRoadConnectionIterator.m_FrontPosition = frontPosition;

                // Find suitable roads, iterate over roads and check which is best
                m_NetSearchTree.Iterate<FindRoadConnectionIterator>(ref findRoadConnectionIterator, 0);

                for (var k = 0; k < m_UpdatedNetChunks.Length; k++) {
                    var netArray = m_UpdatedNetChunks[k].GetNativeArray(m_EntityTypeHandle);
                    for (var l = 0; l < netArray.Length; l++) {
                        findRoadConnectionIterator.CheckEdge(netArray[l]);
                    }
                }

                // Update our BuildingRoadUpdateData struct with the new info
                currentEntityData.m_NewRoad = findRoadConnectionIterator.m_BestRoad;
                currentEntityData.m_FrontPos = findRoadConnectionIterator.m_FrontPosition;
                currentEntityData.m_CurvePos = findRoadConnectionIterator.m_BestCurvePos;

                // Update the data in the list with what we found
                m_ParcelEntitiesList[index] = currentEntityData;
#if !USE_BURST
                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] FindRoadConnectionJob() -- Updated list with eligible roads.");
#endif
            }

            /// <summary>
            /// todo.
            /// </summary>
            public struct FindRoadConnectionIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                /// <summary>
                /// todo.
                /// </summary>
                public Bounds3 m_Bounds;

                /// <summary>
                /// todo.
                /// </summary>
                public float m_MinDistance;

                /// <summary>
                /// todo.
                /// </summary>
                public float m_BestCurvePos;

                /// <summary>
                /// todo.
                /// </summary>
                public Entity m_BestRoad;

                /// <summary>
                /// todo.
                /// </summary>
                public float3 m_FrontPosition;

                /// <summary>
                /// todo.
                /// </summary>
                public bool m_CanBeOnRoad;

                /// <summary>
                /// todo.
                /// </summary>
                public BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<Curve> m_CurveDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<Composition> m_CompositionDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;

                /// <summary>
                /// todo.
                /// </summary>
                public ComponentLookup<Deleted> m_DeletedDataComponentLookup;

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

                /// <summary>
                /// todo.
                /// </summary>
                /// <param name="edgeEntity">Todo.</param>
                public void CheckEdge(Entity edgeEntity) {
                    // Exit early if the edge doesn't have a "Connected Buildings" buffer
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
                    RoadConnectionSystem.CheckDistance(edgeGeo, startNodeGeo, endNodeGeo, m_FrontPosition, m_CanBeOnRoad, ref distanceToFront);

                    // If the distanceToFront is less than the max
                    if (distanceToFront < m_MinDistance) {
                        // Retrieves the SelectedCurve data for the road edge, which represents the road's shape as a Bezier curve.
                        var curve = m_CurveDataComponentLookup[edgeEntity];

                        // Finds the nearest point on the curve to the entity's front position.
                        MathUtils.Distance(curve.m_Bezier.xz, m_FrontPosition.xz, out var nearestPointToFront);
                        var positionOfNearestPointToBuildingFront = MathUtils.Position(curve.m_Bezier, nearestPointToFront);

                        // Compute the tangent vector and determine the side of the curve (right or left)
                        var tangent = MathUtils.Tangent(curve.m_Bezier, nearestPointToFront).xz;
                        var toFront = m_FrontPosition.xz - positionOfNearestPointToBuildingFront.xz;
                        var isRightSide = math.dot(MathUtils.Right(tangent), toFront) >= 0f;

                        // Determine the relevant flags based on the side
                        var relevantFlags = isRightSide ? netCompositionData.m_Flags.m_Right : netCompositionData.m_Flags.m_Left;

                        // Check if the edge is raised or lowered
                        var isRaisedOrLowered = (relevantFlags & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) != 0U;

                        if (isRaisedOrLowered) {
                            return;
                        }

                        // If we got here, we found a valid best road entity, so store it
                        m_Bounds = new Bounds3(m_FrontPosition - distanceToFront, m_FrontPosition + distanceToFront);
                        m_MinDistance = distanceToFront;
                        m_BestCurvePos = nearestPointToFront;
                        m_BestRoad = edgeEntity;
                    }
                }
            }
        }
    }
}
