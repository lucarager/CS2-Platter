namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public static partial class RoadConnectionJobs {

        /// <summary>
        /// Find the best and eligible road for a given parcel.
        /// </summary>
        public struct FindRoadConnection : IJobParallelForDefer {
            public NativeArray<RoadConnectionSystem.ConnectionUpdateData> m_ConnectionUpdateDataList;
            [ReadOnly]
            public ComponentLookup<Deleted> m_DeletedDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefComponentLookup;
            [ReadOnly]
            public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<Transform> m_TransformComponentLookup;
            [ReadOnly]
            public BufferLookup<ConnectedBuilding> m_ConnectedBuildingsBufferLookup;
            [ReadOnly]
            public ComponentLookup<Curve> m_CurveDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<Composition> m_CompositionDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<EdgeGeometry> m_EdgeGeometryDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;
            [ReadOnly]
            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly]
            public NativeList<ArchetypeChunk> m_UpdatedNetChunks;
            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;

            public void Execute(int index) {
                // Retrieve the data
                var currentEntityData = this.m_ConnectionUpdateDataList[index];
                // If entity has DELETED component
                // mark its entry in the list as deleted and exit early
                if (this.m_DeletedDataComponentLookup.HasComponent(currentEntityData.m_Parcel)) {
                    currentEntityData.m_Deleted = true;
                    this.m_ConnectionUpdateDataList[index] = currentEntityData;
                    return;
                }

                var parcelPrefabRef = this.m_PrefabRefComponentLookup[currentEntityData.m_Parcel];
                var parcelPrefabData = this.m_ParcelDataComponentLookup[parcelPrefabRef.m_Prefab];
                var parcelTransform = this.m_TransformComponentLookup[currentEntityData.m_Parcel];

                // The "front position" is the point where a parcel is expected to connect to a road.
                var frontPosition = BuildingUtils.CalculateFrontPosition(parcelTransform, parcelPrefabData.m_LotSize.y);

                // Initializes a FindRoadConnectionIterator, used to iterate through potential road connections.
                var findRoadConnectionIterator = default(FindRoadConnectionIterator);
                findRoadConnectionIterator.m_BestCurvePos = 0f;
                findRoadConnectionIterator.m_BestRoad = Entity.Null;
                findRoadConnectionIterator.m_CanBeOnRoad = true;
                findRoadConnectionIterator.m_ConnectedBuildingsBufferLookup = m_ConnectedBuildingsBufferLookup;
                findRoadConnectionIterator.m_CurveDataComponentLookup = m_CurveDataComponentLookup;
                findRoadConnectionIterator.m_CompositionDataComponentLookup = m_CompositionDataComponentLookup;
                findRoadConnectionIterator.m_EdgeGeometryDataComponentLookup = m_EdgeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_StartNodeGeometryDataComponentLookup = m_StartNodeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_EndNodeGeometryDataComponentLookup = m_EndNodeGeometryDataComponentLookup;
                findRoadConnectionIterator.m_PrefabNetCompositionDataComponentLookup = m_PrefabNetCompositionDataComponentLookup;
                findRoadConnectionIterator.m_DeletedDataComponentLookup = m_DeletedDataComponentLookup;
                findRoadConnectionIterator.m_Bounds = new Bounds3(
                    frontPosition - RoadConnectionSystem.maxDistance,
                    frontPosition + RoadConnectionSystem.maxDistance
                );
                findRoadConnectionIterator.m_MinDistance = RoadConnectionSystem.maxDistance;
                findRoadConnectionIterator.m_FrontPosition = frontPosition;

                // Find suitable roads, iterate over roads and check which is best
                m_NetSearchTree.Iterate<FindRoadConnectionIterator>(ref findRoadConnectionIterator, 0);

                for (int k = 0; k < m_UpdatedNetChunks.Length; k++) {
                    NativeArray<Entity> netArray = m_UpdatedNetChunks[k].GetNativeArray(m_EntityTypeHandle);
                    for (int l = 0; l < netArray.Length; l++) {
                        findRoadConnectionIterator.CheckEdge(netArray[l]);
                    }
                }

                // Update our BuildingRoadUpdateData struct with the new info
                currentEntityData.m_NewRoad = findRoadConnectionIterator.m_BestRoad;
                currentEntityData.m_FrontPos = findRoadConnectionIterator.m_FrontPosition;
                currentEntityData.m_CurvePos = findRoadConnectionIterator.m_BestCurvePos;

                // Update the data in the list with what we found
                this.m_ConnectionUpdateDataList[index] = currentEntityData;

                Mod.Instance.Log.Debug($"[RoadConnectionJobs->FindRoadConnection] Updated list with eligible roads.");
            }

            public struct FindRoadConnectionIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public Bounds3 m_Bounds;
                public float m_MinDistance;
                public float m_BestCurvePos;
                public Entity m_BestRoad;
                public float3 m_FrontPosition;
                public bool m_CanBeOnRoad;

                public BufferLookup<ConnectedBuilding> m_ConnectedBuildingsBufferLookup;
                public ComponentLookup<Curve> m_CurveDataComponentLookup;
                public ComponentLookup<Composition> m_CompositionDataComponentLookup;
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryDataComponentLookup;
                public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryDataComponentLookup;
                public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryDataComponentLookup;
                public ComponentLookup<NetCompositionData> m_PrefabNetCompositionDataComponentLookup;
                public ComponentLookup<Deleted> m_DeletedDataComponentLookup;

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds.xz);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds.xz)) {
                        return;
                    }
                    if (this.m_DeletedDataComponentLookup.HasComponent(edgeEntity)) {
                        return;
                    }
                    this.CheckEdge(edgeEntity);
                }

                public void CheckEdge(Entity edgeEntity) {
                    // Exit early if the edge doesn't have a "Connected Buildings" buffer
                    if (!this.m_ConnectedBuildingsBufferLookup.HasBuffer(edgeEntity)) {
                        return;
                    }

                    // Retrieve composition data
                    // Exit early if the road is elevated or a tunnel.
                    NetCompositionData netCompositionData = default(NetCompositionData);
                    if (
                        this.m_CompositionDataComponentLookup.TryGetComponent(edgeEntity, out var composition) &&
                        this.m_PrefabNetCompositionDataComponentLookup.TryGetComponent(composition.m_Edge, out netCompositionData) &&
                        (netCompositionData.m_Flags.m_General & (CompositionFlags.General.Elevated | CompositionFlags.General.Tunnel)) != (CompositionFlags.General)0U) {
                        return;
                    }

                    // Check whether the entity can be connected to the road based on a maximum distance
                    // Calls RoadConnectionSystem.CheckDistance, which likely checks the distance from the entity to a road and updates the distanceToRoad if necessary.
                    EdgeGeometry edgeGeo = this.m_EdgeGeometryDataComponentLookup[edgeEntity];
                    EdgeNodeGeometry startNodeGeo = this.m_StartNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    EdgeNodeGeometry endNodeGeo = this.m_EndNodeGeometryDataComponentLookup[edgeEntity].m_Geometry;
                    float distanceToFront = this.m_MinDistance;
                    RoadConnectionSystem.CheckDistance(edgeGeo, startNodeGeo, endNodeGeo, this.m_FrontPosition, this.m_CanBeOnRoad, ref distanceToFront);

                    //  If the distanceToFront is less than the max
                    if (distanceToFront < this.m_MinDistance) {
                        // Retrieves the Curve data for the road edge, which represents the road's shape as a Bezier curve.
                        Curve curve = this.m_CurveDataComponentLookup[edgeEntity];

                        // Finds the nearest point on the curve to the entity's front position.
                        MathUtils.Distance(curve.m_Bezier.xz, this.m_FrontPosition.xz, out var nearestPointToFront);
                        float3 positionOfNearestPointToBuildingFront = MathUtils.Position(curve.m_Bezier, nearestPointToFront);

                        // Compute the tangent vector and determine the side of the curve (right or left)
                        var tangent = MathUtils.Tangent(curve.m_Bezier, nearestPointToFront).xz;
                        var toFront = this.m_FrontPosition.xz - positionOfNearestPointToBuildingFront.xz;
                        bool isRightSide = math.dot(MathUtils.Right(tangent), toFront) >= 0f;

                        // Determine the relevant flags based on the side
                        CompositionFlags.Side relevantFlags = isRightSide ? netCompositionData.m_Flags.m_Right : netCompositionData.m_Flags.m_Left;

                        // Check if the edge is raised or lowered
                        bool isRaisedOrLowered = (relevantFlags & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) != (CompositionFlags.Side)0U;

                        if (isRaisedOrLowered) {
                            return;
                        }

                        // If we got here, we found a valid best road entity, so store it
                        this.m_Bounds = new Bounds3(this.m_FrontPosition - distanceToFront, this.m_FrontPosition + distanceToFront);
                        this.m_MinDistance = distanceToFront;
                        this.m_BestCurvePos = nearestPointToFront;
                        this.m_BestRoad = edgeEntity;
                    }
                }
            }
        }
    }
}
