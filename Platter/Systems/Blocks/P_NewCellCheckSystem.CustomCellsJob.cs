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

    public partial class P_NewCellCheckSystem {
#if USE_BURST
        [BurstCompile]
#endif
        public struct CustomCellsJob : IJobParallelForDefer {
            [ReadOnly] public required NativeArray<OverlapGroup> m_OverlapGroups;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData> m_ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<Block> m_BlockLookup;
            [NativeDisableParallelForRestriction] public required ComponentLookup<ValidArea> m_ValidAreaLookup;
            [NativeDisableParallelForRestriction] public required NativeArray<BlockOverlap> m_BlockOverlaps;
            [NativeDisableParallelForRestriction] public required BufferLookup<Cell> m_CellLookup;

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];

                var overlapIterator = new OverlapIterator(
                                                          parcelOwnerLookup: m_ParcelOwnerLookup,
                                                          parcelDataLookup: m_ParcelDataLookup,
                                                          prefabRefLookup: m_PrefabRefLookup,
                                                          blockLookup: m_BlockLookup,
                                                          validAreaLookup: m_ValidAreaLookup,
                                                          cellsBufferLookup: m_CellLookup
                                                         );

                for (var n = overlapGroup.m_StartIndex; n < overlapGroup.m_EndIndex; n++) {
                    var blockOverlap = m_BlockOverlaps[n];
                    var curBlockEntity = blockOverlap.m_Block;

                    if (curBlockEntity != overlapIterator.BlockEntity) {
                        overlapIterator.SetEntity(curBlockEntity);
                    }

                    if (overlapIterator.ValidArea.m_Area.y > overlapIterator.ValidArea.m_Area.x &&
                        blockOverlap.m_Other != Entity.Null) {
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
                private ComponentLookup<ParcelData> m_ParcelDataLookup;
                private ComponentLookup<PrefabRef> m_PrefabRefLookup;
                private ComponentLookup<Block> m_BlockLookup;
                private ComponentLookup<ValidArea> m_ValidAreaLookup;
                private BufferLookup<Cell> m_CellBufferLookup;
                private Entity m_CurBlockEntity;
                private ValidArea m_CurValidArea;
                private Bounds2 m_CurBounds;
                private Block m_CurBlock;
                private DynamicBuffer<Cell> m_CurCellBuffer;
                private Quad2 m_CurCorners;
                private bool m_CurBlockIsParcel;
                private Block m_OtherBlock;
                private DynamicBuffer<Cell> m_OtherCellBuffer;
                private bool m_OtherBlockIsParcel;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerLookup, ComponentLookup<ParcelData> parcelDataLookup,
                                       ComponentLookup<PrefabRef> prefabRefLookup, ComponentLookup<Block> blockLookup,
                                       ComponentLookup<ValidArea> validAreaLookup, BufferLookup<Cell> cellsBufferLookup) : this() {
                    m_ParcelOwnerLookup = parcelOwnerLookup;
                    m_ParcelDataLookup = parcelDataLookup;
                    m_PrefabRefLookup = prefabRefLookup;
                    m_BlockLookup = blockLookup;
                    m_ValidAreaLookup = validAreaLookup;
                    m_CellBufferLookup = cellsBufferLookup;
                }

                public void SetEntity(Entity curBlockEntity) {
                    var curValidArea = m_ValidAreaLookup[curBlockEntity];
                    var curBlock = m_BlockLookup[curBlockEntity];
                    var isParcel = m_ParcelOwnerLookup.TryGetComponent(curBlockEntity, out var parcelOwner);

                    // Clamp the valid area to the actual lot size so that
                    // e.g. a 1-wide parcel with a 2-wide block only overlaps 1 column.
                    if (isParcel) {
                        var parcelData = m_ParcelDataLookup[m_PrefabRefLookup[parcelOwner.m_Owner].m_Prefab];
                        curValidArea.m_Area.y = math.min(curValidArea.m_Area.y, parcelData.m_LotSize.x);
                        curValidArea.m_Area.w = math.min(curValidArea.m_Area.w, parcelData.m_LotSize.y);
                    }

                    var curCorners = ZoneUtils.CalculateCorners(curBlock, curValidArea);

                    m_CurBlockEntity = curBlockEntity;
                    m_CurValidArea = curValidArea;
                    m_CurCorners = curCorners;
                    m_CurBounds = MathUtils.Bounds(curCorners);
                    m_CurBlock = curBlock;
                    m_CurCellBuffer = m_CellBufferLookup[curBlockEntity];
                    m_CurBlockIsParcel = isParcel;
                }

                public void Iterate(Entity otherBlockEntity) {
                    m_OtherBlock = m_BlockLookup[otherBlockEntity];
                    m_OtherCellBuffer = m_CellBufferLookup[otherBlockEntity];
                    m_OtherBlockIsParcel = m_ParcelOwnerLookup.TryGetComponent(otherBlockEntity, out var otherParcelOwner);

                    // Only process pairs where exactly one block is a parcel.
                    // Skip when neither is a parcel (nothing to override) and when
                    // both are parcels (they should not affect each other).
                    if (m_CurBlockIsParcel == m_OtherBlockIsParcel) {
                        return;
                    }

                    var otherValidArea = m_ValidAreaLookup[otherBlockEntity];

                    // Clamp the valid area to the actual lot size (mirrors the clamp in SetEntity).
                    if (m_OtherBlockIsParcel) {
                        var parcelData = m_ParcelDataLookup[m_PrefabRefLookup[otherParcelOwner.m_Owner].m_Prefab];
                        otherValidArea.m_Area.y = math.min(otherValidArea.m_Area.y, parcelData.m_LotSize.x);
                        otherValidArea.m_Area.w = math.min(otherValidArea.m_Area.w, parcelData.m_LotSize.y);
                    }

                    if (otherValidArea.m_Area.y <= otherValidArea.m_Area.x) {
                        return;
                    }

                    var otherCorners = ZoneUtils.CalculateCorners(m_OtherBlock, otherValidArea);
                    var otherBounds = MathUtils.Bounds(otherCorners);

                    // Quick bounding-box rejection before entering the recursion.
                    if (!MathUtils.Intersect(m_CurBounds, otherBounds)) {
                        return;
                    }

                    // Recursively subdivide both blocks down to individual cells
                    // and block every non-parcel cell that physically overlaps a parcel cell.
                    CheckOverlapX1(
                                   m_CurBounds,
                                   otherBounds,
                                   m_CurCorners,
                                   otherCorners,
                                   m_CurValidArea.m_Area,
                                   otherValidArea.m_Area);
                }

                private void CheckOverlapX1(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4 validArea, int4 otherValidArea) {
                    // If the X-range of the region spans 2 or more cells, split it into two subregions and recurse.
                    if (validArea.y - validArea.x >= 2) {
                        var leftArea = validArea;
                        var rightArea = validArea;
                        leftArea.y = (validArea.x + validArea.y) >> 1;
                        rightArea.x = leftArea.y;

                        var leftCorners = blockCorners;
                        var rightCorners = blockCorners;

                        var t = (leftArea.y - validArea.x) / (float)(validArea.y - validArea.x);

                        leftCorners.b = math.lerp(blockCorners.a, blockCorners.b, t);
                        leftCorners.c = math.lerp(blockCorners.d, blockCorners.c, t);
                        rightCorners.a = leftCorners.b;
                        rightCorners.d = leftCorners.c;

                        var leftBounds = MathUtils.Bounds(leftCorners);
                        var rightBounds = MathUtils.Bounds(rightCorners);
                        if (MathUtils.Intersect(leftBounds, otherBounds)) {
                            CheckOverlapZ1(leftBounds, otherBounds, leftCorners, otherCorners, leftArea, otherValidArea);
                        }

                        if (MathUtils.Intersect(rightBounds, otherBounds)) {
                            CheckOverlapZ1(rightBounds, otherBounds, rightCorners, otherCorners, rightArea, otherValidArea);
                        }

                        return;
                    }

                    // Base case: X-range is a single column.
                    CheckOverlapZ1(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                }

                private void CheckOverlapZ1(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4 validArea, int4 otherValidArea) {
                    // If the Z-range of the region spans 2 or more cells, split into two subregions and recurse.
                    if (validArea.w - validArea.z >= 2) {
                        var topArea = validArea;
                        var bottomArea = validArea;
                        topArea.w = (validArea.z + validArea.w) >> 1;
                        bottomArea.z = topArea.w;

                        var topCorners = blockCorners;
                        var bottomCorners = blockCorners;
                        var t = (topArea.w - validArea.z) / (float)(validArea.w - validArea.z);

                        topCorners.d = math.lerp(blockCorners.a, blockCorners.d, t);
                        topCorners.c = math.lerp(blockCorners.b, blockCorners.c, t);

                        bottomCorners.a = topCorners.d;
                        bottomCorners.b = topCorners.c;
                        var topBounds = MathUtils.Bounds(topCorners);
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
                                            int4 validArea, int4 otherValidArea) {
                    // If the other block's X-range spans multiple cells, subdivide other block and recurse.
                    if (otherValidArea.y - otherValidArea.x >= 2) {
                        var otherLeftArea = otherValidArea;
                        var otherRightArea = otherValidArea;
                        otherLeftArea.y = (otherValidArea.x + otherValidArea.y) >> 1;
                        otherRightArea.x = otherLeftArea.y;

                        var otherLeftCorners = otherCorners;
                        var otherRightCorners = otherCorners;

                        var t = (otherLeftArea.y - otherValidArea.x) / (float)(otherValidArea.y - otherValidArea.x);

                        otherLeftCorners.b = math.lerp(otherCorners.a, otherCorners.b, t);
                        otherLeftCorners.c = math.lerp(otherCorners.d, otherCorners.c, t);
                        otherRightCorners.a = otherLeftCorners.b;
                        otherRightCorners.d = otherLeftCorners.c;

                        var otherLeftBounds = MathUtils.Bounds(otherLeftCorners);
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

                    // Base case: other block's X-range is a single column.
                    CheckOverlapZ2(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                }

                private void CheckOverlapZ2(Bounds2 blockBounds, Bounds2 otherBounds, Quad2 blockCorners, Quad2 otherCorners,
                                            int4 validArea, int4 otherValidArea) {
                    // If second region spans more than one Z row, split it and recurse.
                    if (otherValidArea.w - otherValidArea.z >= 2) {
                        var otherTopArea = otherValidArea;
                        var otherBottomArea = otherValidArea;

                        otherTopArea.w = (otherValidArea.z + otherValidArea.w) >> 1;
                        otherBottomArea.z = otherTopArea.w;

                        var otherTopCorners = otherCorners;
                        var otherBottomCorners = otherCorners;
                        var t = (otherTopArea.w - otherValidArea.z) / (float)(otherValidArea.w - otherValidArea.z);

                        otherTopCorners.d = math.lerp(otherCorners.a, otherCorners.d, t);
                        otherTopCorners.c = math.lerp(otherCorners.b, otherCorners.c, t);
                        otherBottomCorners.a = otherTopCorners.d;
                        otherBottomCorners.b = otherTopCorners.c;

                        var topBounds = MathUtils.Bounds(otherTopCorners);
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
                    if (math.any(validArea.yw - validArea.xz >= 2) || math.any(otherValidArea.yw - otherValidArea.xz >= 2)) {
                        CheckOverlapX1(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                        return;
                    }

                    // Both regions are now single cells. Check actual geometric overlap
                    // between the two cell quads (shrunk slightly to avoid false positives at exact edges).
                    if (!MathUtils.Intersect(MathUtils.Expand(blockCorners, -0.01f), MathUtils.Expand(otherCorners, -0.01f))) {
                        return;
                    }

                    // Parcel cells always take priority: block the non-parcel cell
                    // and propagate the block to all deeper rows in that column.
                    if (m_CurBlockIsParcel) {
                        BlockColumnFromRow(ref m_OtherCellBuffer, m_OtherBlock.m_Size, otherValidArea.x, otherValidArea.z);
                    } else if (m_OtherBlockIsParcel) {
                        BlockColumnFromRow(ref m_CurCellBuffer, m_CurBlock.m_Size, validArea.x, validArea.z);
                    }
                }

                /// <summary>
                /// Blocks the cell at (<paramref name="col"/>, <paramref name="startRow"/>) and
                /// propagates the block to all deeper rows in the same column, mirroring the
                /// forward-propagation logic in vanilla CleanBlockedCells.
                /// </summary>
                private static void BlockColumnFromRow(ref DynamicBuffer<Cell> cellBuffer, int2 blockSize, int col, int startRow) {
                    // Mark the overlapping cell as redundant and blocked.
                    var index = startRow * blockSize.x + col;
                    var cell = cellBuffer[index];
                    cell.m_Zone = ZoneType.None;
                    cell.m_State = CellFlags.Redundant | CellFlags.Blocked;
                    cellBuffer[index] = cell;

                    // Propagate blocking to deeper rows (same semantics as CleanBlockedCells).
                    for (var row = startRow + 1; row < blockSize.y; row++) {
                        index = row * blockSize.x + col;
                        cell = cellBuffer[index];
                        if ((cell.m_State & CellFlags.Blocked) != CellFlags.None) {
                            break;
                        }

                        cell.m_Zone = ZoneType.None;
                        cell.m_State |= CellFlags.Blocked;
                        cellBuffer[index] = cell;
                    }
                }
            }
        }
    }
}