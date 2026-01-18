// <copyright file="P_NewCellCheckSystem.CustomCellsJob.cs" company="Luca Rager">
// Copyright (c) lucar. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Game.Prefabs;
    using Game.Zones;
    using Platter.Components;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using static Game.Zones.CellCheckHelpers;
    using BuildOrder = Game.Zones.BuildOrder;

    public partial class P_NewCellCheckSystem {
#if USE_BURST
        [BurstCompile]
#endif
        public struct CustomCellsJob : IJobParallelForDefer {
            [ReadOnly]                            public required NativeArray<OverlapGroup>    m_OverlapGroups;
            [NativeDisableParallelForRestriction] public required NativeArray<BlockOverlap>    m_BlockOverlaps;
            [ReadOnly]                            public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly]                            public required ComponentLookup<ParcelData>  m_ParcelDataLookup;
            [ReadOnly]                            public required ComponentLookup<PrefabRef>   m_PrefabRefLookup;
            [ReadOnly]                            public required ComponentLookup<Block>       m_BlockLookup;
            [ReadOnly]                            public required ComponentLookup<BuildOrder>  m_BuildOrderLookup;
            [NativeDisableParallelForRestriction] public required ComponentLookup<ValidArea>   m_ValidAreaLookup;
            [NativeDisableParallelForRestriction] public required BufferLookup<Cell>           m_CellLookup;

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];

                var overlapIterator = new OverlapIterator(
                                                          blockLookup: m_BlockLookup,
                                                          validAreaLookup: m_ValidAreaLookup,
                                                          buildOrderLookup: m_BuildOrderLookup,
                                                          cellsBufferLookup: m_CellLookup,
                                                          parcelOwnerLookup: m_ParcelOwnerLookup,
                                                          parcelDataLookup: m_ParcelDataLookup,
                                                          prefabRefLookup: m_PrefabRefLookup
                                                         );

                for (var n = overlapGroup.m_StartIndex; n < overlapGroup.m_EndIndex; n++) {
                    var blockOverlap   = m_BlockOverlaps[n];
                    var curBlockEntity = blockOverlap.m_Block;

                    if (curBlockEntity != overlapIterator.BlockEntity) {
                        overlapIterator.SetEntity(curBlockEntity);
                    }

                    if (overlapIterator.ValidArea.m_Area.y > overlapIterator.ValidArea.m_Area.x &&
                        blockOverlap.m_Other               != Entity.Null) {
                        overlapIterator.Iterate(blockOverlap.m_Other);
                    }
                }
            }

            private struct OverlapIterator {
                public Entity BlockEntity {
                    readonly get => m_CurBlockEntity;
                    set => m_CurBlockEntity = value;
                }

                public ValidArea ValidArea {
                    readonly get => m_CurValidArea;
                    set => m_CurValidArea = value;
                }

                private ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
                private ComponentLookup<ParcelData>  m_ParcelDataLookup;
                private ComponentLookup<PrefabRef>   m_PrefabRefLookup;
                private ComponentLookup<Block>       m_BlockLookup;
                private ComponentLookup<ValidArea>   m_ValidAreaLookup;
                private ComponentLookup<BuildOrder>  m_BuildOrderLookup;
                private BufferLookup<Cell>           m_CellBufferLookup;
                private Entity                       m_CurBlockEntity;
                private ValidArea                    m_CurValidArea;
                private Bounds2                      m_CurBounds;
                private Block                        m_CurBlock;
                private BuildOrder                   m_CurBuildOrder;
                private DynamicBuffer<Cell>          m_CurCellBuffer;
                private Quad2                        m_CurCorners;
                private bool                         m_CurBlockIsParcel;
                private int2                         m_CurBlockParcelBounds;
                private Entity                       m_OtherBlockEntity;
                private Block                        m_OtherBlock;
                private ValidArea                    m_OtherValidArea;
                private BuildOrder                   m_OtherBuildOrder;
                private DynamicBuffer<Cell>          m_OtherCellBuffer;
                private bool                         m_OtherBlockIsParcel;
                private int2                         m_OtherBlockParcelBounds;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerLookup, ComponentLookup<Block>      blockLookup,
                                       ComponentLookup<ValidArea>   validAreaLookup,   ComponentLookup<BuildOrder> buildOrderLookup,
                                       BufferLookup<Cell>           cellsBufferLookup, ComponentLookup<ParcelData> parcelDataLookup,
                                       ComponentLookup<PrefabRef>   prefabRefLookup) : this() {
                    m_ParcelOwnerLookup = parcelOwnerLookup;
                    m_BlockLookup       = blockLookup;
                    m_ValidAreaLookup   = validAreaLookup;
                    m_BuildOrderLookup  = buildOrderLookup;
                    m_CellBufferLookup  = cellsBufferLookup;
                    m_ParcelDataLookup  = parcelDataLookup;
                    m_PrefabRefLookup   = prefabRefLookup;
                }

                public void SetEntity(Entity curBlockEntity) {
                    var curValidArea = m_ValidAreaLookup[curBlockEntity];
                    var curBlock     = m_BlockLookup[curBlockEntity];
                    var curCorners   = ZoneUtils.CalculateCorners(curBlock, curValidArea);

                    // Set data
                    m_CurBlockEntity       = curBlockEntity;
                    m_CurValidArea         = curValidArea;
                    m_CurCorners           = curCorners;
                    m_CurBounds            = MathUtils.Bounds(curCorners);
                    m_CurBlock             = curBlock;
                    m_CurBuildOrder        = m_BuildOrderLookup[curBlockEntity];
                    m_CurCellBuffer        = m_CellBufferLookup[curBlockEntity];
                    m_CurBlockIsParcel     = m_ParcelOwnerLookup.HasComponent(curBlockEntity);
                    m_CurBlockParcelBounds = default;

                    if (m_CurBlockIsParcel) {
                        var prefab     = m_PrefabRefLookup[curBlockEntity];
                        var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                        m_CurBlockParcelBounds = parcelData.m_LotSize;
                    }
                }

                public void Iterate(Entity otherBlock) {
                    m_OtherBlockEntity       = otherBlock;
                    m_OtherBlock             = m_BlockLookup[otherBlock];
                    m_OtherValidArea         = m_ValidAreaLookup[otherBlock];
                    m_OtherBuildOrder        = m_BuildOrderLookup[otherBlock];
                    m_OtherCellBuffer        = m_CellBufferLookup[otherBlock];
                    m_OtherBlockIsParcel     = m_ParcelOwnerLookup.HasComponent(otherBlock);
                    m_OtherBlockParcelBounds = default;

                    if (m_OtherBlockIsParcel) {
                        var prefab     = m_PrefabRefLookup[otherBlock];
                        var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                        m_OtherBlockParcelBounds = parcelData.m_LotSize;
                    }

                    if (!m_CurBlockIsParcel && !m_OtherBlockIsParcel) {
                        return;
                    }

                    if (m_OtherValidArea.m_Area.y <= m_OtherValidArea.m_Area.x) {
                        return;
                    }

                    if (!ZoneUtils.CanShareCells(m_CurBlock, m_OtherBlock, m_CurBuildOrder, m_OtherBuildOrder)) {
                        return;
                    }

                    var otherBlockCorners = ZoneUtils.CalculateCorners(m_OtherBlock, m_OtherValidArea);

                    // Recursively iterate over cells
                    CheckOverlapX1(
                                   m_CurBounds,
                                   MathUtils.Bounds(otherBlockCorners),
                                   m_CurCorners,
                                   otherBlockCorners,
                                   m_CurValidArea.m_Area,
                                   m_OtherValidArea.m_Area);
                }

                private void CheckOverlapX1(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4    validArea,   int4    otherValidArea) {
                    // If the X-range of the region spans 2 or more cells, split it into two subregions and recurse.
                    if (validArea.y - validArea.x >= 2) {
                        var leftArea  = validArea;
                        var rightArea = validArea;
                        leftArea.y  = (validArea.x + validArea.y) >> 1;
                        rightArea.x = leftArea.y;

                        var leftCorners  = blockCorners;
                        var rightCorners = blockCorners;

                        var t = (leftArea.y - validArea.x) / (float)(validArea.y - validArea.x);

                        leftCorners.b  = math.lerp(blockCorners.a, blockCorners.b, t);
                        leftCorners.c  = math.lerp(blockCorners.d, blockCorners.c, t);
                        rightCorners.a = leftCorners.b;
                        rightCorners.d = leftCorners.c;

                        var leftBounds  = MathUtils.Bounds(leftCorners);
                        var rightBounds = MathUtils.Bounds(rightCorners);
                        if (MathUtils.Intersect(leftBounds, otherBounds)) {
                            CheckOverlapZ1(leftBounds, otherBounds, leftCorners, otherCorners, leftArea, otherValidArea);
                        }

                        if (MathUtils.Intersect(rightBounds, otherBounds)) {
                            CheckOverlapZ1(rightBounds, otherBounds, rightCorners, otherCorners, rightArea, otherValidArea);
                        }

                        return;
                    }

                    // Base case: X-range is a single column
                    CheckOverlapZ1(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                }

                private void CheckOverlapZ1(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4    validArea,   int4    otherValidArea) {
                    // If the Z-range of the region spans 2 or more cells, split into two subregions and recurse.
                    if (validArea.w - validArea.z >= 2) {
                        var topArea    = validArea;
                        var bottomArea = validArea;
                        topArea.w    = (validArea.z + validArea.w) >> 1;
                        bottomArea.z = topArea.w;

                        var topCorners    = blockCorners;
                        var bottomCorners = blockCorners;
                        var t             = (topArea.w - validArea.z) / (float)(validArea.w - validArea.z);

                        topCorners.d = math.lerp(blockCorners.a, blockCorners.d, t);
                        topCorners.c = math.lerp(blockCorners.b, blockCorners.c, t);

                        bottomCorners.a = topCorners.d;
                        bottomCorners.b = topCorners.c;
                        var topBounds    = MathUtils.Bounds(topCorners);
                        var bottomBounds = MathUtils.Bounds(bottomCorners);

                        if (MathUtils.Intersect(topBounds, otherBounds)) {
                            CheckOverlapX2(topBounds, otherBounds, topCorners, otherCorners, topArea, otherValidArea);
                        }

                        if (MathUtils.Intersect(bottomBounds, otherBounds)) {
                            CheckOverlapX2(bottomBounds, otherBounds, bottomCorners, otherCorners, bottomArea, otherValidArea);
                        }

                        return;
                    }

                    CheckOverlapX2(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                }

                private void CheckOverlapX2(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4    validArea,   int4    otherValidArea) {
                    // If the other block's X-range spans multiple cells, subdivide other block and recurse.
                    if (otherValidArea.y - otherValidArea.x >= 2) {
                        var otherLeftArea  = otherValidArea;
                        var otherRightArea = otherValidArea;
                        otherLeftArea.y  = (otherValidArea.x + otherValidArea.y) >> 1;
                        otherRightArea.x = otherLeftArea.y;

                        var otherLeftCorners  = otherCorners;
                        var otherRightCorners = otherCorners;

                        var t = (otherLeftArea.y - otherValidArea.x) / (float)(otherValidArea.y - otherValidArea.x);

                        otherLeftCorners.b  = math.lerp(otherCorners.a, otherCorners.b, t);
                        otherLeftCorners.c  = math.lerp(otherCorners.d, otherCorners.c, t);
                        otherRightCorners.a = otherLeftCorners.b;
                        otherRightCorners.d = otherLeftCorners.c;

                        var otherLeftBounds  = MathUtils.Bounds(otherLeftCorners);
                        var otherRightBounds = MathUtils.Bounds(otherRightCorners);

                        if (MathUtils.Intersect(blockBounds, otherLeftBounds)) {
                            CheckOverlapZ2(
                                           blockBounds,
                                           otherLeftBounds,
                                           blockCorners,
                                           otherLeftCorners,
                                           validArea,
                                           otherLeftArea);
                        }

                        if (MathUtils.Intersect(blockBounds, otherRightBounds)) {
                            CheckOverlapZ2(
                                           blockBounds,
                                           otherRightBounds,
                                           blockCorners,
                                           otherRightCorners,
                                           validArea,
                                           otherRightArea);
                        }

                        return;
                    }

                    // Base case: other block's X-range is a single column
                    CheckOverlapZ2(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                }

                private void CheckOverlapZ2(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4    validArea,   int4    otherValidArea) {
                    // If second region spans more than one Z row, split it and recurse.
                    if (otherValidArea.w - otherValidArea.z >= 2) {
                        var otherTopArea    = otherValidArea;
                        var otherBottomArea = otherValidArea;

                        otherTopArea.w    = (otherValidArea.z + otherValidArea.w) >> 1;
                        otherBottomArea.z = otherTopArea.w;

                        var otherTopCorners    = otherCorners;
                        var otherBottomCorners = otherCorners;
                        var t                  = (otherTopArea.w - otherValidArea.z) / (float)(otherValidArea.w - otherValidArea.z);

                        // lerp to get subdivided quads along Z
                        otherTopCorners.d    = math.lerp(otherCorners.a, otherCorners.d, t);
                        otherTopCorners.c    = math.lerp(otherCorners.b, otherCorners.c, t);
                        otherBottomCorners.a = otherTopCorners.d;
                        otherBottomCorners.b = otherTopCorners.c;

                        var topBounds    = MathUtils.Bounds(otherTopCorners);
                        var bottomBounds = MathUtils.Bounds(otherBottomCorners);

                        if (MathUtils.Intersect(blockBounds, topBounds)) {
                            CheckOverlapX1(blockBounds, topBounds, blockCorners, otherTopCorners, validArea, otherTopArea);
                        }

                        if (MathUtils.Intersect(blockBounds, bottomBounds)) {
                            CheckOverlapX1(
                                           blockBounds,
                                           bottomBounds,
                                           blockCorners,
                                           otherBottomCorners,
                                           validArea,
                                           otherBottomArea);
                        }

                        return;
                    }

                    // If either region still spans multiple X or Z cells, pass control back up to CheckOverlapX1.
                    // math.any used to test both components. Use logical OR to be explicit.
                    if (math.any(validArea.yw - validArea.xz >= 2) || math.any(otherValidArea.yw - otherValidArea.xz >= 2)) {
                        CheckOverlapX1(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                        return;
                    }

                    // Now both regions are single cells: compute flat buffer indices.
                    var curIndex   = validArea.z      * m_CurBlock.m_Size.x   + validArea.x;
                    var otherIndex = otherValidArea.z * m_OtherBlock.m_Size.x + otherValidArea.x;

                    var curCell   = m_CurCellBuffer[curIndex];
                    var otherCell = m_OtherCellBuffer[otherIndex];

                    // In sharing mode we only allow sharing when cell centers are very close.
                    if (!(math.lengthsq(MathUtils.Center(blockCorners) - MathUtils.Center(otherCorners)) < 16f)) {
                        return;
                    }

                    if (m_CurBlockIsParcel) {
                        otherCell.m_Zone  = ZoneType.None;
                        otherCell.m_State = CellFlags.Redundant | CellFlags.Blocked;
                    } else if (m_OtherBlockIsParcel) {
                        curCell.m_Zone  = ZoneType.None;
                        curCell.m_State = CellFlags.Redundant | CellFlags.Blocked;
                    }

                    m_CurCellBuffer[curIndex]     = curCell;
                    m_OtherCellBuffer[otherIndex] = otherCell;
                }
            }
        }
    }
}
