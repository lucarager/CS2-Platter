// <copyright file="P_NewCellCheckSystem.BlockCellsJob.cs" company="Luca Rager">
// Copyright (c) lucar. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Areas;
    using Game.Common;
    using Game.Objects;
    using Game.Net;
    using Game.Zones;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Game.Prefabs;

    public partial class P_NewCellCheckSystem {
        /// <summary>
        /// Similar to base-game BlockCellsJob, but adapted to Platter's zoning system.
        /// Normalizes parcel cells and checks for conflicts with Net and Area geometry to mark blocked cells.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct BlockCellsJob : Unity.Jobs.IJobParallelForDefer {
            [ReadOnly] public required NativeArray<CellCheckHelpers.SortedEntity> m_Blocks;
            [ReadOnly] public required ComponentLookup<Block> m_BlockLookup;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData> m_ParcelDataLookup;

            [ReadOnly]
            public required Colossal.Collections.NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

            [ReadOnly]
            public required Colossal.Collections.NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> m_AreaSearchTree;

            [ReadOnly] public required ComponentLookup<Owner> m_OwnerLookup;
            [ReadOnly] public required ComponentLookup<Transform> m_TransformLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry> m_EdgeGeometryLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry> m_StartNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry> m_EndNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<Composition> m_CompositionLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<NetCompositionData> m_NetCompositionLookup;

            [ReadOnly]
            public required ComponentLookup<RoadComposition> m_PrefabRoadCompositionLookup;

            [ReadOnly]
            public required ComponentLookup<AreaGeometryData> m_PrefabAreaGeometryLookup;

            [ReadOnly]
            public required ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryLookup;

            [ReadOnly] public required ComponentLookup<Native> m_NativeLookup;
            [ReadOnly] public required BufferLookup<Game.Areas.Node> m_AreaNodesLookup;
            [ReadOnly] public required BufferLookup<Triangle> m_AreaTrianglesLookup;

            [NativeDisableParallelForRestrictionAttribute]
            public required BufferLookup<Cell> m_CellsLookup;

            [NativeDisableParallelForRestrictionAttribute]
            public required ComponentLookup<ValidArea> m_ValidAreaLookup;

            public void Execute(int index) {
                var entity = m_Blocks[index].m_Entity;

                // Exit early if it's not a parcel
                if (!m_ParcelOwnerLookup.TryGetComponent(entity, out var parcelOwner)) {
                    return;
                }

                // Retrieve data
                var block = m_BlockLookup[entity];
                var prefab = m_PrefabRefLookup[parcelOwner.m_Owner];
                var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                var cellBuffer = m_CellsLookup[entity];
                var validArea = new ValidArea() {
                    m_Area = new Unity.Mathematics.int4(0, parcelData.m_LotSize.x, 0, parcelData.m_LotSize.y),
                };
                var parcelGeo = new Platter.Utils.ParcelGeometry(parcelData.m_LotSize);
                var bounds = parcelGeo.Bounds;
                var corners = ZoneUtils.CalculateCorners(block, validArea);

                Platter.Utils.DebugUtils.DebugBlockStatus("[BCJ] Start", entity, cellBuffer);

                if (parcelData.m_LotSize.x > 1) {
                    // Normalize "wide" parcel's cells.
                    NormalizeWideParcelCells(block, parcelData, cellBuffer);
                    m_ValidAreaLookup[entity] = validArea;
                    // "Wide" parcels should now be done and ready for processing by other jobs.
                    return;
                } else {
                    // Normalize "narrow" parcel's cells so they are processed from a clean state.
                    NormalizeNarrowParcelCells(block, parcelData, cellBuffer);
                }

                // Check for conflicts with Net geometry.
                var netIterator = new NetIterator {
                    m_BlockEntity = entity,
                    m_BlockData = block,
                    m_Bounds = bounds.xz,
                    m_Quad = corners,
                    m_ValidAreaData = validArea,
                    m_Cells = cellBuffer,
                    m_OwnerData = this.m_OwnerLookup,
                    m_TransformData = this.m_TransformLookup,
                    m_EdgeGeometryData = this.m_EdgeGeometryLookup,
                    m_StartNodeGeometryData = this.m_StartNodeGeometryLookup,
                    m_EndNodeGeometryData = this.m_EndNodeGeometryLookup,
                    m_CompositionData = this.m_CompositionLookup,
                    m_PrefabRefData = this.m_PrefabRefLookup,
                    m_PrefabCompositionData = this.m_NetCompositionLookup,
                    m_PrefabRoadCompositionData = this.m_PrefabRoadCompositionLookup,
                    m_PrefabObjectGeometryData = this.m_PrefabObjectGeometryLookup
                };
                m_NetSearchTree.Iterate(ref netIterator, 0);

                // Check for conflicts with Areas.
                var areaIterator = new AreaIterator {
                    m_BlockEntity = entity,
                    m_BlockData = block,
                    m_Bounds = bounds.xz,
                    m_Quad = corners,
                    m_ValidAreaData = validArea,
                    m_Cells = cellBuffer,
                    m_NativeData = this.m_NativeLookup,
                    m_PrefabRefData = this.m_PrefabRefLookup,
                    m_PrefabAreaGeometryData = this.m_PrefabAreaGeometryLookup,
                    m_AreaNodes = this.m_AreaNodesLookup,
                    m_AreaTriangles = this.m_AreaTrianglesLookup
                };
                m_AreaSearchTree.Iterate(ref areaIterator, 0);

                // Process the results, calculating the final valid area.
                CleanBlockedCells(block, ref validArea, cellBuffer);

                Platter.Utils.DebugUtils.DebugBlockStatus("[BCJ] End", entity, cellBuffer);

                // Set final valid area data
                m_ValidAreaLookup[entity] = validArea;
            }

            /// <summary>
            /// Normalizes the cells of "wide" parcels (width > 1) by marking cells outside the lot size as blocked and occupied.
            /// The rest of the cells should have the correct flags at this stage.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="parcelData"></param>
            /// <param name="cells"></param>
            private static void NormalizeWideParcelCells(Block block,
                                                         ParcelData parcelData,
                                                         DynamicBuffer<Cell> cells) {
                for (var row = 0; row < block.m_Size.y; row++)
                    for (var col = 0; col < block.m_Size.x; col++) {
                        var isOutsideLot = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;

                        if (!isOutsideLot) {
                            continue;
                        }

                        var i = row * block.m_Size.x + col;
                        var cell = cells[i];

                        cell.m_State = CellFlags.Blocked;
                        cell.m_Zone = ZoneType.None;

                        cells[i] = cell;
                    }
            }

            /// <summary>
            /// Normalizes the cells of a 1-cell-wide parcel block.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="parcelData"></param>
            /// <param name="cells"></param>
            private static void NormalizeNarrowParcelCells(Block block,
                                                           ParcelData parcelData,
                                                           DynamicBuffer<Cell> cells) {
                for (var row = 0; row < block.m_Size.y; row++)
                    for (var col = 0; col < block.m_Size.x; col++) {
                        var isOutsideLot = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;
                        var i = row * block.m_Size.x + col;
                        var cell = cells[i];

                        if (isOutsideLot) {
                            cell.m_State = CellFlags.Blocked;
                            cell.m_Zone = ZoneType.None;
                        } else {
                            cell.m_State &= ~(CellFlags.Occupied | CellFlags.Blocked);
                        }

                        cells[i] = cell;
                    }
            }

            // Exact copy of CleanBlockedCells from vanilla CellCheckSystem.
            // Propagates blocked cell states and calculates the valid buildable area within a block.
            // This method performs two main operations:
            // 1. Forward propagation: Blocks cells that are below other blocked cells (top-down blocking)
            // 2. Road flag propagation: Sets RoadLeft/RoadRight flags on cells adjacent to blocked road cells
            private static void CleanBlockedCells(Block blockData,
                                                  ref ValidArea validAreaData,
                                                  DynamicBuffer<Cell> cells) {
                var validArea = default(ValidArea);
                validArea.m_Area.xz = blockData.m_Size;
                for (var i = validAreaData.m_Area.x; i < validAreaData.m_Area.y; i++) {
                    var cell = cells[i];
                    var cell2 = cells[blockData.m_Size.x + i];
                    if (((cell.m_State & CellFlags.Blocked) == CellFlags.None) &
                        ((cell2.m_State & CellFlags.Blocked) > CellFlags.None)) {
                        cell.m_State |= CellFlags.Blocked;
                        cells[i] = cell;
                    }

                    var num = 0;
                    for (var j = validAreaData.m_Area.z + 1; j < validAreaData.m_Area.w; j++) {
                        var num2 = j * blockData.m_Size.x + i;
                        var cell3 = cells[num2];
                        if (((cell3.m_State & CellFlags.Blocked) == CellFlags.None) &
                            ((cell.m_State & CellFlags.Blocked) > CellFlags.None)) {
                            cell3.m_State |= CellFlags.Blocked;
                            cells[num2] = cell3;
                        }

                        if ((cell3.m_State & CellFlags.Blocked) == CellFlags.None) {
                            num = j + 1;
                        }

                        cell = cell3;
                    }

                    if (num > validAreaData.m_Area.z) {
                        validArea.m_Area.xz = Unity.Mathematics.math.min(validArea.m_Area.xz, new Unity.Mathematics.int2(i, validAreaData.m_Area.z));
                        validArea.m_Area.yw = Unity.Mathematics.math.max(validArea.m_Area.yw, new Unity.Mathematics.int2(i + 1, num));
                    }
                }

                validAreaData = validArea;
                for (var k = validAreaData.m_Area.z; k < validAreaData.m_Area.w; k++) {
                    for (var l = validAreaData.m_Area.x; l < validAreaData.m_Area.y; l++) {
                        var num3 = k * blockData.m_Size.x + l;
                        var cell4 = cells[num3];
                        if ((cell4.m_State & (CellFlags.Blocked | CellFlags.RoadLeft)) == CellFlags.None && l > 0 &&
                            (cells[num3 - 1].m_State & (CellFlags.Blocked | CellFlags.RoadLeft)) ==
                            (CellFlags.Blocked | CellFlags.RoadLeft)) {
                            cell4.m_State |= CellFlags.RoadLeft;
                            cells[num3] = cell4;
                        }

                        if ((cell4.m_State & (CellFlags.Blocked | CellFlags.RoadRight)) == CellFlags.None &&
                            l < blockData.m_Size.x - 1 && (cells[num3 + 1].m_State & (CellFlags.Blocked | CellFlags.RoadRight)) ==
                            (CellFlags.Blocked | CellFlags.RoadRight)) {
                            cell4.m_State |= CellFlags.RoadRight;
                            cells[num3] = cell4;
                        }
                    }
                }
            }

            // Exact copy of NetIterator from vanilla CellCheckSystem.
            private struct NetIterator : Colossal.Collections.INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds)) {
                        return;
                    }

                    if (!this.m_EdgeGeometryData.HasComponent(edgeEntity)) {
                        return;
                    }

                    this.m_HasIgnore = false;
                    if (this.m_OwnerData.HasComponent(edgeEntity)) {
                        var owner = this.m_OwnerData[edgeEntity];
                        if (this.m_TransformData.HasComponent(owner.m_Owner)) {
                            var prefabRef = this.m_PrefabRefData[owner.m_Owner];
                            if (this.m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab)) {
                                var transform = this.m_TransformData[owner.m_Owner];
                                var objectGeometryData = this.m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                                if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Circular) != Game.Objects.GeometryFlags.None) {
                                    var @float = Unity.Mathematics.math.max(objectGeometryData.m_Size - 0.16f, 0f);
                                    this.m_IgnoreCircle = new Colossal.Mathematics.Circle2(@float.x * 0.5f, transform.m_Position.xz);
                                    this.m_HasIgnore.y = true;
                                } else {
                                    var bounds2 = Colossal.Mathematics.MathUtils.Expand(objectGeometryData.m_Bounds, -0.08f);
                                    var float2 = Colossal.Mathematics.MathUtils.Center(bounds2);
                                    var @bool = bounds2.min > bounds2.max;
                                    bounds2.min = Unity.Mathematics.math.select(bounds2.min, float2, @bool);
                                    bounds2.max = Unity.Mathematics.math.select(bounds2.max, float2, @bool);
                                    this.m_IgnoreQuad = Game.Objects.ObjectUtils.CalculateBaseCorners(transform.m_Position, transform.m_Rotation, bounds2)
                                                            .xz;
                                    this.m_HasIgnore.x = true;
                                }
                            }
                        }
                    }

                    var composition = this.m_CompositionData[edgeEntity];
                    var edgeGeometry = this.m_EdgeGeometryData[edgeEntity];
                    var startNodeGeometry = this.m_StartNodeGeometryData[edgeEntity];
                    var endNodeGeometry = this.m_EndNodeGeometryData[edgeEntity];
                    if (Colossal.Mathematics.MathUtils.Intersect(this.m_Bounds, edgeGeometry.m_Bounds.xz)) {
                        var netCompositionData = this.m_PrefabCompositionData[composition.m_Edge];
                        var roadComposition = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_Edge)) {
                            roadComposition = this.m_PrefabRoadCompositionData[composition.m_Edge];
                        }

                        this.CheckSegment(edgeGeometry.m_Start.m_Left,
                                          edgeGeometry.m_Start.m_Right,
                                          netCompositionData,
                                          roadComposition,
                                          new Unity.Mathematics.bool2(true, true));
                        this.CheckSegment(edgeGeometry.m_End.m_Left,
                                          edgeGeometry.m_End.m_Right,
                                          netCompositionData,
                                          roadComposition,
                                          new Unity.Mathematics.bool2(true, true));
                    }

                    if (Colossal.Mathematics.MathUtils.Intersect(this.m_Bounds, startNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData2 = this.m_PrefabCompositionData[composition.m_StartNode];
                        var roadComposition2 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_StartNode)) {
                            roadComposition2 = this.m_PrefabRoadCompositionData[composition.m_StartNode];
                        }

                        if (startNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left,
                                              startNodeGeometry.m_Geometry.m_Left.m_Right,
                                              netCompositionData2,
                                              roadComposition2,
                                              new Unity.Mathematics.bool2(true, true));
                            var bezier4x = Colossal.Mathematics.MathUtils.Lerp(startNodeGeometry.m_Geometry.m_Right.m_Left,
                                                                               startNodeGeometry.m_Geometry.m_Right.m_Right,
                                                                               0.5f);
                            bezier4x.d = startNodeGeometry.m_Geometry.m_Middle.d;
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Right.m_Left,
                                              bezier4x,
                                              netCompositionData2,
                                              roadComposition2,
                                              new Unity.Mathematics.bool2(true, false));
                            this.CheckSegment(bezier4x,
                                              startNodeGeometry.m_Geometry.m_Right.m_Right,
                                              netCompositionData2,
                                              roadComposition2,
                                              new Unity.Mathematics.bool2(false, true));
                        } else {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left,
                                              startNodeGeometry.m_Geometry.m_Middle,
                                              netCompositionData2,
                                              roadComposition2,
                                              new Unity.Mathematics.bool2(true, false));
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Middle,
                                              startNodeGeometry.m_Geometry.m_Right.m_Right,
                                              netCompositionData2,
                                              roadComposition2,
                                              new Unity.Mathematics.bool2(false, true));
                        }
                    }

                    if (Colossal.Mathematics.MathUtils.Intersect(this.m_Bounds, endNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData3 = this.m_PrefabCompositionData[composition.m_EndNode];
                        var roadComposition3 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_EndNode)) {
                            roadComposition3 = this.m_PrefabRoadCompositionData[composition.m_EndNode];
                        }

                        if (endNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(endNodeGeometry.m_Geometry.m_Left.m_Left,
                                              endNodeGeometry.m_Geometry.m_Left.m_Right,
                                              netCompositionData3,
                                              roadComposition3,
                                              new Unity.Mathematics.bool2(true, true));
                            var bezier4x2 =
                                Colossal.Mathematics.MathUtils.Lerp(endNodeGeometry.m_Geometry.m_Right.m_Left,
                                                                    endNodeGeometry.m_Geometry.m_Right.m_Right,
                                                                    0.5f);
                            bezier4x2.d = endNodeGeometry.m_Geometry.m_Middle.d;
                            this.CheckSegment(endNodeGeometry.m_Geometry.m_Right.m_Left,
                                              bezier4x2,
                                              netCompositionData3,
                                              roadComposition3,
                                              new Unity.Mathematics.bool2(true, false));
                            this.CheckSegment(bezier4x2,
                                              endNodeGeometry.m_Geometry.m_Right.m_Right,
                                              netCompositionData3,
                                              roadComposition3,
                                              new Unity.Mathematics.bool2(false, true));
                            return;
                        }

                        this.CheckSegment(endNodeGeometry.m_Geometry.m_Left.m_Left,
                                          endNodeGeometry.m_Geometry.m_Middle,
                                          netCompositionData3,
                                          roadComposition3,
                                          new Unity.Mathematics.bool2(true, false));
                        this.CheckSegment(endNodeGeometry.m_Geometry.m_Middle,
                                          endNodeGeometry.m_Geometry.m_Right.m_Right,
                                          netCompositionData3,
                                          roadComposition3,
                                          new Unity.Mathematics.bool2(false, true));
                    }
                }

                private void CheckSegment(Colossal.Mathematics.Bezier4x3 left,
                                          Colossal.Mathematics.Bezier4x3 right,
                                          NetCompositionData prefabCompositionData,
                                          RoadComposition prefabRoadData,
                                          Unity.Mathematics.bool2 isEdge) {
                    if ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Tunnel) !=
                        (CompositionFlags.General)0U) {
                        return;
                    }

                    if ((prefabCompositionData.m_State & CompositionState.BlockZone) == (CompositionState)0) {
                        return;
                    }

                    var flag = (prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) >
                               (CompositionFlags.General)0U;
                    flag |= (prefabCompositionData.m_State & CompositionState.ExclusiveGround) == (CompositionState)0;
                    if (!Colossal.Mathematics.MathUtils.Intersect((Colossal.Mathematics.MathUtils.Bounds(left) |
                                                                   Colossal.Mathematics.MathUtils.Bounds(right)).xz,
                                                                  this.m_Bounds)) {
                        return;
                    }

                    isEdge &= ((prefabRoadData.m_Flags & Game.Prefabs.RoadFlags.EnableZoning) > (Game.Prefabs.RoadFlags)0) &
                              ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) ==
                               (CompositionFlags.General)0U);
                    isEdge &=
                        new Unity.Mathematics.bool2((prefabCompositionData.m_Flags.m_Left &
                                                     (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) ==
                                                    (CompositionFlags.Side)0U,
                                                    (prefabCompositionData.m_Flags.m_Right & (CompositionFlags.Side.Raised |
                                                                                              CompositionFlags.Side.Lowered)) ==
                                                    (CompositionFlags.Side)0U);
                    Colossal.Mathematics.Quad3 quad;
                    quad.a = left.a;
                    quad.b = right.a;
                    var bounds = NetIterator.SetHeightRange(Colossal.Mathematics.MathUtils.Bounds(quad.a, quad.b), prefabCompositionData.m_HeightRange);
                    for (var i = 1; i <= 8; i++) {
                        var num = (float)i / 8f;
                        quad.d = Colossal.Mathematics.MathUtils.Position(left, num);
                        quad.c = Colossal.Mathematics.MathUtils.Position(right, num);
                        var bounds2 = NetIterator.SetHeightRange(Colossal.Mathematics.MathUtils.Bounds(quad.d, quad.c),
                                                                 prefabCompositionData.m_HeightRange);
                        var bounds3 = bounds | bounds2;
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3.xz, this.m_Bounds) &&
                            Colossal.Mathematics.MathUtils.Intersect(this.m_Quad, quad.xz)) {
                            var cellFlags = CellFlags.Blocked;
                            if (isEdge.x) {
                                var block = new Block {
                                    m_Direction = Unity.Mathematics.math.normalizesafe(Colossal.Mathematics.MathUtils.Right(quad.d.xz - quad.a.xz),
                                                                                       default(Unity.Mathematics.float2))
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(this.m_BlockData, block);
                            }

                            if (isEdge.y) {
                                var block2 = new Block {
                                    m_Direction = Unity.Mathematics.math.normalizesafe(Colossal.Mathematics.MathUtils.Left(quad.c.xz - quad.b.xz),
                                                                                       default(Unity.Mathematics.float2))
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(this.m_BlockData, block2);
                            }

                            this.CheckOverlapX(this.m_Bounds, bounds3, this.m_Quad, quad, this.m_ValidAreaData.m_Area, cellFlags, flag);
                        }

                        quad.a = quad.d;
                        quad.b = quad.c;
                        bounds = bounds2;
                    }
                }

                private static Colossal.Mathematics.Bounds3
                    SetHeightRange(Colossal.Mathematics.Bounds3 bounds, Colossal.Mathematics.Bounds1 heightRange) {
                    bounds.min.y = bounds.min.y + heightRange.min;
                    bounds.max.y = bounds.max.y + heightRange.max;
                    return bounds;
                }

                private void CheckOverlapX(Colossal.Mathematics.Bounds2 bounds1,
                                           Colossal.Mathematics.Bounds3 bounds2,
                                           Colossal.Mathematics.Quad2 quad1,
                                           Colossal.Mathematics.Quad3 quad2,
                                           Unity.Mathematics.int4 xxzz1,
                                           CellFlags flags,
                                           bool isElevated) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
                        int2.x = @int.y;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad3.b = Unity.Mathematics.math.lerp(quad1.a, quad1.b, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad1.d, quad1.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2.xz)) {
                            this.CheckOverlapZ(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }

                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2.xz)) {
                            this.CheckOverlapZ(bounds4, bounds2, quad4, quad2, int2, flags, isElevated);
                            return;
                        }
                    } else {
                        this.CheckOverlapZ(bounds1, bounds2, quad1, quad2, xxzz1, flags, isElevated);
                    }
                }

                private void CheckOverlapZ(Colossal.Mathematics.Bounds2 bounds1,
                                           Colossal.Mathematics.Bounds3 bounds2,
                                           Colossal.Mathematics.Quad2 quad1,
                                           Colossal.Mathematics.Quad3 quad2,
                                           Unity.Mathematics.int4 xxzz1,
                                           CellFlags flags,
                                           bool isElevated) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
                        int2.z = @int.w;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad3.d = Unity.Mathematics.math.lerp(quad1.a, quad1.d, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad1.b, quad1.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2.xz)) {
                            this.CheckOverlapX(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }

                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2.xz)) {
                            this.CheckOverlapX(bounds4, bounds2, quad4, quad2, int2, flags, isElevated);
                            return;
                        }
                    } else {
                        if (xxzz1.y - xxzz1.x >= 2) {
                            this.CheckOverlapX(bounds1, bounds2, quad1, quad2, xxzz1, flags, isElevated);
                            return;
                        }

                        var num2 = xxzz1.z * this.m_BlockData.m_Size.x + xxzz1.x;
                        var cell = this.m_Cells[num2];
                        if ((cell.m_State & flags) == flags) {
                            return;
                        }

                        quad1 = Colossal.Mathematics.MathUtils.Expand(quad1, -0.0625f);
                        if (Colossal.Mathematics.MathUtils.Intersect(quad1, quad2.xz)) {
                            if (Unity.Mathematics.math.any(this.m_HasIgnore)) {
                                if (this.m_HasIgnore.x && Colossal.Mathematics.MathUtils.Intersect(quad1, this.m_IgnoreQuad)) {
                                    return;
                                }

                                if (this.m_HasIgnore.y && Colossal.Mathematics.MathUtils.Intersect(quad1, this.m_IgnoreCircle)) {
                                    return;
                                }
                            }

                            if (isElevated) {
                                cell.m_Height = (short)Unity.Mathematics.math.clamp(UnityEngine.Mathf.FloorToInt(bounds2.min.y),
                                                                                    -32768,
                                                                                    Unity.Mathematics.math.min((int)cell.m_Height, 32767));
                            } else {
                                cell.m_State |= flags;
                            }

                            this.m_Cells[num2] = cell;
                        }
                    }
                }

                public Entity m_BlockEntity;

                public Block m_BlockData;

                public ValidArea m_ValidAreaData;

                public Colossal.Mathematics.Bounds2 m_Bounds;

                public Colossal.Mathematics.Quad2 m_Quad;

                public Colossal.Mathematics.Quad2 m_IgnoreQuad;

                public Colossal.Mathematics.Circle2 m_IgnoreCircle;

                public Unity.Mathematics.bool2 m_HasIgnore;

                public DynamicBuffer<Cell> m_Cells;
                public ComponentLookup<Owner> m_OwnerData;
                public ComponentLookup<Transform> m_TransformData;
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;
                public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;
                public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;
                public ComponentLookup<Composition> m_CompositionData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
                public ComponentLookup<RoadComposition> m_PrefabRoadCompositionData;
                public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
            }

            // Exact copy of AreaIterator from vanilla CellCheckSystem.
            private struct AreaIterator : Colossal.Collections.INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem areaItem) {
                    if (!Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds)) {
                        return;
                    }

                    var prefabRef = this.m_PrefabRefData[areaItem.m_Area];
                    var areaGeometryData = this.m_PrefabAreaGeometryData[prefabRef.m_Prefab];
                    if ((areaGeometryData.m_Flags & (Game.Areas.GeometryFlags.PhysicalGeometry | Game.Areas.GeometryFlags.ProtectedArea)) ==
                        (Game.Areas.GeometryFlags)0) {
                        return;
                    }

                    if ((areaGeometryData.m_Flags & Game.Areas.GeometryFlags.ProtectedArea) != (Game.Areas.GeometryFlags)0 &&
                        !this.m_NativeData.HasComponent(areaItem.m_Area)) {
                        return;
                    }

                    var dynamicBuffer = this.m_AreaNodes[areaItem.m_Area];
                    var dynamicBuffer2 = this.m_AreaTriangles[areaItem.m_Area];
                    if (dynamicBuffer2.Length <= areaItem.m_Triangle) {
                        return;
                    }

                    var triangle = AreaUtils.GetTriangle3(dynamicBuffer, dynamicBuffer2[areaItem.m_Triangle]);
                    this.CheckOverlapX(this.m_Bounds, bounds.m_Bounds.xz, this.m_Quad, triangle.xz, this.m_ValidAreaData.m_Area);
                }

                private void CheckOverlapX(Colossal.Mathematics.Bounds2 bounds1,
                                           Colossal.Mathematics.Bounds2 bounds2,
                                           Colossal.Mathematics.Quad2 quad1,
                                           Colossal.Mathematics.Triangle2 triangle2,
                                           Unity.Mathematics.int4 xxzz1) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
                        int2.x = @int.y;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad2.b = Unity.Mathematics.math.lerp(quad1.a, quad1.b, num);
                        quad2.c = Unity.Mathematics.math.lerp(quad1.d, quad1.c, num);
                        quad3.a = quad2.b;
                        quad3.d = quad2.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad2);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2)) {
                            this.CheckOverlapZ(bounds3, bounds2, quad2, triangle2, @int);
                        }

                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2)) {
                            this.CheckOverlapZ(bounds4, bounds2, quad3, triangle2, int2);
                            return;
                        }
                    } else {
                        this.CheckOverlapZ(bounds1, bounds2, quad1, triangle2, xxzz1);
                    }
                }

                private void CheckOverlapZ(Colossal.Mathematics.Bounds2 bounds1,
                                           Colossal.Mathematics.Bounds2 bounds2,
                                           Colossal.Mathematics.Quad2 quad1,
                                           Colossal.Mathematics.Triangle2 triangle2,
                                           Unity.Mathematics.int4 xxzz1) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
                        int2.z = @int.w;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad2.d = Unity.Mathematics.math.lerp(quad1.a, quad1.d, num);
                        quad2.c = Unity.Mathematics.math.lerp(quad1.b, quad1.c, num);
                        quad3.a = quad2.d;
                        quad3.b = quad2.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad2);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2)) {
                            this.CheckOverlapX(bounds3, bounds2, quad2, triangle2, @int);
                        }

                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2)) {
                            this.CheckOverlapX(bounds4, bounds2, quad3, triangle2, int2);
                            return;
                        }
                    } else {
                        if (xxzz1.y - xxzz1.x >= 2) {
                            this.CheckOverlapX(bounds1, bounds2, quad1, triangle2, xxzz1);
                            return;
                        }

                        var num2 = xxzz1.z * this.m_BlockData.m_Size.x + xxzz1.x;
                        var cell = this.m_Cells[num2];
                        if ((cell.m_State & CellFlags.Blocked) != CellFlags.None) {
                            return;
                        }

                        quad1 = Colossal.Mathematics.MathUtils.Expand(quad1, -0.02f);
                        if (Colossal.Mathematics.MathUtils.Intersect(quad1, triangle2)) {
                            cell.m_State |= CellFlags.Blocked;
                            this.m_Cells[num2] = cell;
                        }
                    }
                }

                public Entity m_BlockEntity;
                public Block m_BlockData;
                public ValidArea m_ValidAreaData;
                public Colossal.Mathematics.Bounds2 m_Bounds;
                public Colossal.Mathematics.Quad2 m_Quad;
                public DynamicBuffer<Cell> m_Cells;
                public ComponentLookup<Native> m_NativeData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<AreaGeometryData> m_PrefabAreaGeometryData;
                public BufferLookup<Game.Areas.Node> m_AreaNodes;
                public BufferLookup<Triangle> m_AreaTriangles;
            }
        }
    }
}
