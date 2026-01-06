// <copyright file="P_NewCellCheckSystem.CheckBlockOverlapJob.cs" company="Luca Rager">
// Copyright (c) lucar. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using static Game.Zones.CellCheckHelpers;
    using Unity.Burst;
    public partial class P_NewCellCheckSystem {
        /// <summary>
        /// Modified copy of Game.Zones.CellOverlapJobs.CheckBlockOverlapJob from the base game.
        /// Checks for overlaps between blocks within a group and updates cell states accordingly.
        ///
        /// CUSTOM MODIFICATIONS:
        /// 1. Narrow Parcel Protection - Added IsNarrowParcel() and m_IsNarrowParcel flag to CellReduction.
        ///    When a block belongs to a 1-wide parcel, depth reduction is skipped entirely.
        /// 2. Parcel Zone Clearing - Added m_CurBlockIsParcel/m_OtherBlockIsParcel to OverlapIterator.
        ///    When one block is a parcel, we clear the zone from the non-parcel
        ///    block's cells instead of copying zones.
        ///
        /// ADDED FIELDS: m_ParcelOwnerData, m_PrefabRefData, m_ParcelData
        /// MODIFIED STRUCTS: CellReduction (m_IsNarrowParcel), OverlapIterator (parcel detection + zone clearing)
        /// </summary>
        #if USE_BURST
        [BurstCompile]
        #endif
        public struct CheckBlockOverlapJob : Unity.Jobs.IJobParallelForDefer {
            // [CUSTOM] Checks if a block belongs to a narrow (1-wide) parcel
            private bool IsNarrowParcel(Unity.Entities.Entity blockEntity) {
                if (m_ParcelOwnerData.TryGetComponent(blockEntity, out var parcelOwner)     &&
                    m_PrefabRefData.TryGetComponent(parcelOwner.m_Owner, out var prefabRef) &&
                    m_ParcelData.TryGetComponent(prefabRef.m_Prefab, out var parcelData)) {
                    return parcelData.m_LotSize.x == 1;
                }

                return false;
            }

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];
                var blockOverlap = default(Game.Zones.CellCheckHelpers.BlockOverlap);
                var num          = 0;
                var block        = default(Game.Zones.Block);
                var buildOrder   = default(Game.Zones.BuildOrder);

                // Phase 1: Identify Neighbors
                // Iterate through overlaps to identify left and right neighbors for each block.
                for (var i = overlapGroup.m_StartIndex; i < overlapGroup.m_EndIndex; i++) {
                    var blockOverlap2 = m_BlockOverlaps[i];
                    if (blockOverlap2.m_Block != blockOverlap.m_Block) {
                        if (blockOverlap.m_Block != Unity.Entities.Entity.Null) m_BlockOverlaps[num] = blockOverlap;
                        blockOverlap = blockOverlap2;
                        num          = i;
                        block        = m_BlockData[blockOverlap2.m_Block];
                        var validArea = m_ValidAreaData[blockOverlap2.m_Block];
                        buildOrder = m_BuildOrderData[blockOverlap2.m_Block];
                        var dynamicBuffer = m_Cells[blockOverlap2.m_Block];
                    }

                    if (blockOverlap2.m_Other != Unity.Entities.Entity.Null) {
                        var block2      = m_BlockData[blockOverlap2.m_Other];
                        var buildOrder2 = m_BuildOrderData[blockOverlap2.m_Other];

                        // Check if the other block is a valid neighbor and determine direction (Left/Right)
                        if (Game.Zones.ZoneUtils.IsNeighbor(block, block2, buildOrder, buildOrder2)) {
                            if (Unity.Mathematics.math.dot(block2.m_Position.xz - block.m_Position.xz,
                                                           Colossal.Mathematics.MathUtils.Right(block.m_Direction)) > 0f)
                                blockOverlap.m_Left = blockOverlap2.m_Other;
                            else
                                blockOverlap.m_Right = blockOverlap2.m_Other;
                        }
                    }
                }

                if (blockOverlap.m_Block != Unity.Entities.Entity.Null) m_BlockOverlaps[num] = blockOverlap;

                Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Start (Primary)", blockOverlap.m_Block, m_Cells[blockOverlap.m_Block]);
                Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Start (Other)", blockOverlap.m_Other, m_Cells[blockOverlap.m_Other]);

                // Setup iterators for processing overlaps
                var overlapIterator = default(OverlapIterator);
                overlapIterator.m_BlockDataFromEntity      = m_BlockData;
                overlapIterator.m_ValidAreaDataFromEntity  = m_ValidAreaData;
                overlapIterator.m_BuildOrderDataFromEntity = m_BuildOrderData;
                overlapIterator.m_CellsFromEntity          = m_Cells;
                overlapIterator.m_ParcelOwnerData          = m_ParcelOwnerData; // [CUSTOM]

                var cellReduction = default(CellReduction);
                cellReduction.m_BlockDataFromEntity      = m_BlockData;
                cellReduction.m_ValidAreaDataFromEntity  = m_ValidAreaData;
                cellReduction.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction.m_CellsFromEntity          = m_Cells;

                // Phase 2: Mark Redundant Cells (First Pass)
                // Propagate redundancy flags based on initial overlap data.
                cellReduction.m_Flag = Game.Zones.CellFlags.Redundant;
                for (var j = overlapGroup.m_StartIndex; j < overlapGroup.m_EndIndex; j++) {
                    var blockOverlap3 = m_BlockOverlaps[j];
                    if (blockOverlap3.m_Block != overlapIterator.m_BlockEntity) {
                        if (cellReduction.m_BlockEntity != Unity.Entities.Entity.Null) {
                            cellReduction.Perform();
                            Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #0", cellReduction.m_BlockEntity, m_Cells[cellReduction.m_BlockEntity]);
                        }
                        cellReduction.m_BlockEntity        = blockOverlap3.m_Block;
                        cellReduction.m_LeftNeightbor      = blockOverlap3.m_Left;
                        cellReduction.m_RightNeightbor     = blockOverlap3.m_Right;
                        cellReduction.m_IsNarrowParcel     = IsNarrowParcel(blockOverlap3.m_Block); // [CUSTOM]
                        overlapIterator.m_BlockEntity      = blockOverlap3.m_Block;
                        overlapIterator.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap3.m_Block); // [CUSTOM]
                        overlapIterator.m_BlockData        = m_BlockData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_ValidAreaData    = m_ValidAreaData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_BuildOrderData   = m_BuildOrderData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Cells            = m_Cells[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Quad = Game.Zones.ZoneUtils.CalculateCorners(overlapIterator.m_BlockData,
                                                                                       overlapIterator.m_ValidAreaData);
                        overlapIterator.m_Bounds = Colossal.Mathematics.MathUtils.Bounds(overlapIterator.m_Quad);
                    }

                    // Check for geometric overlaps if valid area exists
                    if (overlapIterator.m_ValidAreaData.m_Area.y > overlapIterator.m_ValidAreaData.m_Area.x &&
                        blockOverlap3.m_Other                    != Unity.Entities.Entity.Null) {
                        overlapIterator.Iterate(blockOverlap3.m_Other);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #1 (Primary)", blockOverlap3.m_Block, m_Cells[blockOverlap3.m_Block]);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #1 (Other)", blockOverlap3.m_Other, m_Cells[blockOverlap3.m_Other]);
                    }
                }

                if (cellReduction.m_BlockEntity != Unity.Entities.Entity.Null) {
                    cellReduction.Perform();
                    Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #1", cellReduction.m_BlockEntity, m_Cells[cellReduction.m_BlockEntity]);
                }

                // Phase 3: Check Physical Overlaps (Blocking)
                // Iterate again to mark cells as Blocked where physical overlaps occur.
                overlapIterator.m_BlockEntity   = Unity.Entities.Entity.Null;
                overlapIterator.m_CheckBlocking = true;
                cellReduction.m_BlockEntity     = Unity.Entities.Entity.Null;
                for (var k = overlapGroup.m_StartIndex; k < overlapGroup.m_EndIndex; k++) {
                    var blockOverlap4 = m_BlockOverlaps[k];
                    if (blockOverlap4.m_Block != overlapIterator.m_BlockEntity) {
                        if (cellReduction.m_BlockEntity != Unity.Entities.Entity.Null) {
                            // Clear redundant flags before applying blocked flags to ensure accuracy
                            cellReduction.m_Flag = Game.Zones.CellFlags.Redundant;
                            cellReduction.Clear();
                            cellReduction.m_Flag = Game.Zones.CellFlags.Blocked;
                            cellReduction.Perform();
                            Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #2", cellReduction.m_BlockEntity, m_Cells[cellReduction.m_BlockEntity]);
                        }

                        cellReduction.m_BlockEntity        = blockOverlap4.m_Block;
                        cellReduction.m_LeftNeightbor      = blockOverlap4.m_Left;
                        cellReduction.m_RightNeightbor     = blockOverlap4.m_Right;
                        cellReduction.m_IsNarrowParcel     = IsNarrowParcel(blockOverlap4.m_Block);
                        overlapIterator.m_BlockEntity      = blockOverlap4.m_Block;
                        overlapIterator.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap4.m_Block);
                        overlapIterator.m_BlockData        = m_BlockData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_ValidAreaData    = m_ValidAreaData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_BuildOrderData   = m_BuildOrderData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Cells            = m_Cells[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Quad = Game.Zones.ZoneUtils.CalculateCorners(overlapIterator.m_BlockData,
                                                                                       overlapIterator.m_ValidAreaData);
                        overlapIterator.m_Bounds = Colossal.Mathematics.MathUtils.Bounds(overlapIterator.m_Quad);
                    }

                    if (overlapIterator.m_ValidAreaData.m_Area.y > overlapIterator.m_ValidAreaData.m_Area.x &&
                        blockOverlap4.m_Other                    != Unity.Entities.Entity.Null) {
                        overlapIterator.Iterate(blockOverlap4.m_Other);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #2 (Primary)", blockOverlap4.m_Block, m_Cells[blockOverlap4.m_Block]);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #2 (Other)", blockOverlap4.m_Other, m_Cells[blockOverlap4.m_Other]);
                    }
                }

                if (cellReduction.m_BlockEntity != Unity.Entities.Entity.Null) {
                    cellReduction.m_Flag = Game.Zones.CellFlags.Redundant;
                    cellReduction.Clear();
                    cellReduction.m_Flag = Game.Zones.CellFlags.Blocked;
                    cellReduction.Perform();
                    Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #3", cellReduction.m_BlockEntity, m_Cells[cellReduction.m_BlockEntity]);
                }

                // Phase 4: Redundant Cleanup
                // Final pass to ensure redundant cells are correctly marked after blocking logic.
                var cellReduction2 = default(CellReduction);
                cellReduction2.m_BlockDataFromEntity      = m_BlockData;
                cellReduction2.m_ValidAreaDataFromEntity  = m_ValidAreaData;
                cellReduction2.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction2.m_CellsFromEntity          = m_Cells;
                cellReduction2.m_Flag                     = Game.Zones.CellFlags.Redundant;
                for (var l = overlapGroup.m_StartIndex; l < overlapGroup.m_EndIndex; l++) {
                    var blockOverlap5 = m_BlockOverlaps[l];
                    if (blockOverlap5.m_Block != cellReduction2.m_BlockEntity) {
                        cellReduction2.m_BlockEntity    = blockOverlap5.m_Block;
                        cellReduction2.m_LeftNeightbor  = blockOverlap5.m_Left;
                        cellReduction2.m_RightNeightbor = blockOverlap5.m_Right;
                        cellReduction2.m_IsNarrowParcel = IsNarrowParcel(blockOverlap5.m_Block);
                        cellReduction2.Perform();
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #4", cellReduction2.m_BlockEntity, m_Cells[cellReduction2.m_BlockEntity]);
                    }
                }

                // Phase 5: Process Occupied Cells
                // Handle cells that are already occupied (e.g., by buildings), adjusting depth based on neighbors.
                var cellReduction3 = default(CellReduction);
                cellReduction3.m_ZonePrefabs              = m_ZonePrefabs;
                cellReduction3.m_BlockDataFromEntity      = m_BlockData;
                cellReduction3.m_ValidAreaDataFromEntity  = m_ValidAreaData;
                cellReduction3.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction3.m_ZoneData                 = m_ZoneData;
                cellReduction3.m_CellsFromEntity          = m_Cells;
                cellReduction3.m_Flag                     = Game.Zones.CellFlags.Occupied;
                for (var m = overlapGroup.m_StartIndex; m < overlapGroup.m_EndIndex; m++) {
                    var blockOverlap6 = m_BlockOverlaps[m];
                    if (blockOverlap6.m_Block != cellReduction3.m_BlockEntity) {
                        cellReduction3.m_BlockEntity    = blockOverlap6.m_Block;
                        cellReduction3.m_LeftNeightbor  = blockOverlap6.m_Left;
                        cellReduction3.m_RightNeightbor = blockOverlap6.m_Right;
                        cellReduction3.m_IsNarrowParcel = IsNarrowParcel(blockOverlap6.m_Block);
                        cellReduction3.Perform();
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] CellRed #5", cellReduction3.m_BlockEntity, m_Cells[cellReduction3.m_BlockEntity]);
                    }
                }

                // Phase 6: Check Cell Sharing
                // Determine if cells can be shared between blocks (e.g., corner lots).
                var overlapIterator2 = default(OverlapIterator);
                overlapIterator2.m_BlockDataFromEntity      = m_BlockData;
                overlapIterator2.m_ValidAreaDataFromEntity  = m_ValidAreaData;
                overlapIterator2.m_BuildOrderDataFromEntity = m_BuildOrderData;
                overlapIterator2.m_CellsFromEntity          = m_Cells;
                overlapIterator2.m_ParcelOwnerData          = m_ParcelOwnerData;
                overlapIterator2.m_CheckSharing             = true;
                for (var n = overlapGroup.m_StartIndex; n < overlapGroup.m_EndIndex; n++) {
                    var blockOverlap7 = m_BlockOverlaps[n];
                    if (blockOverlap7.m_Block != overlapIterator2.m_BlockEntity) {
                        overlapIterator2.m_BlockEntity      = blockOverlap7.m_Block;
                        overlapIterator2.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap7.m_Block);
                        overlapIterator2.m_BlockData        = m_BlockData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_ValidAreaData    = m_ValidAreaData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_BuildOrderData   = m_BuildOrderData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_Cells            = m_Cells[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_Quad = Game.Zones.ZoneUtils.CalculateCorners(overlapIterator2.m_BlockData,
                                                                                        overlapIterator2.m_ValidAreaData);
                        overlapIterator2.m_Bounds = Colossal.Mathematics.MathUtils.Bounds(overlapIterator2.m_Quad);
                    }

                    if (overlapIterator2.m_ValidAreaData.m_Area.y > overlapIterator2.m_ValidAreaData.m_Area.x &&
                        blockOverlap7.m_Other                     != Unity.Entities.Entity.Null) {
                        overlapIterator2.Iterate(blockOverlap7.m_Other);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #3 (Primary)", blockOverlap7.m_Block, m_Cells[blockOverlap7.m_Block]);
                        Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap #3 (Other)", blockOverlap7.m_Other, m_Cells[blockOverlap7.m_Other]);
                    }
                }
            }

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public Unity.Collections.NativeArray<Game.Zones.CellCheckHelpers.BlockOverlap> m_BlockOverlaps;

            [Unity.Collections.ReadOnlyAttribute] public Unity.Collections.NativeArray<Game.Zones.CellCheckHelpers.OverlapGroup> m_OverlapGroups;

            [Unity.Collections.ReadOnlyAttribute] public Game.Prefabs.ZonePrefabs m_ZonePrefabs;

            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Game.Zones.Block> m_BlockData;

            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Game.Zones.BuildOrder> m_BuildOrderData;

            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Game.Prefabs.ZoneData> m_ZoneData;

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public Unity.Entities.BufferLookup<Game.Zones.Cell> m_Cells;

            [Unity.Collections.NativeDisableParallelForRestrictionAttribute]
            public Unity.Entities.ComponentLookup<Game.Zones.ValidArea> m_ValidAreaData;

            // --- [CUSTOM] Parcel detection lookups ---
            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Platter.Components.ParcelOwner> m_ParcelOwnerData;
            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Game.Prefabs.PrefabRef>         m_PrefabRefData;
            [Unity.Collections.ReadOnlyAttribute] public Unity.Entities.ComponentLookup<Platter.Components.ParcelData>  m_ParcelData;

            /// <summary>
            /// Modified vanilla CellReduction. [CUSTOM] Added m_IsNarrowParcel - when true, Perform() exits early.
            /// </summary>
            private struct CellReduction {
                /// <summary>
                /// Clears the specified flag from all cells in the valid area.
                /// </summary>
                public void Clear() {
                    m_BlockData     = m_BlockDataFromEntity[m_BlockEntity];
                    m_ValidAreaData = m_ValidAreaDataFromEntity[m_BlockEntity];
                    m_Cells         = m_CellsFromEntity[m_BlockEntity];
                    for (var i = m_ValidAreaData.m_Area.x; i < m_ValidAreaData.m_Area.y; i++)
                    for (var j = m_ValidAreaData.m_Area.z; j < m_ValidAreaData.m_Area.w; j++) {
                        var num  = j * m_BlockData.m_Size.x + i;
                        var cell = m_Cells[num];
                        if ((cell.m_State & m_Flag) != Game.Zones.CellFlags.None) {
                            cell.m_State &= ~m_Flag;
                            m_Cells[num] =  cell;
                        }
                    }
                }

                /// <summary>
                /// Performs the reduction logic, propagating flags and adjusting depth based on neighbors.
                /// </summary>
                public void Perform() {
                    m_BlockData      = m_BlockDataFromEntity[m_BlockEntity];
                    m_ValidAreaData  = m_ValidAreaDataFromEntity[m_BlockEntity];
                    m_BuildOrderData = m_BuildOrderDataFromEntity[m_BlockEntity];
                    m_Cells          = m_CellsFromEntity[m_BlockEntity];

                    // [CUSTOM] Skip depth reduction for narrow parcels
                    if (m_IsNarrowParcel) {
                        // Only update ValidArea if we're in the Blocked phase
                        if (m_Flag == Game.Zones.CellFlags.Blocked) {
                            m_ValidAreaDataFromEntity[m_BlockEntity] = m_ValidAreaData;
                        }

                        return;
                    }

                    if (m_LeftNeightbor != Unity.Entities.Entity.Null) {
                        m_LeftBlockData      = m_BlockDataFromEntity[m_LeftNeightbor];
                        m_LeftValidAreaData  = m_ValidAreaDataFromEntity[m_LeftNeightbor];
                        m_LeftBuildOrderData = m_BuildOrderDataFromEntity[m_LeftNeightbor];
                        m_LeftCells          = m_CellsFromEntity[m_LeftNeightbor];
                    } else {
                        m_LeftBlockData = default;
                    }

                    if (m_RightNeightbor != Unity.Entities.Entity.Null) {
                        m_RightBlockData      = m_BlockDataFromEntity[m_RightNeightbor];
                        m_RightValidAreaData  = m_ValidAreaDataFromEntity[m_RightNeightbor];
                        m_RightBuildOrderData = m_BuildOrderDataFromEntity[m_RightNeightbor];
                        m_RightCells          = m_CellsFromEntity[m_RightNeightbor];
                    } else {
                        m_RightBlockData = default;
                    }

                    var cellFlags = m_Flag | Game.Zones.CellFlags.Blocked;
                    for (var i = m_ValidAreaData.m_Area.x; i < m_ValidAreaData.m_Area.y; i++) {
                        var cell  = m_Cells[i];
                        var cell2 = m_Cells[m_BlockData.m_Size.x + i];
                        if (((cell.m_State & cellFlags) == Game.Zones.CellFlags.None) & ((cell2.m_State & cellFlags) == m_Flag)) {
                            cell.m_State |= m_Flag;
                            m_Cells[i]   =  cell;
                        }

                        for (var j = m_ValidAreaData.m_Area.z + 1; j < m_ValidAreaData.m_Area.w; j++) {
                            var num   = j * m_BlockData.m_Size.x + i;
                            var cell3 = m_Cells[num];
                            if (((cell3.m_State & cellFlags) == Game.Zones.CellFlags.None) &
                                ((cell.m_State  & cellFlags) == m_Flag)) {
                                cell3.m_State |= m_Flag;
                                m_Cells[num]  =  cell3;
                            }

                            cell = cell3;
                        }
                    }

                    var num2      = m_ValidAreaData.m_Area.x;
                    var k         = m_ValidAreaData.m_Area.y - 1;
                    var validArea = default(Game.Zones.ValidArea);
                    validArea.m_Area.xz = m_BlockData.m_Size;
                    while (k >= m_ValidAreaData.m_Area.x) {
                        if (m_Flag == Game.Zones.CellFlags.Occupied) {
                            var cell4    = m_Cells[num2];
                            var cell5    = m_Cells[k];
                            var entity   = m_ZonePrefabs[cell4.m_Zone];
                            var entity2  = m_ZonePrefabs[cell5.m_Zone];
                            var ptr      = m_ZoneData[entity];
                            var zoneData = m_ZoneData[entity2];
                            if ((ptr.m_ZoneFlags & Game.Prefabs.ZoneFlags.SupportNarrow) == (Game.Prefabs.ZoneFlags)0) {
                                var num3 = CalculateLeftDepth(num2, cell4.m_Zone);
                                ReduceDepth(num2, num3);
                            }

                            if ((zoneData.m_ZoneFlags & Game.Prefabs.ZoneFlags.SupportNarrow) == (Game.Prefabs.ZoneFlags)0) {
                                var num4 = CalculateRightDepth(k, cell5.m_Zone);
                                ReduceDepth(k, num4);
                            }
                        } else {
                            var num5 = CalculateLeftDepth(num2, Game.Zones.ZoneType.None);
                            ReduceDepth(num2, num5);
                            var num6 = CalculateRightDepth(k, Game.Zones.ZoneType.None);
                            ReduceDepth(k, num6);
                            if (k <= num2 && m_Flag == Game.Zones.CellFlags.Blocked) {
                                if (num5 != 0 && num2 != k) {
                                    validArea.m_Area.xz = Unity.Mathematics.math.min(validArea.m_Area.xz,
                                                                                     new Unity.Mathematics.int2(num2, m_ValidAreaData.m_Area.z));
                                    validArea.m_Area.yw = Unity.Mathematics.math.max(validArea.m_Area.yw, new Unity.Mathematics.int2(num2 + 1, num5));
                                }

                                if (num6 != 0) {
                                    validArea.m_Area.xz = Unity.Mathematics.math.min(validArea.m_Area.xz,
                                                                                     new Unity.Mathematics.int2(k, m_ValidAreaData.m_Area.z));
                                    validArea.m_Area.yw = Unity.Mathematics.math.max(validArea.m_Area.yw, new Unity.Mathematics.int2(k + 1, num6));
                                }
                            }
                        }

                        num2++;
                        k--;
                    }

                    if (m_Flag == Game.Zones.CellFlags.Blocked) m_ValidAreaDataFromEntity[m_BlockEntity] = validArea;
                }

                private int CalculateLeftDepth(int x, Game.Zones.ZoneType zoneType) {
                    var leftNeighborDepth = GetDepth(x - 1, zoneType);
                    var currentDepth      = GetDepth(x, zoneType);
                    if (currentDepth <= leftNeighborDepth) return currentDepth;
                    var rightNeighborDepth   = GetDepth(x + 1, zoneType);
                    var farLeftNeighborDepth = GetDepth(x - 2, zoneType);
                    if ((leftNeighborDepth != farLeftNeighborDepth) & (leftNeighborDepth != 0)) return leftNeighborDepth;
                    if (rightNeighborDepth - currentDepth < currentDepth - leftNeighborDepth)
                        return Unity.Mathematics.math.min(Unity.Mathematics.math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    if (GetDepth(x + 2, zoneType) != rightNeighborDepth)
                        return Unity.Mathematics.math.min(Unity.Mathematics.math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    return leftNeighborDepth;
                }

                private int CalculateRightDepth(int x, Game.Zones.ZoneType zoneType) {
                    var rightNeighborDepth = GetDepth(x + 1, zoneType);
                    var currentDepth       = GetDepth(x, zoneType);
                    if (currentDepth <= rightNeighborDepth) return currentDepth;
                    var leftNeighborDepth     = GetDepth(x - 1, zoneType);
                    var farRightNeighborDepth = GetDepth(x + 2, zoneType);
                    if ((rightNeighborDepth != farRightNeighborDepth) & (rightNeighborDepth != 0)) return rightNeighborDepth;
                    if (leftNeighborDepth - currentDepth < currentDepth - rightNeighborDepth)
                        return Unity.Mathematics.math.min(Unity.Mathematics.math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    if (GetDepth(x - 2, zoneType) != leftNeighborDepth)
                        return Unity.Mathematics.math.min(Unity.Mathematics.math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    return rightNeighborDepth;
                }

                private int GetDepth(int x, Game.Zones.ZoneType zoneType) {
                    if (x < 0) {
                        x += m_LeftBlockData.m_Size.x;
                        if (x < 0) return 0;
                        if ((m_BuildOrderData.m_Order < m_LeftBuildOrderData.m_Order) & (m_Flag == Game.Zones.CellFlags.Blocked))
                            return GetDepth(m_BlockData,
                                            m_ValidAreaData,
                                            m_Cells,
                                            0,
                                            m_Flag | Game.Zones.CellFlags.Blocked,
                                            zoneType);
                        return GetDepth(m_LeftBlockData,
                                        m_LeftValidAreaData,
                                        m_LeftCells,
                                        x,
                                        m_Flag | Game.Zones.CellFlags.Blocked,
                                        zoneType);
                    } else {
                        if (x < m_BlockData.m_Size.x)
                            return GetDepth(m_BlockData,
                                            m_ValidAreaData,
                                            m_Cells,
                                            x,
                                            m_Flag | Game.Zones.CellFlags.Blocked,
                                            zoneType);
                        x -= m_BlockData.m_Size.x;
                        if (x >= m_RightBlockData.m_Size.x) return 0;
                        if ((m_BuildOrderData.m_Order < m_RightBuildOrderData.m_Order) & (m_Flag == Game.Zones.CellFlags.Blocked))
                            return GetDepth(m_BlockData,
                                            m_ValidAreaData,
                                            m_Cells,
                                            m_BlockData.m_Size.x - 1,
                                            m_Flag | Game.Zones.CellFlags.Blocked,
                                            zoneType);
                        return GetDepth(m_RightBlockData,
                                        m_RightValidAreaData,
                                        m_RightCells,
                                        x,
                                        m_Flag | Game.Zones.CellFlags.Blocked,
                                        zoneType);
                    }
                }

                private int GetDepth(Game.Zones.Block                              blockData,
                                     Game.Zones.ValidArea                          validAreaData,
                                     Unity.Entities.DynamicBuffer<Game.Zones.Cell> cells,
                                     int                                           x,
                                     Game.Zones.CellFlags                          flags,
                                     Game.Zones.ZoneType                           zoneType) {
                    var num  = validAreaData.m_Area.z;
                    var num2 = x;
                    if (m_Flag == Game.Zones.CellFlags.Occupied)
                        while (num < validAreaData.m_Area.w && (cells[num2].m_State & flags) == Game.Zones.CellFlags.None) {
                            if (!cells[num2].m_Zone.Equals(zoneType)) break;
                            num2 += blockData.m_Size.x;
                            num++;
                        }
                    else
                        while (num < validAreaData.m_Area.w && (cells[num2].m_State & flags) == Game.Zones.CellFlags.None) {
                            num2 += blockData.m_Size.x;
                            num++;
                        }

                    return num;
                }

                private void ReduceDepth(int x, int newDepth) {
                    var cellFlags = m_Flag | Game.Zones.CellFlags.Blocked;
                    var num       = m_BlockData.m_Size.x * newDepth + x;
                    for (var i = newDepth; i < m_ValidAreaData.m_Area.w; i++) {
                        var cell = m_Cells[num];
                        if ((cell.m_State & cellFlags) != Game.Zones.CellFlags.None) return;
                        cell.m_State |= m_Flag;
                        m_Cells[num] =  cell;
                        num          += m_BlockData.m_Size.x;
                    }
                }

                public Unity.Entities.Entity m_BlockEntity;

                public Unity.Entities.Entity m_LeftNeightbor;

                public Unity.Entities.Entity m_RightNeightbor;

                public Game.Zones.CellFlags m_Flag;

                /// <summary>[CUSTOM] When true, skip depth reduction to protect narrow parcels.</summary>
                public bool m_IsNarrowParcel;

                public Game.Prefabs.ZonePrefabs m_ZonePrefabs;

                public Unity.Entities.ComponentLookup<Game.Zones.Block> m_BlockDataFromEntity;

                public Unity.Entities.ComponentLookup<Game.Zones.ValidArea> m_ValidAreaDataFromEntity;

                public Unity.Entities.ComponentLookup<Game.Zones.BuildOrder> m_BuildOrderDataFromEntity;

                public Unity.Entities.ComponentLookup<Game.Prefabs.ZoneData> m_ZoneData;

                public Unity.Entities.BufferLookup<Game.Zones.Cell> m_CellsFromEntity;

                private Game.Zones.Block m_BlockData;

                private Game.Zones.Block m_LeftBlockData;

                private Game.Zones.Block m_RightBlockData;

                private Game.Zones.ValidArea m_ValidAreaData;

                private Game.Zones.ValidArea m_LeftValidAreaData;

                private Game.Zones.ValidArea m_RightValidAreaData;

                private Game.Zones.BuildOrder m_BuildOrderData;

                private Game.Zones.BuildOrder m_LeftBuildOrderData;

                private Game.Zones.BuildOrder m_RightBuildOrderData;

                private Unity.Entities.DynamicBuffer<Game.Zones.Cell> m_Cells;

                private Unity.Entities.DynamicBuffer<Game.Zones.Cell> m_LeftCells;

                private Unity.Entities.DynamicBuffer<Game.Zones.Cell> m_RightCells;
            }

            /// <summary>
            /// Modified vanilla OverlapIterator. [CUSTOM] Added parcel detection and zone clearing in CheckOverlapZ2().
            /// </summary>
            private struct OverlapIterator {
                /// <summary>
                /// Iterates over the overlap between the current block and another block.
                /// </summary>
                /// <param name="blockEntity2">The entity of the other block.</param>
                public void Iterate(Unity.Entities.Entity blockEntity2) {
                    m_BlockData2      = m_BlockDataFromEntity[blockEntity2];
                    m_ValidAreaData2  = m_ValidAreaDataFromEntity[blockEntity2];
                    m_BuildOrderData2 = m_BuildOrderDataFromEntity[blockEntity2];
                    m_Cells2          = m_CellsFromEntity[blockEntity2];

                    Platter.Utils.BurstLogger.Debug("[CBOJ]", "Start Overlap:");
                    Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap (Primary)", m_BlockEntity, m_CellsFromEntity[m_BlockEntity]);
                    Platter.Utils.DebugUtils.DebugBlockStatus("[CBOJ] Overlap (Other)", blockEntity2, m_CellsFromEntity[blockEntity2]);

                    if (m_ValidAreaData2.m_Area.y <= m_ValidAreaData2.m_Area.x) return;

                    // [CUSTOM] Check if the other block is a parcel
                    m_OtherBlockIsParcel = m_ParcelOwnerData.HasComponent(blockEntity2);

                    if (Game.Zones.ZoneUtils.CanShareCells(m_BlockData, m_BlockData2, m_BuildOrderData, m_BuildOrderData2)) {
                        if (!m_CheckSharing) return;
                        m_CheckDepth = false;
                    } else {
                        if (m_CheckSharing) return;
                        m_CheckDepth = Unity.Mathematics.math.dot(m_BlockData.m_Direction, m_BlockData2.m_Direction) < -0.6946584f;
                    }

                    var quad = Game.Zones.ZoneUtils.CalculateCorners(m_BlockData2, m_ValidAreaData2);
                    CheckOverlapX1(m_Bounds,
                                   Colossal.Mathematics.MathUtils.Bounds(quad),
                                   m_Quad,
                                   quad,
                                   m_ValidAreaData.m_Area,
                                   m_ValidAreaData2.m_Area);
                }

                /// <summary>
                /// Recursively checks for overlaps along the X-axis (first pass).
                /// </summary>
                private void CheckOverlapX1(Colossal.Mathematics.Bounds2 bounds1,
                                            Colossal.Mathematics.Bounds2 bounds2,
                                            Colossal.Mathematics.Quad2   quad1,
                                            Colossal.Mathematics.Quad2   quad2,
                                            Unity.Mathematics.int4       xxzz1,
                                            Unity.Mathematics.int4       xxzz2) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = (xxzz1.x + xxzz1.y) >> 1;
                        int2.x = @int.y;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num   = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad3.b = Unity.Mathematics.math.lerp(quad1.a, quad1.b, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad1.d, quad1.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2)) CheckOverlapZ1(bounds3, bounds2, quad3, quad2, @int, xxzz2);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2)) {
                            CheckOverlapZ1(bounds4, bounds2, quad4, quad2, int2, xxzz2);
                            return;
                        }
                    } else {
                        CheckOverlapZ1(bounds1, bounds2, quad1, quad2, xxzz1, xxzz2);
                    }
                }

                /// <summary>
                /// Recursively checks for overlaps along the Z-axis (first pass).
                /// </summary>
                private void CheckOverlapZ1(Colossal.Mathematics.Bounds2 bounds1,
                                            Colossal.Mathematics.Bounds2 bounds2,
                                            Colossal.Mathematics.Quad2   quad1,
                                            Colossal.Mathematics.Quad2   quad2,
                                            Unity.Mathematics.int4       xxzz1,
                                            Unity.Mathematics.int4       xxzz2) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = (xxzz1.z + xxzz1.w) >> 1;
                        int2.z = @int.w;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num   = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad3.d = Unity.Mathematics.math.lerp(quad1.a, quad1.d, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad1.b, quad1.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds3, bounds2)) CheckOverlapX2(bounds3, bounds2, quad3, quad2, @int, xxzz2);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds4, bounds2)) {
                            CheckOverlapX2(bounds4, bounds2, quad4, quad2, int2, xxzz2);
                            return;
                        }
                    } else {
                        CheckOverlapX2(bounds1, bounds2, quad1, quad2, xxzz1, xxzz2);
                    }
                }

                /// <summary>
                /// Recursively checks for overlaps along the X-axis (second pass).
                /// </summary>
                private void CheckOverlapX2(Colossal.Mathematics.Bounds2 bounds1,
                                            Colossal.Mathematics.Bounds2 bounds2,
                                            Colossal.Mathematics.Quad2   quad1,
                                            Colossal.Mathematics.Quad2   quad2,
                                            Unity.Mathematics.int4       xxzz1,
                                            Unity.Mathematics.int4       xxzz2) {
                    if (xxzz2.y - xxzz2.x >= 2) {
                        var @int = xxzz2;
                        var int2 = xxzz2;
                        @int.y = (xxzz2.x + xxzz2.y) >> 1;
                        int2.x = @int.y;
                        var quad3 = quad2;
                        var quad4 = quad2;
                        var num   = (float)(@int.y - xxzz2.x) / (float)(xxzz2.y - xxzz2.x);
                        quad3.b = Unity.Mathematics.math.lerp(quad2.a, quad2.b, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad2.d, quad2.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds1, bounds3)) CheckOverlapZ2(bounds1, bounds3, quad1, quad3, xxzz1, @int);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds1, bounds4)) {
                            CheckOverlapZ2(bounds1, bounds4, quad1, quad4, xxzz1, int2);
                            return;
                        }
                    } else {
                        CheckOverlapZ2(bounds1, bounds2, quad1, quad2, xxzz1, xxzz2);
                    }
                }

                /// <summary>
                /// Recursively checks for overlaps along the Z-axis (second pass) and performs the final cell-level overlap logic.
                /// </summary>
                private void CheckOverlapZ2(Colossal.Mathematics.Bounds2 bounds1,
                                            Colossal.Mathematics.Bounds2 bounds2,
                                            Colossal.Mathematics.Quad2   quad1,
                                            Colossal.Mathematics.Quad2   quad2,
                                            Unity.Mathematics.int4       xxzz1,
                                            Unity.Mathematics.int4       xxzz2) {
                    if (xxzz2.w - xxzz2.z >= 2) {
                        var @int = xxzz2;
                        var int2 = xxzz2;
                        @int.w = (xxzz2.z + xxzz2.w) >> 1;
                        int2.z = @int.w;
                        var quad3 = quad2;
                        var quad4 = quad2;
                        var num   = (float)(@int.w - xxzz2.z) / (float)(xxzz2.w - xxzz2.z);
                        quad3.d = Unity.Mathematics.math.lerp(quad2.a, quad2.d, num);
                        quad3.c = Unity.Mathematics.math.lerp(quad2.b, quad2.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = Colossal.Mathematics.MathUtils.Bounds(quad3);
                        var bounds4 = Colossal.Mathematics.MathUtils.Bounds(quad4);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds1, bounds3)) CheckOverlapX1(bounds1, bounds3, quad1, quad3, xxzz1, @int);
                        if (Colossal.Mathematics.MathUtils.Intersect(bounds1, bounds4)) {
                            CheckOverlapX1(bounds1, bounds4, quad1, quad4, xxzz1, int2);
                            return;
                        }
                    } else {
                        if (Unity.Mathematics.math.any(xxzz1.yw - xxzz1.xz >= 2) | Unity.Mathematics.math.any(xxzz2.yw - xxzz2.xz >= 2)) {
                            CheckOverlapX1(bounds1, bounds2, quad1, quad2, xxzz1, xxzz2);
                            return;
                        }

                        var num2  = xxzz1.z * m_BlockData.m_Size.x  + xxzz1.x;
                        var num3  = xxzz2.z * m_BlockData2.m_Size.x + xxzz2.x;
                        var cell  = m_Cells[num2];
                        var cell2 = m_Cells2[num3];
                        if (((cell.m_State | cell2.m_State) & Game.Zones.CellFlags.Blocked) != Game.Zones.CellFlags.None) return;

                        // [CUSTOM] When both blocks are parcels, don't modify either - they shouldn't affect each other
                        if (m_CurBlockIsParcel && m_OtherBlockIsParcel) {
                            return;
                        }

                        if (m_CheckSharing) {
                            if (Unity.Mathematics.math.lengthsq(Colossal.Mathematics.MathUtils.Center(quad1) -
                                                                Colossal.Mathematics.MathUtils.Center(quad2)) < 16f) {
                                // [CUSTOM] When one block is a parcel, clear zone from the non-parcel block
                                if (m_CurBlockIsParcel) {
                                    cell2.m_Zone   = Game.Zones.ZoneType.None;
                                    m_Cells2[num3] = cell2;
                                    return;
                                }

                                if (m_OtherBlockIsParcel) {
                                    cell.m_Zone   = Game.Zones.ZoneType.None;
                                    m_Cells[num2] = cell;
                                    return;
                                }
                                // [END CUSTOM]

                                if (CheckPriority(cell,
                                                  cell2,
                                                  xxzz1.z,
                                                  xxzz2.z,
                                                  m_BuildOrderData.m_Order,
                                                  m_BuildOrderData2.m_Order) &&
                                    (cell2.m_State & Game.Zones.CellFlags.Shared) == Game.Zones.CellFlags.None) {
                                    cell.m_State |= Game.Zones.CellFlags.Shared;
                                    cell.m_State = (cell.m_State  & ~Game.Zones.CellFlags.Overridden) |
                                                   (cell2.m_State & Game.Zones.CellFlags.Overridden);
                                    cell.m_Zone = cell2.m_Zone;
                                }

                                if ((cell2.m_State & Game.Zones.CellFlags.Roadside) != Game.Zones.CellFlags.None && xxzz2.z == 0)
                                    cell.m_State |= Game.Zones.ZoneUtils.GetRoadDirection(m_BlockData, m_BlockData2);
                                cell.m_State  &= ~Game.Zones.CellFlags.Occupied | (cell2.m_State & Game.Zones.CellFlags.Occupied);
                                m_Cells[num2] =  cell;
                                return;
                            }
                        } else if (CheckPriority(cell,
                                                 cell2,
                                                 xxzz1.z,
                                                 xxzz2.z,
                                                 m_BuildOrderData.m_Order,
                                                 m_BuildOrderData2.m_Order)) {
                            // [CUSTOM] Don't block current parcel's cells due to overlap with another block
                            if (m_CurBlockIsParcel) {
                                return;
                            }
                            // [END CUSTOM]

                            quad1 = Colossal.Mathematics.MathUtils.Expand(quad1, -0.01f);
                            quad2 = Colossal.Mathematics.MathUtils.Expand(quad2, -0.01f);
                            if (Colossal.Mathematics.MathUtils.Intersect(quad1, quad2)) {
                                cell.m_State = (cell.m_State & ~Game.Zones.CellFlags.Shared) |
                                               (m_CheckBlocking ? Game.Zones.CellFlags.Blocked : Game.Zones.CellFlags.Redundant);
                                m_Cells[num2] = cell;
                                return;
                            }
                        } else if (Unity.Mathematics.math.lengthsq(Colossal.Mathematics.MathUtils.Center(quad1) -
                                                                   Colossal.Mathematics.MathUtils.Center(quad2)) < 64f &&
                                   (cell2.m_State & Game.Zones.CellFlags.Roadside) != Game.Zones.CellFlags.None        && xxzz2.z == 0) {
                            cell.m_State  |= Game.Zones.ZoneUtils.GetRoadDirection(m_BlockData, m_BlockData2);
                            m_Cells[num2] =  cell;
                        }
                    }
                }

                /// <summary>
                /// Determines which cell has priority in an overlap scenario.
                /// </summary>
                private bool CheckPriority(Game.Zones.Cell cell1, Game.Zones.Cell cell2, int depth1, int depth2, uint order1, uint order2) {
                    if ((cell2.m_State & Game.Zones.CellFlags.Updating) == Game.Zones.CellFlags.None)
                        return (cell2.m_State & Game.Zones.CellFlags.Visible) > Game.Zones.CellFlags.None;
                    if (m_CheckBlocking) return (cell1.m_State & ~cell2.m_State & Game.Zones.CellFlags.Redundant) > Game.Zones.CellFlags.None;
                    if (m_CheckDepth) {
                        if (cell1.m_Zone.Equals(Game.Zones.ZoneType.None) != cell2.m_Zone.Equals(Game.Zones.ZoneType.None))
                            return cell1.m_Zone.Equals(Game.Zones.ZoneType.None);
                        if (cell1.m_Zone.Equals(Game.Zones.ZoneType.None)                                                    &&
                            ((cell1.m_State | cell2.m_State) & Game.Zones.CellFlags.Overridden) == Game.Zones.CellFlags.None &&
                            Unity.Mathematics.math.max(0, depth1 - 1)                           != Unity.Mathematics.math.max(0, depth2 - 1))
                            return depth2 < depth1;
                    }

                    if (((cell1.m_State ^ cell2.m_State) & Game.Zones.CellFlags.Visible) != Game.Zones.CellFlags.None)
                        return (cell2.m_State & Game.Zones.CellFlags.Visible) > Game.Zones.CellFlags.None;
                    return order2 < order1;
                }

                public Unity.Entities.Entity m_BlockEntity;

                public Colossal.Mathematics.Quad2 m_Quad;

                public Colossal.Mathematics.Bounds2 m_Bounds;

                public Game.Zones.Block m_BlockData;

                public Game.Zones.ValidArea m_ValidAreaData;

                public Game.Zones.BuildOrder m_BuildOrderData;

                public Unity.Entities.DynamicBuffer<Game.Zones.Cell> m_Cells;

                public Unity.Entities.ComponentLookup<Game.Zones.Block> m_BlockDataFromEntity;

                public Unity.Entities.ComponentLookup<Game.Zones.ValidArea> m_ValidAreaDataFromEntity;

                public Unity.Entities.ComponentLookup<Game.Zones.BuildOrder> m_BuildOrderDataFromEntity;

                public Unity.Entities.BufferLookup<Game.Zones.Cell> m_CellsFromEntity;

                public bool m_CheckSharing;

                public bool m_CheckBlocking;

                public bool m_CheckDepth;

                private Game.Zones.Block m_BlockData2;

                private Game.Zones.ValidArea m_ValidAreaData2;

                private Game.Zones.BuildOrder m_BuildOrderData2;

                private Unity.Entities.DynamicBuffer<Game.Zones.Cell> m_Cells2;

                // --- [CUSTOM] Parcel detection fields ---
                public  Unity.Entities.ComponentLookup<Platter.Components.ParcelOwner> m_ParcelOwnerData;
                public  bool                                                           m_CurBlockIsParcel;
                private bool                                                           m_OtherBlockIsParcel;
            }
        }
    }
}