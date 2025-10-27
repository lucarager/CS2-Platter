// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using Block = Game.Zones.Block;

    /// <summary>
    /// Snap System. Ovverides object placement to snap parcels to road sides.
    /// </summary>
    public partial class P_SnapSystem : GameSystemBase {
        public static float MaxSnapDistance     { get; } = 16f;
        public static float DefaultSnapDistance { get; } = 0f;

        // Props
        public SnapMode CurrentSnapMode {
            get => m_SnapMode;

            set => m_SnapMode = value;
        }

        public float CurrentSnapOffset {
            get => m_SnapOffset;

            set => m_SnapOffset = value;
        }

        // Logger
        private PrefixedLogger m_Log;

        // Systems
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private Game.Net.SearchSystem   m_NetSearchSystem;
        private ObjectToolSystem        m_ObjectToolSystem;
        private PrefabSystem            m_PrefabSystem;
        private ToolSystem              m_ToolSystem;
        private TerrainSystem           m_TerrainSystem;

        // Data
        private EntityQuery m_Query;
        private SnapMode    m_SnapMode;
        private float       m_SnapOffset;

        [Flags]
        public enum SnapMode : uint {
            None     = 0u,
            RoadSide = 1u,
            All      = uint.MaxValue,
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneSearchSystem = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_NetSearchSystem  = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PrefabSystem     = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem       = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem    = World.GetOrCreateSystemManaged<TerrainSystem>();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_SnapSystem));
            m_Log.Debug("OnCreate()");

            // Query
            m_Query = SystemAPI.QueryBuilder()
                               .WithAllRW<ObjectDefinition>()
                               .WithAll<CreationDefinition, Updated>()
                               .WithNone<Deleted, Overridden>()
                               .Build();

            // Data
            m_SnapOffset = DefaultSnapDistance;
            m_SnapMode   = SnapMode.RoadSide;

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Exit early on certain conditions
            if (m_Query.IsEmptyIgnoreFilter                     ||
                m_ToolSystem.activeTool is not ObjectToolSystem ||
                m_ObjectToolSystem.prefab is not ParcelPrefab) {
                return;
            }

            // Exit on disabled snap
            if (!m_SnapMode.HasFlag(SnapMode.RoadSide)) {
                return;
            }

            // Grab control points from ObjectTool
            var controlPoints = m_ObjectToolSystem.GetControlPoints(out var deps);
            Dependency = JobHandle.CombineDependencies(Dependency, deps);

            // If none, exit
            if (controlPoints.Length == 0) {
                return;
            }

            // Schedule our snapping job
            var parcelSnapJobHandle = new ParcelSnapJob(
                m_ZoneSearchSystem.GetSearchTree(true, out var zoneTreeJobHandle),
                m_NetSearchSystem.GetNetSearchTree(true, out var netTreeJobHandle),
                controlPoints: controlPoints,
                objectDefinitionTypeHandle: SystemAPI.GetComponentTypeHandle<ObjectDefinition>(),
                creationDefinitionTypeHandle: SystemAPI.GetComponentTypeHandle<CreationDefinition>(true),
                blockComponentLookup: SystemAPI.GetComponentLookup<Block>(true),
                parcelDataComponentLookup: SystemAPI.GetComponentLookup<ParcelData>(true),
                parcelOwnerComponentLookup: SystemAPI.GetComponentLookup<ParcelOwner>(true),
                terrainHeightData: m_TerrainSystem.GetHeightData(),
                snapOffset: m_SnapOffset,
                entityTypeHandle: SystemAPI.GetEntityTypeHandle()
            ).ScheduleParallel(
                m_Query,
                JobHandle.CombineDependencies(Dependency, zoneTreeJobHandle, netTreeJobHandle)
            );

            m_ZoneSearchSystem.AddSearchTreeReader(parcelSnapJobHandle);
            m_NetSearchSystem.AddNetSearchTreeReader(parcelSnapJobHandle);

            // Register deps
            Dependency = JobHandle.CombineDependencies(Dependency, parcelSnapJobHandle);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct ParcelSnapJob : IJobChunk {
            [ReadOnly] private NativeQuadTree<Entity, Bounds2>          m_ZoneTree;
            [ReadOnly] private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetTree;
            [ReadOnly] private TerrainHeightData                        m_TerrainHeightData;
            [ReadOnly] private NativeList<ControlPoint>                 m_ControlPoints;
            [ReadOnly] private ComponentTypeHandle<CreationDefinition>  m_CreationDefinitionTypeHandle;
            [ReadOnly] private ComponentLookup<Block>                   m_BlockComponentLookup;
            [ReadOnly] private ComponentLookup<ParcelOwner>             m_ParcelOwnerComponentLookup;
            [ReadOnly] private ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] private float                                    m_SnapOffset;
            [ReadOnly] private EntityTypeHandle                         m_EntityTypeHandle;
            private            ComponentTypeHandle<ObjectDefinition>    m_ObjectDefinitionTypeHandle;

            public ParcelSnapJob(NativeQuadTree<Entity, Bounds2>          zoneTree,
                                 NativeQuadTree<Entity, QuadTreeBoundsXZ> netTree,
                                 TerrainHeightData                        terrainHeightData,
                                 NativeList<ControlPoint>                 controlPoints,
                                 ComponentTypeHandle<CreationDefinition>  creationDefinitionTypeHandle,
                                 ComponentLookup<Block>                   blockComponentLookup,
                                 ComponentLookup<ParcelData>              parcelDataComponentLookup,
                                 ComponentLookup<ParcelOwner>             parcelOwnerComponentLookup,
                                 float                                    snapOffset,
                                 EntityTypeHandle                         entityTypeHandle,
                                 ComponentTypeHandle<ObjectDefinition>    objectDefinitionTypeHandle) {
                m_ZoneTree                     = zoneTree;
                m_NetTree                      = netTree;
                m_TerrainHeightData            = terrainHeightData;
                m_ControlPoints                = controlPoints;
                m_CreationDefinitionTypeHandle = creationDefinitionTypeHandle;
                m_BlockComponentLookup         = blockComponentLookup;
                m_ParcelOwnerComponentLookup   = parcelOwnerComponentLookup;
                m_ParcelDataComponentLookup    = parcelDataComponentLookup;
                m_SnapOffset                   = snapOffset;
                m_EntityTypeHandle             = entityTypeHandle;
                m_ObjectDefinitionTypeHandle   = objectDefinitionTypeHandle;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray             = chunk.GetNativeArray(m_EntityTypeHandle);
                var objectDefinitionArray   = chunk.GetNativeArray(ref m_ObjectDefinitionTypeHandle);
                var creationDefinitionArray = chunk.GetNativeArray(ref m_CreationDefinitionTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity             = entityArray[i];
                    var creationDefinition = creationDefinitionArray[i];
                    var objectDefinition   = objectDefinitionArray[i];

                    // Get some data
                    var parcelData = m_ParcelDataComponentLookup[creationDefinition.m_Prefab];

                    // Calculate geometry
                    var searchRadius = parcelData.m_LotSize.y * 4f + 16f;
                    var minValue     = float.MinValue;
                    var controlPoint = m_ControlPoints[0];
                    var bounds = new Bounds3(
                        controlPoint.m_Position - searchRadius,
                        controlPoint.m_Position + searchRadius
                    );

                    // At minimum, the distance to snap must be
                    var minDistance = math.cmin(parcelData.m_LotSize) * 4f + 16f;

                    // Default to our control point as a start
                    var bestSnapPosition = controlPoint;

                    // Iterate over zones to find a snapping point
                    var iterator = new BlockSnapIterator(
                        controlPoint: controlPoint,
                        bestSnapPosition: bestSnapPosition,
                        bounds: bounds.xz,
                        blockComponentLookup: m_BlockComponentLookup,
                        parcelOwnerComponentLookup: m_ParcelOwnerComponentLookup,
                        bestDistance: minDistance,
                        lotSize: parcelData.m_LotSize,
                        snapOffset: m_SnapOffset
                    );
                    m_ZoneTree.Iterate(ref iterator);

                    // Retrieve best found point
                    bestSnapPosition = iterator.BestSnapPosition;
                    var hasSnap = !controlPoint.m_Position.Equals(bestSnapPosition.m_Position);

                    // Height Calc
                    CalculateHeight(ref bestSnapPosition, parcelData, minValue);

                    // If we found a snapping point, modify the object definition
                    if (!hasSnap) {
                        continue;
                    }

                    objectDefinition.m_Position      = bestSnapPosition.m_Position;
                    objectDefinition.m_LocalPosition = bestSnapPosition.m_Position;
                    objectDefinition.m_Rotation      = bestSnapPosition.m_Rotation;
                    objectDefinition.m_LocalRotation = bestSnapPosition.m_Rotation;

                    objectDefinitionArray[i] = objectDefinition;
                }
            }

            private void CalculateHeight(ref ControlPoint controlPoint, ParcelData parcelData, float waterSurfaceHeight) {
                var parcelFrontPosition = BuildingUtils.CalculateFrontPosition(
                    new Game.Objects.Transform(controlPoint.m_Position, controlPoint.m_Rotation), parcelData.m_LotSize.y);
                var targetHeight = TerrainUtils.SampleHeight(ref m_TerrainHeightData, parcelFrontPosition);
                controlPoint.m_Position.y = targetHeight;
            }
        }

        /// <summary>
        /// QuadTree iterator.
        /// </summary>
        public struct BlockSnapIterator : INativeQuadTreeIterator<Entity, Bounds2> {
            public  ControlPoint                 BestSnapPosition => m_BestSnapPosition;
            private ComponentLookup<Block>       m_BlockComponentLookup;
            private ComponentLookup<ParcelOwner> m_ParcelOwnerComponentLookup;
            private ControlPoint                 m_ControlPoint;
            private int2                         m_LotSize;
            private Bounds2                      m_Bounds;
            private ControlPoint                 m_BestSnapPosition;
            private float                        m_BestDistance;
            private float                        m_SnapOffset;

            public BlockSnapIterator(ComponentLookup<ParcelOwner> parcelOwnerComponentLookup,
                                     ComponentLookup<Block>       blockComponentLookup,
                                     ControlPoint                 controlPoint,
                                     int2                         lotSize,
                                     Bounds2                      bounds,
                                     ControlPoint                 bestSnapPosition,
                                     float                        bestDistance,
                                     float                        snapOffset) {
                m_ParcelOwnerComponentLookup = parcelOwnerComponentLookup;
                m_BlockComponentLookup       = blockComponentLookup;
                m_ControlPoint               = controlPoint;
                m_LotSize                    = lotSize;
                m_Bounds                     = bounds;
                m_BestSnapPosition           = bestSnapPosition;
                m_BestDistance               = bestDistance;
                m_SnapOffset                 = snapOffset;
            }

            /// <summary>
            /// Tests whether XZ bounds intersect.
            /// </summary>
            /// <param name="bounds">Quad tree bounds (XZ) for testing.</param>
            /// <returns><c>true</c> if bounds intersect, <c>false</c> otherwise.</returns>
            public bool Intersect(Bounds2 bounds) { return MathUtils.Intersect(bounds, m_Bounds); }

            /// <summary>
            /// BlockSnapIterator.
            /// </summary>
            /// <param name="bounds">Bounds blockCorners tree to check for intersection.</param>
            /// <param name="blockEntity">Entity to enqueue if bounds intersect.</param>
            public void Iterate(Bounds2 bounds, Entity blockEntity) {
                // Early exit if bounds don't intersect
                if (!MathUtils.Intersect(bounds, m_Bounds)) {
                    return;
                }

                // Discard if this is a parcel block
                if (m_ParcelOwnerComponentLookup.HasComponent(blockEntity)) {
                    return;
                }

                // Get the block's geometry
                var block          = m_BlockComponentLookup[blockEntity];
                var blockCorners   = ZoneUtils.CalculateCorners(block);
                var blockFrontEdge = new Line2.Segment(blockCorners.a, blockCorners.b);

                // Create a search line at the cursor position
                var searchLine = new Line2.Segment(m_ControlPoint.m_HitPosition.xz, m_ControlPoint.m_HitPosition.xz);

                // Extend the search line based on lot depth vs width difference
                var lotDepthDifference = math.max(0f, m_LotSize.y - m_LotSize.x) * 4f;
                searchLine.a -= lotDepthDifference;
                searchLine.b += lotDepthDifference;

                // Calculate distance between the block's front edge and our search line
                var distanceToBlock = MathUtils.Distance(blockFrontEdge, searchLine, out var intersectionParams);

                // If distance is exactly 0 (overlapping),
                // applies a bonus based on how centered the overlap is (prefers center alignment)
                if (distanceToBlock == 0f) {
                    var centeredness = 0.5f - math.abs(intersectionParams.y - 0.5f);
                    distanceToBlock -= centeredness;
                }

                // If distance exceeds our "best", exit
                if (distanceToBlock >= m_BestDistance) {
                    return;
                }

                m_BestDistance = distanceToBlock;

                // Calculate offset from block center to cursor
                var cursorOffsetFromBlock = m_ControlPoint.m_HitPosition.xz - block.m_Position.xz;

                // Get perpendicular direction for lateral calculations
                var blockForwardDirection = block.m_Direction;
                var blockLeftDirection    = MathUtils.Left(block.m_Direction);

                // Calculate depths 
                var blockDepth = block.m_Size.y * 4f;

                // Project cursor offset onto block's forward and lateral axes
                var forwardOffset = math.dot(blockForwardDirection, cursorOffsetFromBlock);
                var lateralOffset = math.dot(blockLeftDirection, cursorOffsetFromBlock);

                // Snap lateral position to 8-unit grid, accounting for odd/even width differences
                var hasDifferentParity = ((block.m_Size.x ^ m_LotSize.x) & 1) != 0;
                var parityOffset       = hasDifferentParity ? 0.5f : 0f;
                lateralOffset -= (math.round(lateralOffset / 8f - parityOffset) + parityOffset) * 8f;

                // Build the snapped position
                m_BestSnapPosition            = m_ControlPoint;
                m_BestSnapPosition.m_Position = m_ControlPoint.m_HitPosition;

                // Align new lot behind the existing block's front edge
                var depthAlignment = blockDepth - forwardOffset - m_SnapOffset;
                m_BestSnapPosition.m_Position.xz += blockForwardDirection * depthAlignment;

                // Apply lateral grid snapping
                m_BestSnapPosition.m_Position.xz -= blockLeftDirection * lateralOffset;

                // Set direction and rotation to match the block
                m_BestSnapPosition.m_Direction = block.m_Direction;
                m_BestSnapPosition.m_Rotation  = ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);

                // Calculate snap priority
                m_BestSnapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                    0f,
                    1f,
                    0f,
                    m_ControlPoint.m_HitPosition  * 0.5f,
                    m_BestSnapPosition.m_Position * 0.5f,
                    m_BestSnapPosition.m_Direction
                );

                // Cache block
                m_BestSnapPosition.m_OriginalEntity = blockEntity;
            }
        }

        /// <summary>
        /// QuadTree iterator.
        /// </summary>
        public struct EdgeSnapIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            private bool                                m_EditorMode;
            private Bounds3                             m_TotalBounds;
            private Bounds3                             m_Bounds;
            private Snap                                m_Snap;
            private Entity                              m_ServiceUpgradeOwner;
            private float                               m_SnapOffset;
            private float                               m_SnapDistance;
            private float                               m_Elevation;
            private float                               m_GuideLength;
            private float                               m_LegSnapWidth;
            private Bounds1                             m_HeightRange;
            private NetData                             m_NetData;
            private RoadData                            m_PrefabRoadData;
            private NetGeometryData                     m_NetGeometryData;
            private LocalConnectData                    m_LocalConnectData;
            private ControlPoint                        m_ControlPoint;
            private ControlPoint                        m_BestSnapPosition;
            private NativeList<SnapLine>                m_SnapLines;
            private TerrainHeightData                   m_TerrainHeightData;
            private WaterSurfaceData                    m_WaterSurfaceData;
            private ComponentLookup<Owner>              m_OwnerLookup;
            private ComponentLookup<Node>               m_NodeLookup;
            private ComponentLookup<Edge>               m_EdgeLookup;
            private ComponentLookup<Curve>              m_CurveLookup;
            private ComponentLookup<Composition>        m_CompositionLookup;
            private ComponentLookup<EdgeGeometry>       m_EdgeGeometryLookup;
            private ComponentLookup<Road>               m_RoadLookup;
            private ComponentLookup<PrefabRef>          m_PrefabRefLookup;
            private ComponentLookup<NetData>            m_PrefabNetLookup;
            private ComponentLookup<NetGeometryData>    m_NetGeometryDataLookup;
            private ComponentLookup<NetCompositionData> m_NetCompositionDataLookup;
            private ComponentLookup<RoadComposition>    m_RoadCompositionLookup;
            private BufferLookup<ConnectedEdge>         m_ConnectedEdgeLookup;
            private BufferLookup<Game.Net.SubNet>       m_SubNetLookup;
            private BufferLookup<NetCompositionArea>    m_PrefabCompositionAreaLookup;

            public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds); }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
                if (!MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds)) {
                    return;
                }

                if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && HandleGeometry(entity)) { }
            }

            public bool HandleGeometry(Entity entity) {
                var prefabRef    = m_PrefabRefLookup[entity];
                var controlPoint = m_ControlPoint;
                controlPoint.m_OriginalEntity = entity;

                var distance = m_NetGeometryData.m_DefaultWidth * 0.5f + m_SnapOffset;

                if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                    var netGeometryData = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                    if ((m_NetGeometryData.m_Flags & ~netGeometryData.m_Flags & GeometryFlags.StandingNodes) != 0) {
                        distance = m_LegSnapWidth * 0.5f + m_SnapOffset;
                    }
                }

                if (m_ConnectedEdgeLookup.HasBuffer(entity)) {
                    var node                = m_NodeLookup[entity];
                    var connectedEdgeBuffer = m_ConnectedEdgeLookup[entity];
                    for (var i = 0; i < connectedEdgeBuffer.Length; i++) {
                        var edge = m_EdgeLookup[connectedEdgeBuffer[i].m_Edge];
                        if (edge.m_Start == entity || edge.m_End == entity) {
                            return false;
                        }
                    }

                    if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        var netGeometryData2 = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                        distance += netGeometryData2.m_DefaultWidth * 0.5f;
                    }

                    if (math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >= distance) {
                        return false;
                    }

                    return HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                }

                if (!m_CurveLookup.HasComponent(entity)) {
                    return false;
                }

                var curve = m_CurveLookup[entity];

                if (m_CompositionLookup.HasComponent(entity)) {
                    var composition        = m_CompositionLookup[entity];
                    var netCompositionData = m_NetCompositionDataLookup[composition.m_Edge];
                    distance += netCompositionData.m_Width * 0.5f;
                }

                if (MathUtils.Distance(curve.m_Bezier.xz, m_ControlPoint.m_HitPosition.xz, out controlPoint.m_CurvePosition) >=
                    distance) {
                    return false;
                }

                var snapHeight = MathUtils.Position(curve.m_Bezier, controlPoint.m_CurvePosition).y;

                return HandleGeometry(controlPoint, snapHeight, prefabRef, false);
            }

            public bool HandleGeometry(ControlPoint controlPoint, float snapHeight, PrefabRef prefabRef,
                                       bool         ignoreHeightDistance) {
                if (!m_PrefabNetLookup.HasComponent(prefabRef.m_Prefab)) {
                    return false;
                }

                var netData = m_PrefabNetLookup[prefabRef.m_Prefab];

                var   flag  = false;
                var   flag2 = true;
                var   flag3 = true;
                float height;

                if (m_Elevation < 0f) {
                    height = TerrainUtils.SampleHeight(ref m_TerrainHeightData, controlPoint.m_HitPosition) + m_Elevation;
                } else {
                    height =
                    WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, controlPoint.m_HitPosition) +
                    m_Elevation;
                }

                if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                    var netGeometryData = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                    var bounds          = m_NetGeometryData.m_DefaultHeightRange + height;
                    var bounds2         = netGeometryData.m_DefaultHeightRange   + snapHeight;
                    if (!MathUtils.Intersect(bounds, bounds2)) {
                        flag2 = false;
                        flag3 = (netGeometryData.m_Flags & GeometryFlags.NoEdgeConnection) == 0;
                    }
                }

                if (flag2 && !NetUtils.CanConnect(netData, m_NetData)) {
                    return flag;
                }

                if ((m_NetData.m_ConnectLayers & ~netData.m_RequiredLayers & Layer.LaneEditor) != Layer.None) {
                    return flag;
                }

                var num2 = snapHeight - height;

                if (!ignoreHeightDistance && !MathUtils.Intersect(m_HeightRange, num2)) {
                    return flag;
                }

                if (m_NodeLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                    if (m_ConnectedEdgeLookup.HasBuffer(controlPoint.m_OriginalEntity)) {
                        var dynamicBuffer = m_ConnectedEdgeLookup[controlPoint.m_OriginalEntity];
                        if (dynamicBuffer.Length != 0) {
                            for (var i = 0; i < dynamicBuffer.Length; i++) {
                                var edge  = dynamicBuffer[i].m_Edge;
                                var edge2 = m_EdgeLookup[edge];
                                if (!(edge2.m_Start != controlPoint.m_OriginalEntity) ||
                                    !(edge2.m_End   != controlPoint.m_OriginalEntity)) {
                                    HandleCurve(controlPoint, edge, flag3, ref flag);
                                }
                            }

                            return flag;
                        }
                    }

                    var controlPoint2 = controlPoint;
                    var node          = m_NodeLookup[controlPoint.m_OriginalEntity];
                    controlPoint2.m_Position  = node.m_Position;
                    controlPoint2.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                    MathUtils.TryNormalize(ref controlPoint2.m_Direction);
                    var num3 = 1f;
                    if ((m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes) != 0) {
                        var num4 = m_NetGeometryData.m_DefaultWidth * 0.5f;
                        if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                            var netGeometryData2 = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                            num4 += netGeometryData2.m_DefaultWidth * 0.5f;
                        }

                        if (math.distance(node.m_Position.xz, controlPoint.m_HitPosition.xz) <= num4) {
                            num3 = 2f;
                        }
                    }

                    controlPoint2.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                        num3, 1f, 1f, controlPoint.m_HitPosition, controlPoint2.m_Position, controlPoint2.m_Direction);
                    ToolUtils.AddSnapPosition(ref m_BestSnapPosition, controlPoint2);
                    flag = true;
                } else if (m_CurveLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                    HandleCurve(controlPoint, controlPoint.m_OriginalEntity, flag3, ref flag);
                }

                return flag;
            }

            private void HandleCurve(ControlPoint controlPoint, Entity curveEntity, bool allowEdgeSnap, ref bool snapAdded) {
                var flag = false;
                var flag2 = (m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes)           == 0 &&
                            (m_PrefabRoadData.m_Flags  & Game.Prefabs.RoadFlags.EnableZoning) != 0 &&
                            (m_Snap                    & Snap.CellLength)                     > Snap.None;
                var   defaultWidth    = m_NetGeometryData.m_DefaultWidth;
                var   num             = defaultWidth;
                var   num2            = m_NetGeometryData.m_DefaultWidth * 0.5f;
                bool2 @bool           = false;
                var   prefabRef       = m_PrefabRefLookup[curveEntity];
                var   netGeometryData = default(NetGeometryData);

                if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                    netGeometryData = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                }

                if (m_CompositionLookup.HasComponent(curveEntity)) {
                    var composition        = m_CompositionLookup[curveEntity];
                    var netCompositionData = m_NetCompositionDataLookup[composition.m_Edge];
                    num2 += netCompositionData.m_Width * 0.5f;
                    if ((m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes) == 0) {
                        num = netGeometryData.m_DefaultWidth;
                        if (m_RoadCompositionLookup.HasComponent(composition.m_Edge)) {
                            flag = (m_RoadCompositionLookup[composition.m_Edge].m_Flags & Game.Prefabs.RoadFlags.EnableZoning) !=
                            0 && (m_Snap & Snap.CellLength) > Snap.None;
                            if (flag && m_RoadLookup.HasComponent(curveEntity)) {
                                var road = m_RoadLookup[curveEntity];
                                @bool.x = (road.m_Flags & Game.Net.RoadFlags.StartHalfAligned) > 0;
                                @bool.y = (road.m_Flags & Game.Net.RoadFlags.EndHalfAligned)   > 0;
                            }
                        }
                    }

                    if ((m_NetGeometryData.m_Flags & GeometryFlags.SnapToNetAreas) != 0) {
                        var dynamicBuffer = m_PrefabCompositionAreaLookup[composition.m_Edge];
                        var edgeGeometry  = m_EdgeGeometryLookup[curveEntity];
                        if (SnapSegmentAreas(
                                controlPoint, netCompositionData, dynamicBuffer, edgeGeometry.m_Start, ref snapAdded) |
                            SnapSegmentAreas(
                                controlPoint, netCompositionData, dynamicBuffer, edgeGeometry.m_End, ref snapAdded)) {
                            return;
                        }
                    }
                }

                int   num3;
                float num4;
                float num5;
                if (flag2) {
                    var cellWidth  = ZoneUtils.GetCellWidth(defaultWidth);
                    var cellWidth2 = ZoneUtils.GetCellWidth(num);
                    num3 = 1 + math.abs(cellWidth2 - cellWidth);
                    num4 = (num3 - 1) * -4f;
                    num5 = 8f;
                } else {
                    var num6 = math.abs(num - defaultWidth);
                    if (num6 > 1.6f) {
                        num3 = 3;
                        num4 = num6 * -0.5f;
                        num5 = num6 * 0.5f;
                    } else {
                        num3 = 1;
                        num4 = 0f;
                        num5 = 0f;
                    }
                }

                float num7;
                if (flag) {
                    var cellWidth3 = ZoneUtils.GetCellWidth(defaultWidth);
                    var cellWidth4 = ZoneUtils.GetCellWidth(num);
                    num7 = math.select(0f, 4f, (((cellWidth3 ^ cellWidth4) & 1) != 0) ^ @bool.x);
                } else {
                    num7 = 0f;
                }

                var   curve = m_CurveLookup[curveEntity];
                Owner owner;
                Owner owner2;
                if (!m_EditorMode                          && m_OwnerLookup.TryGetComponent(curveEntity, out owner) &&
                    owner.m_Owner != m_ServiceUpgradeOwner && (!m_EdgeLookup.HasComponent(owner.m_Owner) ||
                                                               (m_OwnerLookup.TryGetComponent(owner.m_Owner, out owner2) &&
                                                                owner2.m_Owner != m_ServiceUpgradeOwner))) {
                    allowEdgeSnap = false;
                }

                var @float = math.normalizesafe(MathUtils.Left(MathUtils.StartTangent(curve.m_Bezier).xz));
                var float2 = math.normalizesafe(MathUtils.Left(curve.m_Bezier.c.xz - curve.m_Bezier.b.xz));
                var float3 = math.normalizesafe(MathUtils.Left(MathUtils.EndTangent(curve.m_Bezier).xz));
                var flag3  = math.dot(@float, float2) > 0.9998477f && math.dot(float2, float3) > 0.9998477f;
                var i      = 0;
                while (i < num3) {
                    Bezier4x3 bezier4x;
                    if (math.abs(num4) < 0.08f) {
                        bezier4x = curve.m_Bezier;
                    } else if (flag3) {
                        bezier4x      = curve.m_Bezier;
                        bezier4x.a.xz = bezier4x.a.xz + @float                                 * num4;
                        bezier4x.b.xz = bezier4x.b.xz + math.lerp(@float, float3, 0.33333334f) * num4;
                        bezier4x.c.xz = bezier4x.c.xz + math.lerp(@float, float3, 0.6666667f)  * num4;
                        bezier4x.d.xz = bezier4x.d.xz + float3                                 * num4;
                    } else {
                        bezier4x = NetUtils.OffsetCurveLeftSmooth(curve.m_Bezier, num4);
                    }

                    float num9;
                    float num8;
                    if ((m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes) == 0) {
                        num8 = NetUtils.ExtendedDistance(bezier4x.xz, controlPoint.m_HitPosition.xz, out num9);
                    } else {
                        num8 = MathUtils.Distance(bezier4x.xz, controlPoint.m_HitPosition.xz, out num9);
                    }

                    var controlPoint2 = controlPoint;
                    if ((m_Snap & Snap.CellLength) != Snap.None) {
                        var num10 = MathUtils.Length(bezier4x.xz);
                        num10 += math.select(0f, 4f, @bool.x != @bool.y);
                        num10 =  math.fmod(num10 + 0.1f, 8f) * 0.5f;
                        var num11 = NetUtils.ExtendedLength(bezier4x.xz, num9);
                        num11 = MathUtils.Snap(num11, m_SnapDistance, num7 + num10);
                        num9  = NetUtils.ExtendedClampLength(bezier4x.xz, num11);
                        if ((m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes) != 0) {
                            num9 = math.saturate(num9);
                        }

                        controlPoint2.m_CurvePosition = num9;
                    } else {
                        num9 = math.saturate(num9);
                        if ((netGeometryData.m_Flags & GeometryFlags.SnapCellSize) != 0) {
                            var num12 = NetUtils.ExtendedLength(bezier4x.xz, num9);
                            num12                         = MathUtils.Snap(num12, 4f);
                            controlPoint2.m_CurvePosition = NetUtils.ExtendedClampLength(bezier4x.xz, num12);
                        } else {
                            if (num9 >= 0.5f) {
                                if (math.distance(bezier4x.d.xz, controlPoint.m_HitPosition.xz) < m_SnapOffset) {
                                    num9 = 1f;
                                }
                            } else if (math.distance(bezier4x.a.xz, controlPoint.m_HitPosition.xz) < m_SnapOffset) {
                                num9 = 0f;
                            }

                            controlPoint2.m_CurvePosition = num9;
                        }
                    }

                    if (allowEdgeSnap || num9 <= 0f || num9 >= 1f) {
                        goto IL_06FE;
                    }

                    if (num9 >= 0.5f) {
                        if (math.distance(bezier4x.d.xz, controlPoint.m_HitPosition.xz) < num2 + m_SnapOffset) {
                            num9                          = 1f;
                            controlPoint2.m_CurvePosition = 1f;
                            goto IL_06FE;
                        }
                    } else if (math.distance(bezier4x.a.xz, controlPoint.m_HitPosition.xz) < num2 + m_SnapOffset) {
                        num9                          = 0f;
                        controlPoint2.m_CurvePosition = 0f;
                        goto IL_06FE;
                    }

                    IL_07C1:
                    i++;
                    continue;
                    IL_06FE:
                    float3 float4;
                    NetUtils.ExtendedPositionAndTangent(bezier4x, num9, out controlPoint2.m_Position, out float4);
                    controlPoint2.m_Direction = float4.xz;
                    MathUtils.TryNormalize(ref controlPoint2.m_Direction);
                    var num13 = 1f;
                    if ((m_NetGeometryData.m_Flags & GeometryFlags.StrictNodes) != 0 && num8 <= num2) {
                        num13 = 2f;
                    }

                    controlPoint2.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                        num13, 1f, 1f, controlPoint.m_HitPosition, controlPoint2.m_Position, controlPoint2.m_Direction);
                    ToolUtils.AddSnapPosition(ref m_BestSnapPosition, controlPoint2);
                    //ToolUtils.AddSnapLine(
                    //    ref m_BestSnapPosition, m_SnapLines,
                    //    new SnapLine(
                    //        controlPoint2, bezier4x, NetToolSystem.SnapJob.GetSnapLineFlags(m_NetGeometryData.m_Flags), 1f));
                    snapAdded =  true;
                    num4      += num5;
                    goto IL_07C1;
                }
            }

            private bool SnapSegmentAreas(ControlPoint controlPoint, NetCompositionData prefabCompositionData,
                                          DynamicBuffer<NetCompositionArea> areas, Segment segment, ref bool snapAdded) {
                var flag = false;
                for (var i = areas.Length - 1; i >= 0; i--) {
                    var netCompositionArea = areas[i];
                    if ((netCompositionArea.m_Flags & NetAreaFlags.Buildable) == 0) {
                        continue;
                    }

                    var num = netCompositionArea.m_Width * 0.51f;

                    if (!(m_LegSnapWidth * 0.5f < num)) {
                        continue;
                    }

                    flag = true;
                    var bezier4x = MathUtils.Lerp(
                        segment.m_Left, segment.m_Right, netCompositionArea.m_Position.x / prefabCompositionData.m_Width + 0.5f);
                    float num3;
                    var   num2          = MathUtils.Distance(bezier4x.xz, controlPoint.m_HitPosition.xz, out num3);
                    var   controlPoint2 = controlPoint;
                    controlPoint2.m_Position  = MathUtils.Position(bezier4x, num3);
                    controlPoint2.m_Direction = math.normalizesafe(MathUtils.Tangent(bezier4x, num3).xz);
                    if ((netCompositionArea.m_Flags & NetAreaFlags.Invert) != 0) {
                        controlPoint2.m_Direction = -controlPoint2.m_Direction;
                    }

                    var @float = MathUtils.Position(
                        MathUtils.Lerp(
                            segment.m_Left, segment.m_Right,
                            netCompositionArea.m_SnapPosition.x / prefabCompositionData.m_Width + 0.5f), num3);
                    var num4 = math.max(
                        0f,
                        math.min(
                            netCompositionArea.m_Width * 0.5f,
                            math.abs(netCompositionArea.m_SnapPosition.x - netCompositionArea.m_Position.x) +
                            netCompositionArea.m_SnapWidth * 0.5f) - m_LegSnapWidth * 0.5f);
                    controlPoint2.m_Position.xz = controlPoint2.m_Position.xz +
                                                  MathUtils.ClampLength(@float.xz - controlPoint2.m_Position.xz, num4);
                    controlPoint2.m_Position.y = controlPoint2.m_Position.y + netCompositionArea.m_Position.y;
                    var num5 = 1f;
                    if (num2 <= prefabCompositionData.m_Width * 0.5f - math.abs(netCompositionArea.m_Position.x) +
                        m_LegSnapWidth * 0.5f) {
                        num5 = 2f;
                    }

                    controlPoint2.m_Rotation = ToolUtils.CalculateRotation(controlPoint2.m_Direction);
                    controlPoint2.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                        num5, 1f, 1f, controlPoint.m_HitPosition, controlPoint2.m_Position, controlPoint2.m_Direction);
                    //ToolUtils.AddSnapPosition(ref m_BestSnapPosition, controlPoint2);
                    //ToolUtils.AddSnapLine(
                    //    ref m_BestSnapPosition, m_SnapLines,
                    //    new SnapLine(
                    //        controlPoint2, bezier4x, NetToolSystem.SnapJob.GetSnapLineFlags(m_NetGeometryData.m_Flags), 1f));
                    snapAdded = true;
                }

                return flag;
            }
        }
    }
}