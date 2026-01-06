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
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Game.Prefabs;
    using Colossal.Collections;
    using Unity.Jobs;

    public partial class P_NewCellCheckSystem {
        /// <summary>
        /// Similar to base-game BlockCellsJob, but adapted to Platter's zoning system.
        /// Normalizes parcel cells and checks for conflicts with Net and Area geometry to mark blocked cells.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct BlockCellsJob : IJobParallelForDefer {
            [ReadOnly] public required NativeArray<CellCheckHelpers.SortedEntity> m_Blocks;
            [ReadOnly] public required ComponentLookup<Block> m_BlockLookup;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData> m_ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> m_PrefabRefLookup;
            [NativeDisableParallelForRestriction]
            public required BufferLookup<Cell> m_CellsLookup;
            [NativeDisableParallelForRestriction]
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

                if (parcelData.m_LotSize.x > 1) {
                    // Normalize "wide" parcel's cells.
                    NormalizeWideParcelCells(block, parcelData, cellBuffer);
                } else {
                    // Normalize "narrow" parcel's cells so they are processed from a clean state.
                    NormalizeNarrowParcelCells(block, parcelData, cellBuffer);
                }

                // Process the results, calculating the final valid area.
                CleanBlockedCells(block, ref validArea, cellBuffer);

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
            #if USE_BURST
            [BurstCompile]
            #endif
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
            #if USE_BURST
            [BurstCompile]
            #endif
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
            #if USE_BURST
            [BurstCompile]
            #endif
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
        }
    }
}
