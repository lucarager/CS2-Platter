// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Colossal.Collections;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Components;
    using Extensions;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using Block = Game.Zones.Block;
    using SearchSystem = Game.Net.SearchSystem;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Ovverides object placement to snap parcels to road sides 
    /// </summary>
    public partial class P_SnapSystem : GameSystemBase {
        [Flags]
        public enum SnapMode : uint {
            None,
            ZoneSide,
            RoadSide,
        }

        // Data
        private EntityQuery      m_Query;
        private float            m_SnapSetback;
        private ObjectToolSystem m_ObjectToolSystem;
        private PrefabSystem     m_PrefabSystem;

        // Logger
        private PrefixedLogger m_Log;
        private SearchSystem   m_NetSearchSystem;

        // Systems
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private SnapMode                m_SnapMode;
        private TerrainSystem           m_TerrainSystem;
        private ToolSystem              m_ToolSystem;
        private WaterSystem             m_WaterSystem;

        public float CurrentSnapSetback {
            get => m_SnapSetback;
            set {
                m_SnapSetback = value;
                m_ObjectToolSystem.SetMemberValue("m_ForceUpdate", true);
            }
        }

        public static float MinSnapDistance     { get; } = 0f;
        public static float MaxSnapDistance     { get; } = 8f;
        public static float DefaultSnapDistance { get; } = MinSnapDistance;

        // Props
        public SnapMode CurrentSnapMode {
            get => m_SnapMode;
            set => m_SnapMode = value;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneSearchSystem = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_NetSearchSystem  = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PrefabSystem     = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem       = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem    = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem      = World.GetOrCreateSystemManaged<WaterSystem>();

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
            m_SnapSetback = DefaultSnapDistance;
            m_SnapMode    = SnapMode.RoadSide;

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Exit early on certain conditions
            if (m_Query.IsEmptyIgnoreFilter                     ||
                m_ToolSystem.activeTool is not ObjectToolSystem ||
                m_ObjectToolSystem.prefab is not ParcelPlaceholderPrefab) {
                return;
            }

            // Handle vanilla line tool
            if (m_ObjectToolSystem.actualMode is ObjectToolSystem.Mode.Line or ObjectToolSystem.Mode.Curve &&
                m_ObjectToolSystem.prefab is ParcelPlaceholderPrefab parcelPrefab) {
                // Override distance scale
                var width        = parcelPrefab.m_LotWidth * 8f;
                m_ObjectToolSystem.SetMemberValue("distanceScale", width);

                // Exit, we don't want to snap in line mode
                return;
            }

            // Exit on disabled snap
            if (m_SnapMode != SnapMode.ZoneSide && m_SnapMode != SnapMode.RoadSide) {
                return;
            }

            // Grab control points from ObjectTool
            var controlPoints = m_ObjectToolSystem.GetControlPoints(out var deps);
            Dependency = JobHandle.CombineDependencies(Dependency, deps);

            // If none, exit
            if (controlPoints.Length == 0) {
                return;
            }

            var curvesList   = new NativeList<Bezier4x3>(Allocator.Temp);
            var curvesFilter = new NativeList<bool>(Allocator.Temp);

            // Schedule our snapping job
            var parcelSnapJobHandle = new ParcelSnapJob {
                m_ZoneTree                     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneTreeJobHandle),
                m_NetTree                      = m_NetSearchSystem.GetNetSearchTree(true, out var netTreeJobHandle),
                m_CurvesList                   = curvesList,
                m_CurvesFilter                 = curvesFilter,
                m_SnapMode                     = m_SnapMode,
                m_ControlPoints                = controlPoints,
                m_ObjectDefinitionTypeHandle   = SystemAPI.GetComponentTypeHandle<ObjectDefinition>(),
                m_CreationDefinitionTypeHandle = SystemAPI.GetComponentTypeHandle<CreationDefinition>(true),
                m_BlockComponentLookup         = SystemAPI.GetComponentLookup<Block>(true),
                m_ParcelDataComponentLookup    = SystemAPI.GetComponentLookup<ParcelData>(true),
                m_ParcelOwnerComponentLookup   = SystemAPI.GetComponentLookup<ParcelOwner>(true),
                m_NodeLookup                   = SystemAPI.GetComponentLookup<Node>(true),
                m_EdgeLookup                   = SystemAPI.GetComponentLookup<Edge>(true),
                m_CurveLookup                  = SystemAPI.GetComponentLookup<Curve>(true),
                m_CompositionLookup            = SystemAPI.GetComponentLookup<Composition>(true),
                m_PrefabRefLookup              = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_NetDataLookup                = SystemAPI.GetComponentLookup<NetData>(true),
                m_NetGeometryDataLookup        = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                m_NetCompositionDataLookup     = SystemAPI.GetComponentLookup<NetCompositionData>(true),
                m_EdgeGeoLookup                = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                m_StartNodeGeoLookup           = SystemAPI.GetComponentLookup<StartNodeGeometry>(true),
                m_EndNodeGeoLookup             = SystemAPI.GetComponentLookup<EndNodeGeometry>(true),
                m_ConnectedEdgeLookup          = SystemAPI.GetBufferLookup<ConnectedEdge>(),
                m_TerrainHeightData            = m_TerrainSystem.GetHeightData(),
                m_WaterSurfaceData             = m_WaterSystem.GetSurfaceData(out var waterSurfaceJobHandle),
                m_SnapSetback                  = m_SnapSetback,
                m_EntityTypeHandle             = SystemAPI.GetEntityTypeHandle(),
                m_ConnectedParcelLookup        = SystemAPI.GetBufferLookup<ConnectedParcel>(true),
            }.ScheduleParallel(
                m_Query,
                JobUtils.CombineDependencies(Dependency, zoneTreeJobHandle, netTreeJobHandle, waterSurfaceJobHandle)
            );

            m_ZoneSearchSystem.AddSearchTreeReader(parcelSnapJobHandle);
            m_NetSearchSystem.AddNetSearchTreeReader(parcelSnapJobHandle);
            m_TerrainSystem.AddCPUHeightReader(parcelSnapJobHandle);
            m_WaterSystem.AddSurfaceReader(parcelSnapJobHandle);

            curvesList.Dispose(parcelSnapJobHandle);
            curvesFilter.Dispose(parcelSnapJobHandle);

            // Register deps
            Dependency = JobHandle.CombineDependencies(Dependency, parcelSnapJobHandle);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct ParcelSnapJob : IJobChunk {
            [ReadOnly] public required NativeQuadTree<Entity, Bounds2>          m_ZoneTree;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetTree;
            [ReadOnly] public required TerrainHeightData                        m_TerrainHeightData;
            [ReadOnly] public required WaterSurfaceData<SurfaceWater>           m_WaterSurfaceData;
            [ReadOnly] public required NativeList<ControlPoint>                 m_ControlPoints;
            [ReadOnly] public required ComponentTypeHandle<CreationDefinition>  m_CreationDefinitionTypeHandle;
            [ReadOnly] public required ComponentLookup<Block>                   m_BlockComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelOwner>             m_ParcelOwnerComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] public required float                                    m_SnapSetback;
            [ReadOnly] public required EntityTypeHandle                         m_EntityTypeHandle;
            [ReadOnly] public required ComponentLookup<Node>                    m_NodeLookup;
            [ReadOnly] public required ComponentLookup<Edge>                    m_EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>                   m_CurveLookup;
            [ReadOnly] public required ComponentLookup<Composition>             m_CompositionLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>               m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<NetData>                 m_NetDataLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData>         m_NetGeometryDataLookup;
            [ReadOnly] public required ComponentLookup<NetCompositionData>      m_NetCompositionDataLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>            m_EdgeGeoLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry>       m_StartNodeGeoLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry>         m_EndNodeGeoLookup;
            [ReadOnly] public required BufferLookup<ConnectedParcel>            m_ConnectedParcelLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>              m_ConnectedEdgeLookup;
            [ReadOnly] public required SnapMode                                 m_SnapMode;
            public required            NativeList<Bezier4x3>                    m_CurvesList;
            public required            NativeList<bool>                         m_CurvesFilter;
            public required            ComponentTypeHandle<ObjectDefinition>    m_ObjectDefinitionTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray             = chunk.GetNativeArray(m_EntityTypeHandle);
                var objectDefinitionArray   = chunk.GetNativeArray(ref m_ObjectDefinitionTypeHandle);
                var creationDefinitionArray = chunk.GetNativeArray(ref m_CreationDefinitionTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
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
                    var totalBounds = bounds;
                    totalBounds.min -= 64f;
                    totalBounds.max += 64f;

                    // At minimum, the distance to snap must be
                    var minDistance = math.cmin(parcelData.m_LotSize) * 4f + 16f;

                    // Default to our control point as a start
                    var bestSnapPosition = controlPoint;

                    if (m_SnapMode == SnapMode.ZoneSide) {
                        var iterator = new BlockSnapIterator(
                            controlPoint: controlPoint,
                            bestSnapPosition: bestSnapPosition,
                            bounds: bounds.xz,
                            blockComponentLookup: m_BlockComponentLookup,
                            parcelOwnerComponentLookup: m_ParcelOwnerComponentLookup,
                            bestDistance: minDistance,
                            lotSize: parcelData.m_LotSize,
                            snapSetback: m_SnapSetback
                        );
                        m_ZoneTree.Iterate(ref iterator);
                        bestSnapPosition = iterator.BestSnapPosition;
                    } else if (m_SnapMode == SnapMode.RoadSide) {
                        var iterator = new EdgeSnapIterator(
                            minDistance,
                            m_CurvesList,
                            m_CurvesFilter,
                            totalBounds,
                            bounds,
                            20f,
                            controlPoint.m_Elevation,
                            bestSnapPosition: bestSnapPosition,
                            lotSize: parcelData.m_LotSize,
                            heightRange: new Bounds1(-50f, 50f),
                            netData: default,
                            controlPoint: controlPoint,
                            terrainHeightData: m_TerrainHeightData,
                            waterSurfaceData: m_WaterSurfaceData,
                            nodeLookup: m_NodeLookup,
                            edgeLookup: m_EdgeLookup,
                            curveLookup: m_CurveLookup,
                            compositionLookup: m_CompositionLookup,
                            prefabRefLookup: m_PrefabRefLookup,
                            prefabNetLookup: m_NetDataLookup,
                            netGeometryDataLookup: m_NetGeometryDataLookup,
                            netCompositionDataLookup: m_NetCompositionDataLookup,
                            edgeGeoLookup: m_EdgeGeoLookup,
                            startNodeGeoLookup: m_StartNodeGeoLookup,
                            endNodeGeoLookup: m_EndNodeGeoLookup,
                            connectedEdgeLookup: m_ConnectedEdgeLookup,
                            snapSetback: m_SnapSetback,
                            connectedParcelLookup: m_ConnectedParcelLookup
                        );
                        m_NetTree.Iterate(ref iterator);

                        bestSnapPosition = iterator.BestSnapPosition;
                    }

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

            private void CalculateHeight(ref ControlPoint controlPoint, ParcelData parcelData,
                                         float            waterSurfaceHeight) {
                var parcelFrontPosition = BuildingUtils.CalculateFrontPosition(
                    new Transform(controlPoint.m_Position, controlPoint.m_Rotation),
                    parcelData.m_LotSize.y);
                var targetHeight = TerrainUtils.SampleHeight(ref m_TerrainHeightData, parcelFrontPosition);
                controlPoint.m_Position.y = targetHeight;
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
                private float                        m_SnapSetback;

                public BlockSnapIterator(ComponentLookup<ParcelOwner> parcelOwnerComponentLookup,
                                         ComponentLookup<Block>       blockComponentLookup,
                                         ControlPoint                 controlPoint,
                                         int2                         lotSize,
                                         Bounds2                      bounds,
                                         ControlPoint                 bestSnapPosition,
                                         float                        bestDistance,
                                         float                        snapSetback) {
                    m_ParcelOwnerComponentLookup = parcelOwnerComponentLookup;
                    m_BlockComponentLookup       = blockComponentLookup;
                    m_ControlPoint               = controlPoint;
                    m_LotSize                    = lotSize;
                    m_Bounds                     = bounds;
                    m_BestSnapPosition           = bestSnapPosition;
                    m_BestDistance               = bestDistance;
                    m_SnapSetback                = snapSetback;
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
                    var depthAlignment = blockDepth - forwardOffset - m_SnapSetback - m_LotSize.y * 4f;
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
                public ControlPoint BestSnapPosition => m_BestSnapPosition;

                private Bounds3                             m_TotalBounds;
                private Bounds3                             m_Bounds;
                private float                               m_SnapOffset;
                private float                               m_Elevation;
                private Bounds1                             m_HeightRange;
                private NetData                             m_NetData;
                private ControlPoint                        m_ControlPoint;
                private ControlPoint                        m_BestSnapPosition;
                private int2                                m_LotSize;
                private TerrainHeightData                   m_TerrainHeightData;
                private WaterSurfaceData<SurfaceWater>      m_WaterSurfaceData;
                private ComponentLookup<Node>               m_NodeLookup;
                private ComponentLookup<Edge>               m_EdgeLookup;
                private ComponentLookup<Curve>              m_CurveLookup;
                private BufferLookup<ConnectedParcel>       m_ConnectedParcelLookup;
                private ComponentLookup<Composition>        m_CompositionLookup;
                private ComponentLookup<PrefabRef>          m_PrefabRefLookup;
                private ComponentLookup<NetData>            m_PrefabNetLookup;
                private ComponentLookup<NetGeometryData>    m_NetGeometryDataLookup;
                private ComponentLookup<NetCompositionData> m_NetCompositionDataLookup;
                private ComponentLookup<EdgeGeometry>       m_EdgeGeoLookup;
                private ComponentLookup<StartNodeGeometry>  m_StartNodeGeoLookup;
                private ComponentLookup<EndNodeGeometry>    m_EndNodeGeoLookup;
                private BufferLookup<ConnectedEdge>         m_ConnectedEdgeLookup;
                private float                               m_SnapSetback;
                private NativeList<Bezier4x3>               m_CurvesList;
                private NativeList<bool>                    m_CurvesFilter;
                private float                               m_BestDistance;

                public EdgeSnapIterator(float                               bestDistance, NativeList<Bezier4x3> curvesList, NativeList<bool> curvesFilter,
                                        Bounds3                             totalBounds,  Bounds3               bounds,     float            snapOffset,
                                        float                               elevation,    int2                  lotSize,
                                        Bounds1                             heightRange,  NetData               netData,
                                        ControlPoint                        controlPoint, ControlPoint          bestSnapPosition,
                                        TerrainHeightData                   terrainHeightData,
                                        WaterSurfaceData<SurfaceWater>      waterSurfaceData, ComponentLookup<Node>  nodeLookup,
                                        ComponentLookup<Edge>               edgeLookup,       ComponentLookup<Curve> curveLookup,
                                        ComponentLookup<Composition>        compositionLookup,
                                        ComponentLookup<PrefabRef>          prefabRefLookup,
                                        ComponentLookup<NetData>            prefabNetLookup,
                                        ComponentLookup<NetGeometryData>    netGeometryDataLookup,
                                        ComponentLookup<NetCompositionData> netCompositionDataLookup,
                                        ComponentLookup<EdgeGeometry>       edgeGeoLookup,
                                        ComponentLookup<StartNodeGeometry>  startNodeGeoLookup,
                                        ComponentLookup<EndNodeGeometry>    endNodeGeoLookup,
                                        BufferLookup<ConnectedEdge>         connectedEdgeLookup, float snapSetback,
                                        BufferLookup<ConnectedParcel>       connectedParcelLookup) {
                    m_BestDistance             = bestDistance;
                    m_CurvesList               = curvesList;
                    m_CurvesFilter             = curvesFilter;
                    m_TotalBounds              = totalBounds;
                    m_Bounds                   = bounds;
                    m_SnapOffset               = snapOffset;
                    m_Elevation                = elevation;
                    m_HeightRange              = heightRange;
                    m_NetData                  = netData;
                    m_ControlPoint             = controlPoint;
                    m_LotSize                  = lotSize;
                    m_BestSnapPosition         = bestSnapPosition;
                    m_TerrainHeightData        = terrainHeightData;
                    m_WaterSurfaceData         = waterSurfaceData;
                    m_NodeLookup               = nodeLookup;
                    m_EdgeLookup               = edgeLookup;
                    m_CurveLookup              = curveLookup;
                    m_CompositionLookup        = compositionLookup;
                    m_PrefabRefLookup          = prefabRefLookup;
                    m_PrefabNetLookup          = prefabNetLookup;
                    m_NetGeometryDataLookup    = netGeometryDataLookup;
                    m_NetCompositionDataLookup = netCompositionDataLookup;
                    m_EdgeGeoLookup            = edgeGeoLookup;
                    m_StartNodeGeoLookup       = startNodeGeoLookup;
                    m_EndNodeGeoLookup         = endNodeGeoLookup;
                    m_ConnectedEdgeLookup      = connectedEdgeLookup;
                    m_SnapSetback              = snapSetback;
                    m_ConnectedParcelLookup    = connectedParcelLookup;
                }

                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds); }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds)) {
                        return;
                    }

                    if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && HandleGeometry(entity)) { }
                }

                private bool HandleGeometry(Entity entity) {
                    var prefabRef    = m_PrefabRefLookup[entity];
                    var controlPoint = m_ControlPoint;
                    controlPoint.m_OriginalEntity = entity;

                    var distance  = m_SnapOffset;
                    var isNode    = m_ConnectedEdgeLookup.HasBuffer(entity);
                    var isCurve   = m_CurveLookup.HasComponent(entity);
                    var isZonable = m_ConnectedParcelLookup.HasBuffer(entity);

                    if (!isZonable) {
                        return false;
                    }

                    if (isNode) {
                        var node                = m_NodeLookup[entity];
                        var connectedEdgeBuffer = m_ConnectedEdgeLookup[entity];

                        for (var i = 0; i < connectedEdgeBuffer.Length; i++) {
                            var edge = m_EdgeLookup[connectedEdgeBuffer[i].m_Edge];

                            if (edge.m_Start == entity || edge.m_End == entity) {
                                return false;
                            }
                        }

                        if (!m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                            return !(math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >= distance) &&
                                   HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                        }

                        var netGeometryData2 = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                        distance += netGeometryData2.m_DefaultWidth * 0.5f;

                        return !(math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >= distance) &&
                               HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                    }

                    if (!isCurve) {
                        return false;
                    }

                    var curve = m_CurveLookup[entity];

                    if (m_CompositionLookup.HasComponent(entity)) {
                        var composition        = m_CompositionLookup[entity];
                        var netCompositionData = m_NetCompositionDataLookup[composition.m_Edge];
                        distance += netCompositionData.m_Width * 0.5f;
                    }

                    if (MathUtils.Distance(
                            curve.m_Bezier.xz,
                            m_ControlPoint.m_HitPosition.xz,
                            out controlPoint.m_CurvePosition) >=
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

                    var   snapAdded = false;
                    var   flag2     = true;
                    var   flag3     = true;
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
                        var bounds          = new Bounds1(height);
                        var bounds2         = netGeometryData.m_DefaultHeightRange + snapHeight;
                        if (!MathUtils.Intersect(bounds, bounds2)) {
                            flag2 = false;
                            flag3 = (netGeometryData.m_Flags & GeometryFlags.NoEdgeConnection) == 0;
                        }
                    }

                    if (flag2 && !NetUtils.CanConnect(netData, m_NetData)) {
                        return snapAdded;
                    }

                    if ((m_NetData.m_ConnectLayers & ~netData.m_RequiredLayers & Layer.LaneEditor) != Layer.None) {
                        return snapAdded;
                    }

                    var num2 = snapHeight - height;

                    if (!ignoreHeightDistance && !MathUtils.Intersect(m_HeightRange, num2)) {
                        return snapAdded;
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
                                        HandleCurve(controlPoint, edge, flag3, ref snapAdded);
                                    }
                                }

                                return snapAdded;
                            }
                        }

                        var controlPoint2 = controlPoint;
                        var node          = m_NodeLookup[controlPoint.m_OriginalEntity];
                        controlPoint2.m_Position  = node.m_Position;
                        controlPoint2.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                        MathUtils.TryNormalize(ref controlPoint2.m_Direction);
                        var num3 = 1f;

                        controlPoint2.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                            num3,
                            1f,
                            1f,
                            controlPoint.m_HitPosition,
                            controlPoint2.m_Position,
                            controlPoint2.m_Direction);
                        ToolUtils.AddSnapPosition(ref m_BestSnapPosition, controlPoint2);
                        snapAdded = true;
                    } else if (m_CurveLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        HandleCurve(controlPoint, controlPoint.m_OriginalEntity, flag3, ref snapAdded);
                    }

                    return snapAdded;
                }

                private void HandleCurve(ControlPoint controlPoint, Entity curveEntity, bool allowEdgeSnap, ref bool snapAdded) {
                    // When you're here, it means we found a valid road edge.
                    var edgeGeo      = m_EdgeGeoLookup[curveEntity];
                    var startNodeGeo = m_StartNodeGeoLookup[curveEntity];
                    var endNodeGeo   = m_EndNodeGeoLookup[curveEntity];
                    var edge         = m_EdgeLookup[curveEntity];

                    var startIsConnected = m_ConnectedEdgeLookup[edge.m_Start].Length > 1;
                    var endIsConnected   = m_ConnectedEdgeLookup[edge.m_End].Length   > 1;

                    // Gather all curves for this road.
                    m_CurvesFilter.Clear();
                    m_CurvesList.Clear();

                    m_CurvesList.Add(edgeGeo.m_Start.m_Left);
                    m_CurvesFilter.Add(true);
                    m_CurvesList.Add(edgeGeo.m_Start.m_Right);
                    m_CurvesFilter.Add(true);
                    m_CurvesList.Add(edgeGeo.m_End.m_Left);
                    m_CurvesFilter.Add(true);
                    m_CurvesList.Add(edgeGeo.m_End.m_Right);
                    m_CurvesFilter.Add(true);
                    m_CurvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Left);
                    m_CurvesFilter.Add(startNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    m_CurvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Right);
                    m_CurvesFilter.Add(
                        startNodeGeo.m_Geometry.m_Left.m_Length.y > 1 &&
                        !startIsConnected); // This shifts to the middle lane when road is connected
                    m_CurvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Left);
                    m_CurvesFilter.Add(
                        startNodeGeo.m_Geometry.m_Right.m_Length.x > 1 &&
                        !startIsConnected); // This shifts to the middle lane when road is connected
                    m_CurvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Right);
                    m_CurvesFilter.Add(startNodeGeo.m_Geometry.m_Right.m_Length.y > 1);
                    m_CurvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Left);
                    m_CurvesFilter.Add(endNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    m_CurvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Right);
                    m_CurvesFilter.Add(
                        endNodeGeo.m_Geometry.m_Left.m_Length.y > 1 &&
                        !endIsConnected); // This shifts to the middle lane when road is connected
                    m_CurvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Left);
                    m_CurvesFilter.Add(
                        endNodeGeo.m_Geometry.m_Right.m_Length.x > 1 &&
                        !endIsConnected); // This shifts to the middle lane when road is connected
                    m_CurvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Right);
                    m_CurvesFilter.Add(endNodeGeo.m_Geometry.m_Right.m_Length.y > 1);

                    // Find curve closes to our control point.
                    var closestCurve = default(Bezier4x3);
                    var closestPoint = default(float);
                    var curveIndex   = -1;

                    for (var i = 0; i < m_CurvesList.Length; i++) {
                        var curve  = m_CurvesList[i];
                        var filter = m_CurvesFilter[i];

                        if (!filter) {
                            continue;
                        }

                        // Calculate the distance from the control point to the curve
                        var distance = MathUtils.Distance(curve.xz, controlPoint.m_HitPosition.xz, out var t);

                        if (!(distance < m_BestDistance)) {
                            continue;
                        }

                        // If this curve is closer, update m_BestSnapPoint
                        m_BestDistance = distance;
                        closestCurve   = curve;
                        closestPoint   = t;
                        curveIndex     = i;
                    }

                    if (closestCurve.Equals(default)) {
                        return;
                    }

                    // Determine what direction we need to rotate
                    var useRight = closestCurve.Equals(edgeGeo.m_Start.m_Left)                 ||
                                   closestCurve.Equals(edgeGeo.m_End.m_Left)                   ||
                                   closestCurve.Equals(startNodeGeo.m_Geometry.m_Left.m_Left)  ||
                                   closestCurve.Equals(startNodeGeo.m_Geometry.m_Right.m_Left) ||
                                   closestCurve.Equals(endNodeGeo.m_Geometry.m_Left.m_Left)    ||
                                   closestCurve.Equals(endNodeGeo.m_Geometry.m_Right.m_Left);

                    var tangent = MathUtils.Tangent(closestCurve, closestPoint);

                    m_BestSnapPosition.m_Direction = useRight ?
                        MathUtils.Right(tangent.xz) :
                        MathUtils.Left(tangent.xz);

                    MathUtils.TryNormalize(ref m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_Rotation = ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_Position = MathUtils.Position(closestCurve, closestPoint);

                    // Shift back by lot half depth
                    m_BestSnapPosition.m_Position.xz -= m_BestSnapPosition.m_Direction * m_LotSize.y * 4f;

                    // Apply the snap setback along the perpendicular axis
                    m_BestSnapPosition.m_Position.xz -= m_BestSnapPosition.m_Direction * m_SnapSetback;
                }
            }
        }
    }
}