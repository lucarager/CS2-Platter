// <copyright file="P_CellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Game.Common;
using Game.Tools;
using Unity.Burst;
using Unity.Entities.Internal;

namespace Platter.Systems {
    using System;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Zones;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;
    using static Game.Zones.CellCheckHelpers;
    using Block = Game.Zones.Block;

    /// <summary>
    /// Cell Check System. Similar to vanilla's CellCheckSystem, checks blocks and clears vanilla zoning under parcels
    /// This prevents vanilla lots from spawning buildings under our custom parcels.
    /// </summary>
    public partial class P_CellCheckSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Systems
        private Game.Zones.UpdateCollectSystem   m_ZoneUpdateCollectSystem;
        private Game.Objects.UpdateCollectSystem m_ObjectUpdateCollectSystem;
        private Game.Net.UpdateCollectSystem     m_NetUpdateCollectSystem;
        private Game.Areas.UpdateCollectSystem   m_AreaUpdateCollectSystem;
        private Game.Zones.SearchSystem          m_ZoneSearchSystem;
        private Game.Objects.SearchSystem        m_ObjectSearchSystem;
        private Game.Net.SearchSystem            m_NetSearchSystem;
        private Game.Areas.SearchSystem          m_AreaSearchSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            m_ObjectUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            m_NetUpdateCollectSystem    = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            m_AreaUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Areas.UpdateCollectSystem>();
            m_ZoneSearchSystem          = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_ObjectSearchSystem        = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_NetSearchSystem           = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_AreaSearchSystem          = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_CellCheckSystem));
            m_Log.Debug("OnCreate()");
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (
                !m_ZoneUpdateCollectSystem.isUpdated   &&
                !m_ObjectUpdateCollectSystem.isUpdated &&
                !m_NetUpdateCollectSystem.netsUpdated  &&
                !m_AreaUpdateCollectSystem.lotsUpdated &&
                !m_AreaUpdateCollectSystem.mapTilesUpdated) {
                return;
            }

            var updateBlocksList  = new NativeList<SortedEntity>(Allocator.TempJob);
            var blockOverlapQueue = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            var blockOverlapList  = new NativeList<BlockOverlap>(Allocator.TempJob);
            var overlapGroupsList = new NativeList<OverlapGroup>(Allocator.TempJob);
            var sortedEntityArray = updateBlocksList.AsDeferredJobArray();

            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updateBlocksList));

            var findOverlappingBlocksJobHandle = new FindOverlappingBlocksJob {
                m_Blocks         = sortedEntityArray,
                m_SearchTree     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle),
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(updateBlocksList, 1, JobHandle.CombineDependencies(Dependency, zoneSearchJobHandle));

            m_ZoneSearchSystem.AddSearchTreeReader(findOverlappingBlocksJobHandle);

            var groupOverlappingBlocksJobHandle = new GroupOverlappingBlocksJob {
                m_Blocks        = sortedEntityArray,
                m_OverlapQueue  = blockOverlapQueue,
                m_BlockOverlaps = blockOverlapList,
                m_OverlapGroups = overlapGroupsList,
            }.Schedule(findOverlappingBlocksJobHandle);

            var clearBlocksJobHandle = new ClearBlocksJob(
                blockOverlapArray: blockOverlapList.AsDeferredJobArray(),
                overlapGroups: overlapGroupsList.AsDeferredJobArray(),
                blockCLook: SystemAPI.GetComponentLookup<Block>(),
                buildOrderCLook: SystemAPI.GetComponentLookup<BuildOrder>(),
                parcelOwnerCLook: SystemAPI.GetComponentLookup<ParcelOwner>(),
                cellsBLook: SystemAPI.GetBufferLookup<Cell>(),
                validAreaCLook: SystemAPI.GetComponentLookup<ValidArea>()
            ).Schedule(overlapGroupsList, 1, groupOverlappingBlocksJobHandle);

            updateBlocksList.Dispose(groupOverlappingBlocksJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(clearBlocksJobHandle);
            overlapGroupsList.Dispose(clearBlocksJobHandle);

            Dependency = clearBlocksJobHandle;
        }

        private JobHandle CollectUpdatedBlocks(NativeList<SortedEntity> updateBlocksList) {
            var zoneUpdateQueue   = new NativeQueue<Entity>(Allocator.TempJob);
            var objectUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var nativeQueue3      = new NativeQueue<Entity>(Allocator.TempJob);
            var nativeQueue4      = new NativeQueue<Entity>(Allocator.TempJob);

            var zoneSearchTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var jobHandle      = default(JobHandle);

            if (m_ZoneUpdateCollectSystem.isUpdated) {
                var updatedBounds = m_ZoneUpdateCollectSystem.GetUpdatedBounds(true, out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Zones = new FindUpdatedBlocksSingleIterationJob {
                    m_Bounds      = updatedBounds.AsDeferredJobArray(),
                    m_SearchTree  = zoneSearchTree,
                    m_ResultQueue = zoneUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_ZoneUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Zones);
                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Zones);
            }

            if (m_ObjectUpdateCollectSystem.isUpdated) {
                var updatedBounds = m_ObjectUpdateCollectSystem.GetUpdatedBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Objects = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds      = updatedBounds.AsDeferredJobArray(),
                    m_SearchTree  = zoneSearchTree,
                    m_ResultQueue = objectUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_ObjectUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Objects);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Objects);
            }

            if (m_NetUpdateCollectSystem.netsUpdated) {
                var updatedNetBounds = m_NetUpdateCollectSystem.GetUpdatedNetBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Nets = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds      = updatedNetBounds.AsDeferredJobArray(),
                    m_SearchTree  = zoneSearchTree,
                    m_ResultQueue = nativeQueue3.AsParallelWriter(),
                }.Schedule(updatedNetBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_NetUpdateCollectSystem.AddNetBoundsReader(findUpdatedBlocksJob_Nets);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Nets);
            }

            var searchJobHandle = zoneSearchJobHandle;
            if (m_AreaUpdateCollectSystem.lotsUpdated) {
                var updatedLotBounds = m_AreaUpdateCollectSystem.GetUpdatedLotBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Areas = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds      = updatedLotBounds.AsDeferredJobArray(),
                    m_SearchTree  = zoneSearchTree,
                    m_ResultQueue = nativeQueue4.AsParallelWriter(),
                }.Schedule(updatedLotBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_AreaUpdateCollectSystem.AddLotBoundsReader(findUpdatedBlocksJob_Areas);

                jobHandle       = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Areas);
                searchJobHandle = findUpdatedBlocksJob_Areas;
            }

            if (m_AreaUpdateCollectSystem.mapTilesUpdated) {
                var updatedMapTileBounds = m_AreaUpdateCollectSystem.GetUpdatedMapTileBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_MapTiles = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds      = updatedMapTileBounds.AsDeferredJobArray(),
                    m_SearchTree  = zoneSearchTree,
                    m_ResultQueue = nativeQueue4.AsParallelWriter(),
                }.Schedule(updatedMapTileBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, searchJobHandle));
                m_AreaUpdateCollectSystem.AddMapTileBoundsReader(findUpdatedBlocksJob_MapTiles);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_MapTiles);
            }

            var collectBlocksJobHandle = new CollectBlocksJob {
                m_Queue1     = zoneUpdateQueue,
                m_Queue2     = objectUpdateQueue,
                m_Queue3     = nativeQueue3,
                m_Queue4     = nativeQueue4,
                m_ResultList = updateBlocksList,
            }.Schedule(jobHandle);

            zoneUpdateQueue.Dispose(collectBlocksJobHandle);
            objectUpdateQueue.Dispose(collectBlocksJobHandle);
            nativeQueue3.Dispose(collectBlocksJobHandle);
            nativeQueue4.Dispose(collectBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(jobHandle);

            return collectBlocksJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        public struct ClearBlocksJob : IJobParallelForDefer {
            [ReadOnly]                            private NativeArray<OverlapGroup>    m_OverlapGroups;
            [ReadOnly]                            private ComponentLookup<ParcelOwner> m_ParcelOwnerCLook;
            [ReadOnly]                            private ComponentLookup<Block>       m_BlockCLook;
            [ReadOnly]                            private ComponentLookup<BuildOrder>  m_BuildOrderCLook;
            [NativeDisableParallelForRestriction] private ComponentLookup<ValidArea>   m_ValidAreaCLook;
            [NativeDisableParallelForRestriction] private NativeArray<BlockOverlap>    m_BlockOverlapArray;
            [NativeDisableParallelForRestriction] private BufferLookup<Cell>           m_CellBLook;

            public ClearBlocksJob(NativeArray<OverlapGroup>  overlapGroups,     ComponentLookup<ParcelOwner> parcelOwnerCLook,
                                  ComponentLookup<Block>     blockCLook,        ComponentLookup<BuildOrder>  buildOrderCLook,
                                  NativeArray<BlockOverlap>  blockOverlapArray, BufferLookup<Cell>           cellsBLook,
                                  ComponentLookup<ValidArea> validAreaCLook) {
                m_OverlapGroups     = overlapGroups;
                m_ParcelOwnerCLook  = parcelOwnerCLook;
                m_BlockCLook        = blockCLook;
                m_BuildOrderCLook   = buildOrderCLook;
                m_BlockOverlapArray = blockOverlapArray;
                m_CellBLook         = cellsBLook;
                m_ValidAreaCLook    = validAreaCLook;
            }

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];

                var overlapIterator = new OverlapIterator(
                    blockCLook: m_BlockCLook,
                    validAreaCLook: m_ValidAreaCLook,
                    buildOrderCLook: m_BuildOrderCLook,
                    cellsBLook: m_CellBLook,
                    parcelOwnerCLook: m_ParcelOwnerCLook
                );

                for (var n = overlapGroup.m_StartIndex; n < overlapGroup.m_EndIndex; n++) {
                    var blockOverlap   = m_BlockOverlapArray[n];
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

                private ComponentLookup<ParcelOwner> m_ParcelOwnerCLook;
                private ComponentLookup<Block>       m_BlockCLook;
                private ComponentLookup<ValidArea>   m_ValidAreaCLook;
                private ComponentLookup<BuildOrder>  m_BuildOrderCLook;
                private BufferLookup<Cell>           m_CellBLook;

                private Entity              m_CurBlockEntity;
                private ValidArea           m_CurValidArea;
                private Bounds2             m_CurBounds;
                private Block               m_CurBlock;
                private BuildOrder          m_CurBuildOrder;
                private DynamicBuffer<Cell> m_CurCellBuffer;
                private Quad2               m_CurCorners;
                private bool                m_CurBlockIsParcel;
                private Entity              m_OtherBlockEntity;
                private Block               m_OtherBlock;
                private ValidArea           m_OtherValidArea;
                private BuildOrder          m_OtherBuildOrder;
                private DynamicBuffer<Cell> m_OtherCells;
                private bool                m_OtherBlockIsParcel;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerCLook, ComponentLookup<Block>      blockCLook,
                                       ComponentLookup<ValidArea>   validAreaCLook,   ComponentLookup<BuildOrder> buildOrderCLook,
                                       BufferLookup<Cell>           cellsBLook) : this() {
                    m_ParcelOwnerCLook = parcelOwnerCLook;
                    m_BlockCLook       = blockCLook;
                    m_ValidAreaCLook   = validAreaCLook;
                    m_BuildOrderCLook  = buildOrderCLook;
                    m_CellBLook        = cellsBLook;
                }

                public void SetEntity(Entity curBlockEntity) {
                    var curValidArea = m_ValidAreaCLook[curBlockEntity];
                    var curBlock     = m_BlockCLook[curBlockEntity];
                    var curCorners   = ZoneUtils.CalculateCorners(curBlock, curValidArea);

                    // Set data
                    m_CurBlockEntity   = curBlockEntity;
                    m_CurValidArea     = curValidArea;
                    m_CurCorners       = curCorners;
                    m_CurBounds        = MathUtils.Bounds(curCorners);
                    m_CurBlock         = curBlock;
                    m_CurBuildOrder    = m_BuildOrderCLook[curBlockEntity];
                    m_CurCellBuffer    = m_CellBLook[curBlockEntity];
                    m_CurBlockIsParcel = m_ParcelOwnerCLook.HasComponent(curBlockEntity);
                }

                public void Iterate(Entity otherBlock) {
                    m_OtherBlockEntity   = otherBlock;
                    m_OtherBlock         = m_BlockCLook[otherBlock];
                    m_OtherValidArea     = m_ValidAreaCLook[otherBlock];
                    m_OtherBuildOrder    = m_BuildOrderCLook[otherBlock];
                    m_OtherCells         = m_CellBLook[otherBlock];
                    m_OtherBlockIsParcel = m_ParcelOwnerCLook.HasComponent(otherBlock);

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
                        m_CurBounds, MathUtils.Bounds(otherBlockCorners), m_CurCorners, otherBlockCorners, m_CurValidArea.m_Area,
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
                                blockBounds, otherLeftBounds, blockCorners, otherLeftCorners, validArea, otherLeftArea);
                        }

                        if (MathUtils.Intersect(blockBounds, otherRightBounds)) {
                            CheckOverlapZ2(
                                blockBounds, otherRightBounds, blockCorners, otherRightCorners, validArea, otherRightArea);
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

                        var otherTopCorners = otherCorners;
                        var otherBottomCorners = otherCorners;
                        var t = (otherTopArea.w - otherValidArea.z) / (float)(otherValidArea.w - otherValidArea.z);

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
                                blockBounds, bottomBounds, blockCorners, otherBottomCorners, validArea, otherBottomArea);
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
                    var otherCell = m_OtherCells[otherIndex];

                    // If either is blocked already, nothing to do.
                    if (((curCell.m_State | otherCell.m_State) & CellFlags.Blocked) != CellFlags.None) {
                        PlatterMod.Instance.Log.Debug(
                            $"[CheckBlockOverlapJob] Blocked - {curCell} {curIndex} ({m_CurBlock.m_Size}) / {otherCell} {otherIndex} ({m_OtherBlock.m_Size})");
                        return;
                    }

                    // In sharing mode we only allow sharing when cell centers are very close.
                    if (!(math.lengthsq(MathUtils.Center(blockCorners) - MathUtils.Center(otherCorners)) < 16f)) {
                        PlatterMod.Instance.Log.Debug(
                            $"[CheckBlockOverlapJob] Not close - {curCell} {curIndex} ({blockCorners}) / {otherCell} {otherIndex} ({otherCorners})");
                        return;
                    }

                    if (m_CurBlockIsParcel) {
                        PlatterMod.Instance.Log.Debug(
                            $"[CheckBlockOverlapJob] Clearing {otherCell} {otherIndex} ({m_OtherBlock.m_Size} {m_OtherBlockEntity})");
                        otherCell.m_Zone         =  ZoneType.None;
                        otherCell.m_State        &= ~CellFlags.Shared;
                        m_OtherCells[otherIndex] =  otherCell;
                    } else if (m_OtherBlockIsParcel) {
                        PlatterMod.Instance.Log.Debug(
                            $"[CheckBlockOverlapJob] Clearing {curCell} {curIndex} ({m_CurBlock.m_Size} {m_CurBlockEntity})");
                        curCell.m_Zone            =  ZoneType.None;
                        curCell.m_State           &= ~CellFlags.Shared;
                        m_CurCellBuffer[curIndex] =  curCell;
                    }
                }
            }
        }
    }
}