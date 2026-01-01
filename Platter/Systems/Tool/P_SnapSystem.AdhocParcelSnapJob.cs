// <copyright file="P_SnapSystem.ParcelSnapJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
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
    using UnityEngine;
    using Utils;
    using Block = Game.Zones.Block;
    using Transform = Game.Objects.Transform;

    #endregion

    public partial class P_SnapSystem {
#if USE_BURST
        [BurstCompile]
#endif
        public struct AdhocParcelSnapJob : IJob {
            [ReadOnly] public required Entity                                   m_PrefabEntity;
            [ReadOnly] public required NativeQuadTree<Entity, Bounds2>          m_ZoneTree;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetTree;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ParcelTree;
            [ReadOnly] public required TerrainHeightData                        m_TerrainHeightData;
            [ReadOnly] public required WaterSurfaceData<SurfaceWater>           m_WaterSurfaceData;
            [ReadOnly] public required NativeList<ControlPoint>                 m_ControlPoints;
            [ReadOnly] public required ComponentLookup<Block>                   m_BlockComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelOwner>             m_ParcelOwnerComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>              m_ParcelDataComponentLookup;
            [ReadOnly] public required ComponentLookup<Transform>               m_TransformComponentLookup;
            [ReadOnly] public required ComponentLookup<Parcel>                  m_ParcelComponentLookup;
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
            [ReadOnly] public required BufferLookup<SubBlock>                   m_SubBlockLookup;
            [ReadOnly] public required SnapMode                                 m_SnapMode;
            public required            NativeReference<bool>                    m_IsSnapped;

            public void Execute() {
                // Process last control point
                var controlPoint = this.m_ControlPoints[^1];

                // Get some data
                var parcelData = m_ParcelDataComponentLookup[m_PrefabEntity];
                var searchRadius = parcelData.m_LotSize.y * 4f + 16f;
                var bounds = new Bounds3(
                                         controlPoint.m_Position - searchRadius,
                                         controlPoint.m_Position + searchRadius
                                        );
                var totalBounds = bounds;
                totalBounds.min -= 10f;
                totalBounds.max += 10f;

                // Start with the raw control point (no snap)
                var bestSnapPosition = controlPoint;
                bestSnapPosition.m_SnapPriority = float2.zero; // No snap = lowest priority

                // Snap Start
                // ========================

                // Run ALL enabled snap modes - each one only updates bestSnapPosition if it wins
                if ((m_SnapMode & SnapMode.ZoneSide) != 0) {
                    SnapToZoneSide(ref bestSnapPosition, controlPoint, bounds, parcelData, math.cmin(parcelData.m_LotSize) * 4f + 4f);
                }

                if ((m_SnapMode & SnapMode.RoadSide) != 0) {
                    SnapToRoadSide(ref bestSnapPosition, controlPoint, bounds, totalBounds, parcelData, math.cmin(parcelData.m_LotSize) * 4f + 4f);
                }

                // Parcel-to-parcel snapping (edge and/or front alignment)
                var enableParcelEdge       = (m_SnapMode & SnapMode.ParcelEdge)       != 0;
                var enableParcelFrontAlign = (m_SnapMode & SnapMode.ParcelFrontAlign) != 0;
                if (enableParcelEdge || enableParcelFrontAlign) {
                    SnapToParcelEdge(ref bestSnapPosition, controlPoint, bounds, parcelData, math.cmax(parcelData.m_LotSize) * 4f + 4f, enableParcelEdge, enableParcelFrontAlign);
                }

                // Snap End
                // ========================

                // Check if we snapped
                var isSnapped = !controlPoint.m_Position.Equals(bestSnapPosition.m_Position) ||
                                !controlPoint.m_Rotation.Equals(bestSnapPosition.m_Rotation) ||
                                !controlPoint.m_Direction.Equals(bestSnapPosition.m_Direction);
                m_IsSnapped.Value = isSnapped;

                // Exit early on no snap
                if (!isSnapped) {
                    return;
                }

                // Calculate height
                // todo do we need this?
                CalculateHeight(ref bestSnapPosition, parcelData, float.MinValue);

                // Update control point
                m_ControlPoints[^1] = bestSnapPosition;
            }

            private void SnapToZoneSide(ref ControlPoint bestSnapPosition,
                                        ControlPoint     controlPoint,
                                        Bounds3          bounds,
                                        ParcelData       parcelData,
                                        float            minDistance) {
                var iterator = new BlockSnapIterator(
                    controlPoint: controlPoint,
                    bestSnapPosition: controlPoint, // Start fresh for this iterator
                    bounds: bounds.xz,
                    blockComponentLookup: m_BlockComponentLookup,
                    parcelOwnerComponentLookup: m_ParcelOwnerComponentLookup,
                    bestDistance: minDistance,
                    lotSize: parcelData.m_LotSize,
                    snapSetback: m_SnapSetback,
                    snapLevel: SnapLevel.ZoneSide
                );

                m_ZoneTree.Iterate(ref iterator);

                // Only update if this snap beats the current best
                AddSnapPosition(ref bestSnapPosition, iterator.BestSnapPosition);
            }

            private void SnapToRoadSide(ref ControlPoint bestSnapPosition,
                                        ControlPoint     controlPoint,
                                        Bounds3          bounds,
                                        Bounds3          totalBounds,
                                        ParcelData       parcelData,
                                        float            minDistance) {
                var iterator = new EdgeSnapIterator {
                    m_TotalBounds              = totalBounds,
                    m_Bounds                   = bounds,
                    m_SnapOffset               = 20f,
                    m_Elevation                = controlPoint.m_Elevation,
                    m_HeightRange              = new Bounds1(-20f, 20f),
                    m_NetData                  = default,
                    m_ControlPoint             = controlPoint,
                    m_BestSnapPosition         = bestSnapPosition,
                    m_LotSize                  = parcelData.m_LotSize,
                    m_TerrainHeightData        = m_TerrainHeightData,
                    m_WaterSurfaceData         = m_WaterSurfaceData         ,
                    m_NodeLookup               = m_NodeLookup               ,
                    m_EdgeLookup               = m_EdgeLookup               ,
                    m_CurveLookup              = m_CurveLookup              ,
                    m_SubBlockLookup           = m_SubBlockLookup,
                    m_ConnectedParcelLookup    = m_ConnectedParcelLookup,
                    m_CompositionLookup        = m_CompositionLookup        ,
                    m_PrefabRefLookup          = m_PrefabRefLookup          ,
                    m_NetDataLookup            = m_NetDataLookup,
                    m_NetGeometryDataLookup    = m_NetGeometryDataLookup    ,
                    m_NetCompositionDataLookup = m_NetCompositionDataLookup ,
                    m_EdgeGeoLookup            = m_EdgeGeoLookup            ,
                    m_StartNodeGeoLookup       = m_StartNodeGeoLookup       ,
                    m_EndNodeGeoLookup         = m_EndNodeGeoLookup         ,
                    m_ConnectedEdgeLookup      = m_ConnectedEdgeLookup      ,
                    m_SnapSetback              = m_SnapSetback              ,
                    m_BestDistance             = minDistance,
                    m_SnapLevel                = SnapLevel.RoadSide,
                };
                    
                m_NetTree.Iterate(ref iterator);

                AddSnapPosition(ref bestSnapPosition, iterator.BestSnapPosition);
            }

            private void SnapToParcelEdge(ref ControlPoint bestSnapPosition,
                                          ControlPoint     controlPoint,
                                          Bounds3          bounds,
                                          ParcelData       parcelData,
                                          float            minDistance,
                                          bool             enableEdgeSnap,
                                          bool             enableFrontAlign) {
                var iterator = new ParcelEdgeIterator(
                    controlPoint,
                    controlPoint,
                    bounds,
                    parcelData,
                    minDistance,
                    enableEdgeSnap,
                    enableFrontAlign,
                    SnapLevel.ParcelEdge,
                    SnapLevel.ParcelFrontAlign,
                    m_TransformComponentLookup,
                    m_PrefabRefLookup,
                    m_ParcelDataComponentLookup,
                    m_ParcelComponentLookup
                );

                m_ParcelTree.Iterate(ref iterator);

                AddSnapPosition(ref bestSnapPosition, iterator.BestSnapPosition);
            }

            // Helper method matching game's pattern
            private static void AddSnapPosition(ref ControlPoint bestSnapPosition, ControlPoint candidate) {
                if (CompareSnapPriority(candidate.m_SnapPriority, bestSnapPosition.m_SnapPriority)) {
                    bestSnapPosition = candidate;
                }
            }

            // Match game's comparison logic
            private static bool CompareSnapPriority(float2 a, float2 b) {
                // Higher level always wins; if equal level, compare priority
                return a.x > b.x || (a.x == b.x && a.y > b.y);
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
                private float                        m_SnapLevel;
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
                                         float                        snapSetback,
                                         float                        snapLevel) {
                    m_ParcelOwnerComponentLookup = parcelOwnerComponentLookup;
                    m_BlockComponentLookup       = blockComponentLookup;
                    m_ControlPoint               = controlPoint;
                    m_LotSize                    = lotSize;
                    m_Bounds                     = bounds;
                    m_BestSnapPosition           = bestSnapPosition;
                    m_BestDistance               = bestDistance;
                    m_SnapSetback                = snapSetback;
                    m_SnapLevel                  = snapLevel;
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
                    // The search line extends perpendicular to the parcel's facing direction
                    var lotDepthDifference = math.max(0f, m_LotSize.y - m_LotSize.x) * 4f;
                    var parcelLeftDirection = MathUtils.Left(m_ControlPoint.m_Direction);
                    searchLine.a -= parcelLeftDirection * lotDepthDifference;
                    searchLine.b += parcelLeftDirection * lotDepthDifference;

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
                    var candidate = m_ControlPoint;
                    candidate.m_Position = m_ControlPoint.m_HitPosition;

                    // Align new lot behind the existing block's front edge
                    var depthAlignment = blockDepth - forwardOffset - m_SnapSetback - m_LotSize.y * 4f;
                    candidate.m_Position.xz += blockForwardDirection * depthAlignment;

                    // Apply lateral grid snapping
                    candidate.m_Position.xz -= blockLeftDirection * lateralOffset;

                    // Set direction and rotation to match the block
                    candidate.m_Direction = block.m_Direction;
                    candidate.m_Rotation  = ToolUtils.CalculateRotation(candidate.m_Direction);

                    // Calculate snap priority
                    candidate.m_SnapPriority = CalculateSnapPriority(
                        m_SnapLevel,
                        1f,
                        1f,
                        m_ControlPoint.m_HitPosition,
                        candidate.m_Position,
                        candidate.m_Direction
                    );

                    // Cache block
                    //candidate.m_OriginalEntity = blockEntity;

                    // Store candidate
                    m_BestSnapPosition = candidate;
                }
            }

            /// <summary>
            /// QuadTree iterator.
            /// </summary>
            public struct EdgeSnapIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public          ControlPoint                        BestSnapPosition => m_BestSnapPosition;
                public required Bounds3                             m_TotalBounds;
                public required Bounds3                             m_Bounds;
                public required float                               m_SnapOffset;
                public required float                               m_Elevation;
                public required Bounds1                             m_HeightRange;
                public required NetData                             m_NetData;
                public required ControlPoint                        m_ControlPoint;
                public required ControlPoint                        m_BestSnapPosition;
                public required int2                                m_LotSize;
                public required TerrainHeightData                   m_TerrainHeightData;
                public required WaterSurfaceData<SurfaceWater>      m_WaterSurfaceData;
                public required ComponentLookup<Node>               m_NodeLookup;
                public required ComponentLookup<Edge>               m_EdgeLookup;
                public required ComponentLookup<Curve>              m_CurveLookup;
                public required BufferLookup<ConnectedParcel>       m_ConnectedParcelLookup;
                public required BufferLookup<SubBlock>              m_SubBlockLookup;
                public required ComponentLookup<Composition>        m_CompositionLookup;
                public required ComponentLookup<PrefabRef>          m_PrefabRefLookup;
                public required ComponentLookup<NetData>            m_NetDataLookup;
                public required ComponentLookup<NetGeometryData>    m_NetGeometryDataLookup;
                public required ComponentLookup<NetCompositionData> m_NetCompositionDataLookup;
                public required ComponentLookup<EdgeGeometry>       m_EdgeGeoLookup;
                public required ComponentLookup<StartNodeGeometry>  m_StartNodeGeoLookup;
                public required ComponentLookup<EndNodeGeometry>    m_EndNodeGeoLookup;
                public required BufferLookup<ConnectedEdge>         m_ConnectedEdgeLookup;
                public required float                               m_SnapSetback;
                public required float                               m_BestDistance;
                public required float                               m_SnapLevel;

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
                    var isValidRoad = m_SubBlockLookup.HasBuffer(entity) && m_ConnectedParcelLookup.HasBuffer(entity);

                    if (!isValidRoad) {
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
                    if (!m_NetDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        return false;
                    }

                    var netData = m_NetDataLookup[prefabRef.m_Prefab];

                    var   snapAdded = false;
                    var   flag2     = true;
                    var   allowEdgeSnap     = true;
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
                            allowEdgeSnap = (netGeometryData.m_Flags & Game.Net.GeometryFlags.NoEdgeConnection) == 0;
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
                                        HandleCurve(controlPoint, edge, allowEdgeSnap, ref snapAdded);
                                    }
                                }

                                return snapAdded;
                            }
                        }

                        var candidate = controlPoint;
                        var node      = m_NodeLookup[controlPoint.m_OriginalEntity];
                        candidate.m_Position  = node.m_Position;
                        candidate.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                        MathUtils.TryNormalize(ref candidate.m_Direction);

                        candidate.m_SnapPriority = CalculateSnapPriority(
                            m_SnapLevel,
                            1f,
                            1f,
                            m_ControlPoint.m_HitPosition,
                            controlPoint.m_Position,
                            controlPoint.m_Direction
                        );

                        ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);

                        snapAdded = true;
                    } else if (m_CurveLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        HandleCurve(controlPoint, controlPoint.m_OriginalEntity, allowEdgeSnap, ref snapAdded);
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

                    var curvesList   = new NativeList<Bezier4x3>(Allocator.Temp);
                    var curvesFilter = new NativeList<bool>(Allocator.Temp);

                    curvesList.Add(edgeGeo.m_Start.m_Left);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_Start.m_Right);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_End.m_Left);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_End.m_Right);
                    curvesFilter.Add(true);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Left);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Right);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Left.m_Length.y > 1 && !startIsConnected);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Left);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Right.m_Length.x > 1 && !startIsConnected);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Right);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Right.m_Length.y > 1);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Left);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Right);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Left.m_Length.y > 1 && !endIsConnected);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Left);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Right.m_Length.x > 1 && !endIsConnected);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Right);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Right.m_Length.y > 1);

                    // Find curve closes to our control point.
                    var closestCurve = default(Bezier4x3);
                    var closestPoint = default(float);

                    for (var i = 0; i < curvesList.Length; i++) {
                        var curve  = curvesList[i];
                        var filter = curvesFilter[i];

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
                    }

                    if (closestCurve.Equals(default)) {
                        curvesList.Dispose();
                        curvesFilter.Dispose();
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

                    // Shift back to center on the curve
                    m_BestSnapPosition.m_Position.xz -= m_BestSnapPosition.m_Direction * m_LotSize.y * 4f;

                    // Apply the snap setback along the perpendicular axis
                    m_BestSnapPosition.m_Position.xz -= m_BestSnapPosition.m_Direction * m_SnapSetback;

                    // Calculate and set snap priority
                    m_BestSnapPosition.m_SnapPriority = CalculateSnapPriority(
                        m_SnapLevel,
                        1f,
                        1f,
                        m_ControlPoint.m_HitPosition,
                        m_BestSnapPosition.m_Position,
                        m_BestSnapPosition.m_Direction
                    );

                    //m_BestSnapPosition.m_OriginalEntity = curveEntity;
                    snapAdded                           = true;

                    curvesList.Dispose();
                    curvesFilter.Dispose();
                }
            }

            // Simplified version of ToolUtils.CalculateSnapPriority
            private static float2 CalculateSnapPriority(float  level,
                                                        float  basePriority,
                                                        float  heightWeight,
                                                        float3 hitPosition,
                                                        float3 snapPosition,
                                                        float2 snapDirection) {
                var offset = math.abs(snapPosition - hitPosition) / 8f;
                offset *= offset;

                var horizontal = math.min(1f, offset.x + offset.z);
                var diagonal   = math.max(offset.x, offset.z) + math.min(offset.x, offset.z) * 0.001f;

                var priority = basePriority * (2f - horizontal - diagonal) / (1f + offset.y * heightWeight);

                return new float2(level, priority);
            }

            /// <summary>
            /// QuadTree iterator for parcel-to-parcel snapping.
            /// Supports both edge snapping (sides slide along each other) and front corner alignment (locked corners).
            /// </summary>
            public struct ParcelEdgeIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public  ControlPoint                BestSnapPosition => m_BestSnapPosition;
                private ControlPoint                m_ControlPoint;
                private ControlPoint                m_BestSnapPosition;
                private Bounds3                     m_Bounds;
                private ParcelData                  m_NewParcelData;
                private int2                        m_LotSize;
                private float3                      m_NewParcelSize;
                private float                       m_BestDistance;
                private float                       m_SnapLevelEdge;
                private float                       m_SnapLevelFrontAlign;
                private bool                        m_EnableEdgeSnap;
                private bool                        m_EnableFrontAlign;
                private ComponentLookup<Transform>  m_TransformLookup;
                private ComponentLookup<PrefabRef>  m_PrefabRefLookup;
                private ComponentLookup<ParcelData> m_ParcelDataLookup;
                private ComponentLookup<Parcel>     m_ParcelLookup;

                public ParcelEdgeIterator(ControlPoint                controlPoint,
                                          ControlPoint                bestSnapPosition,
                                          Bounds3                     bounds,
                                          ParcelData                        newParcelData,
                                          float                       bestDistance,
                                          bool                        enableEdgeSnap,
                                          bool                        enableFrontAlign,
                                          float                       snapLevelEdge,
                                          float                       snapLevelFrontAlign,
                                          ComponentLookup<Transform>  transformLookup,
                                          ComponentLookup<PrefabRef>  prefabRefLookup,
                                          ComponentLookup<ParcelData> parcelDataLookup,
                                          ComponentLookup<Parcel>     parcelLookup) {
                    m_ControlPoint        = controlPoint;
                    m_BestSnapPosition    = bestSnapPosition;
                    m_Bounds              = bounds;
                    m_NewParcelData       = newParcelData;
                    m_LotSize             = m_NewParcelData.m_LotSize;
                    m_NewParcelSize       = ParcelGeometryUtils.GetParcelSize(m_LotSize);
                    m_BestDistance        = bestDistance;
                    m_EnableEdgeSnap      = enableEdgeSnap;
                    m_EnableFrontAlign    = enableFrontAlign;
                    m_SnapLevelEdge       = snapLevelEdge;
                    m_SnapLevelFrontAlign = snapLevelFrontAlign;
                    m_TransformLookup     = transformLookup;
                    m_PrefabRefLookup     = prefabRefLookup;
                    m_ParcelDataLookup    = parcelDataLookup;
                    m_ParcelLookup        = parcelLookup;
                }

                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds, m_Bounds); }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity existingEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds)) {
                        return;
                    }

                    // Verify this is a parcel with required components
                    if (!m_ParcelLookup.HasComponent(existingEntity)    ||
                        !m_TransformLookup.HasComponent(existingEntity) ||
                        !m_PrefabRefLookup.HasComponent(existingEntity)) {
                        return;
                    }

                    var existingTransform = m_TransformLookup[existingEntity];
                    var prefabRef         = m_PrefabRefLookup[existingEntity];

                    if (!m_ParcelDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        return;
                    }

                    var existingParcelData = m_ParcelDataLookup[prefabRef.m_Prefab];
                    var existingSize       = ParcelGeometryUtils.GetParcelSize(existingParcelData.m_LotSize);
                    var existingCorners    = ParcelGeometryUtils.GetWorldCorners(existingTransform, existingParcelData.m_LotSize);
                    var existingRightFront = existingCorners.a;
                    var existingLeftFront  = existingCorners.b;

                    // Get existing parcel's direction (forward = +Z in local, transformed to world)
                    var existingFrontDir = math.mul(existingTransform.m_Rotation, new float3(0f, 0f, 1f)).xz;
                    MathUtils.TryNormalize(ref existingFrontDir);
                    var existingRightDir = MathUtils.Left(existingFrontDir);
                    var existingLeftDir  = MathUtils.Right(existingFrontDir);
                    var existingBackDir  = -existingFrontDir;

                    // Try front corner alignment first (higher priority if enabled)
                    if (m_EnableFrontAlign) {
                        // New parcel half dimensions
                        var newHalfWidth = m_NewParcelSize.x * 0.5f;
                        var newHalfDepth = m_NewParcelSize.z * 0.5f;

                        // Case 1: New LeftFront corner → Existing RightFront corner (new parcel to the RIGHT)
                        TrySnapToCorner(existingEntity, existingTransform, existingRightFront, existingFrontDir, existingRightDir, newHalfWidth, newHalfDepth, false);

                        // Case 2: New RightFront corner → Existing LeftFront corner (new parcel to the LEFT)
                        TrySnapToCorner(existingEntity, existingTransform, existingLeftFront, existingFrontDir, existingRightDir, newHalfWidth, newHalfDepth, true);
                    }

                    // Try edge snapping (side-to-side with lateral freedom)
                    if (m_EnableEdgeSnap) {
                        // Front
                        CheckSnapLine(
                            existingParcelData,
                            existingFrontDir,
                            existingTransform,
                            m_ControlPoint,
                            new Line2.Segment(existingCorners.a, existingCorners.b),
                            existingParcelData.m_LotSize.x - 1);
                        // Left
                        CheckSnapLine(
                            existingParcelData,
                            existingLeftDir,
                            existingTransform,
                            m_ControlPoint,
                            new Line2.Segment(existingCorners.b, existingCorners.c),
                            existingParcelData.m_LotSize.y - 1);
                        // Back
                        CheckSnapLine(
                            existingParcelData,
                            existingBackDir,
                            existingTransform,
                            m_ControlPoint,
                            new Line2.Segment(existingCorners.c, existingCorners.d),
                            existingParcelData.m_LotSize.x - 1);
                        // Right
                        CheckSnapLine(
                            existingParcelData,
                            existingRightDir,
                            existingTransform,
                            m_ControlPoint,
                            new Line2.Segment(existingCorners.d, existingCorners.a),
                            existingParcelData.m_LotSize.y - 1);
                    }
                }

                public enum RelativeDirection {
                    Forward = 0, // Input: 0
                    Right   = 1, // Input: PI
                    Back    = 2, // Input: 2PI or -2PI
                    Left    = 3,  // Input: -PI
                }

                private static bool TryGetRelativeDirection(float rotationRadians, out RelativeDirection relativeDirection) {
                    if (Mathf.Approximately(rotationRadians, math.PI) || Mathf.Approximately(rotationRadians, -math.PI)) {
                        relativeDirection = RelativeDirection.Back;
                        return true;
                    } 
                    if (Mathf.Approximately(rotationRadians, math.PIHALF)) {
                        relativeDirection = RelativeDirection.Left;
                        return true;
                    } 
                    if (Mathf.Approximately(rotationRadians, 0f)) {
                        relativeDirection = RelativeDirection.Forward;
                        return true;
                    } 
                    if (Mathf.Approximately(rotationRadians, -math.PIHALF)) {
                        relativeDirection = RelativeDirection.Right;
                        return true;
                    }

                    relativeDirection = RelativeDirection.Forward;
                    return false;
                }

                private void CheckSnapLine(ParcelData    existingParcelData, 
                                           float2        existingDirection,
                                           Transform     existingTransform, 
                                           ControlPoint  controlPoint,      
                                           Line2.Segment line,              
                                           int           maxOffset 
                ) {
                    var newCorners = ParcelGeometryUtils.GetWorldCorners(controlPoint.m_Rotation, controlPoint.m_Position, m_LotSize);
                    // todo: Try to exit early when too distant.

                    //PlatterMod.Instance.Log.Debug($"CheckSnapLine --  existingDirection {existingDirection.ToJSONString()}");

                    // Create candidate snap position
                    var candidate = controlPoint;
                    candidate.m_OriginalEntity = Entity.Null;
                    candidate.m_Position.y     = existingTransform.m_Position.y;

                    // Calculate position of candidate on the line
                    MathUtils.Distance(line, controlPoint.m_Position.xz, out var t);
                    var validRange = 8f * maxOffset; // Allow going over by some amount of cells
                    var lineLength = math.distance(line.a, line.b);  

                    t *= lineLength;                                  
                    t =  math.clamp(t, -validRange, lineLength + validRange);

                    candidate.m_Position.xz = MathUtils.Position(line, t / lineLength);

                    // Calculate the needed rotation to align the new parcel edge with the existing edge
                    var degreesToAlign = GetAlignmentRotation(line, new Line2.Segment(newCorners.a, newCorners.b));

                    // Set facing direction
                    candidate.m_Direction = math.mul(
                        math.mul(candidate.m_Rotation, quaternion.RotateY(math.radians(degreesToAlign))),
                        new float3(0f, 0f, 1f)
                    ).xz;
                    MathUtils.TryNormalize(ref candidate.m_Direction);
                    candidate.m_Rotation = ToolUtils.CalculateRotation(candidate.m_Direction);

                    // Calculate directional delta between edge direction and new parcel direction
                    var directionDelta = MathUtils.RotationAngleSignedLeft(existingDirection, candidate.m_Direction);

                    if (!TryGetRelativeDirection(directionDelta, out var relativeDirection)) {
                        return;
                    }

                    // Calculate offset direction and amount
                    var offsetDirection = default(float2);
                    var offsetAmount    = 0.0f;
                    var offsetSign      = 1.0f;

                    switch (relativeDirection) {
                        case RelativeDirection.Forward:
                            offsetDirection = existingDirection;
                            offsetAmount    = m_NewParcelSize.z * 0.5f;
                            offsetSign      = 1f;
                            break;
                        case RelativeDirection.Back:
                            offsetDirection = existingDirection;
                            offsetAmount    = m_NewParcelSize.z * 0.5f;
                            offsetSign      = 1f;
                            break;
                        case RelativeDirection.Right:
                            offsetDirection = existingDirection;
                            offsetAmount    = m_NewParcelSize.x * 0.5f;
                            offsetSign      = 1f;
                            break;
                        case RelativeDirection.Left:
                            offsetDirection = existingDirection;
                            offsetAmount    = m_NewParcelSize.x * 0.5f;
                            offsetSign      = 1f;
                            break;
                    }

                    // Apply offset
                    candidate.m_Position.xz += offsetDirection * offsetAmount * offsetSign;

                    var distanceToHit = math.distance(candidate.m_Position.xz, m_ControlPoint.m_HitPosition.xz);

                    if (distanceToHit >= m_BestDistance) {
                        return;
                    }

                    // Calculate priority
                    candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                        SnapLevel.ParcelEdge,
                        1f,
                        1f,
                        controlPoint.m_HitPosition * 0.5f,
                        candidate.m_Position       * 0.5f,
                        candidate.m_Direction
                    );

                    if (!CompareSnapPriority(candidate.m_SnapPriority, m_BestSnapPosition.m_SnapPriority)) {
                        return;
                    }

                    m_BestDistance     = distanceToHit;
                    m_BestSnapPosition = candidate;
                }

                /// <summary>
                /// Calculates the smallest rotation (in degrees) required to align edgeB 
                /// parallel or perpendicular to edgeA.
                /// </summary>
                public static float GetAlignmentRotation(Line2.Segment a, Line2.Segment b) {
                    // 1. Get the vectors representing the edges
                    var vectorA = a.b - a.a;
                    var vectorB = b.b - b.a;

                    // 2. Calculate the signed angle from A to B (in Radians)
                    // We use atan2(determinant, dot_product) for robust angle calculation
                    var det     = vectorA.x * vectorB.y - vectorA.y * vectorB.x;
                    var dotProd = math.dot(vectorA, vectorB);

                    var angleRadians = math.atan2(det, dotProd);
                    var angleDegrees = math.degrees(angleRadians);

                    // 3. Find the offset from the nearest 90-degree axis
                    // We use the remainder operator (%)
                    var remainder = angleDegrees % 90.0f;

                    // 4. Adjust the remainder to find the shortest path [-45 to 45]
                    // Example: If remainder is 89, we want to rotate +1, not -89.
                    if (remainder > 45.0f) {
                        remainder -= 90.0f;
                    } else if (remainder < -45.0f) {
                        remainder += 90.0f;
                    }

                    // 5. The rotation to APPLY is the negation of the current offset
                    // If the line is offset by +10 degrees, we must rotate by -10 to fix it.
                    return remainder;
                }

                /// <summary>
                /// Helper to try snapping to a specific corner position.
                /// </summary>
                private void TrySnapToCorner(Entity existingEntity,    Transform existingTransform, float2 cornerPosition,
                                             float2 existingDirection, float2    existingRight,
                                             float  newHalfWidth,      float     newHalfDepth, bool isRightSide) {
                    // Calculate new parcel center based on which side we're snapping to
                    var newCenter = new float3(cornerPosition.x, existingTransform.m_Position.y, cornerPosition.y);
                    newCenter.xz += existingRight     * newHalfWidth * (isRightSide ? -1f : 1f);
                    newCenter.xz -= existingDirection * newHalfDepth;

                    var distanceToHit = math.distance(newCenter.xz, m_ControlPoint.m_HitPosition.xz);

                    if (distanceToHit >= m_BestDistance) {
                        return;
                    }

                    var priority = CalculateSnapPriority(
                        m_SnapLevelFrontAlign,
                        1f,
                        1f,
                        m_ControlPoint.m_HitPosition,
                        new float3(newCenter.x, existingTransform.m_Position.y, newCenter.y),
                        existingDirection
                    );

                    if (!CompareSnapPriority(priority, m_BestSnapPosition.m_SnapPriority)) {
                        return;
                    }

                    m_BestDistance                      = distanceToHit;
                    m_BestSnapPosition.m_Position       = newCenter;
                    m_BestSnapPosition.m_Direction      = existingDirection;
                    m_BestSnapPosition.m_Rotation       = existingTransform.m_Rotation;
                    m_BestSnapPosition.m_SnapPriority   = priority;
                    //m_BestSnapPosition.m_OriginalEntity = existingEntity;
                }
            }
        }
    }
}