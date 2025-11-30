// <copyright file="P_CellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using static Game.Zones.CellCheckHelpers;
    using Block = Game.Zones.Block;
    using BlockOverlap = Game.Zones.CellCheckHelpers.BlockOverlap;
    using BuildOrder = Game.Zones.BuildOrder;
    using OverlapGroup = Game.Zones.CellCheckHelpers.OverlapGroup;
    using SearchSystem = Game.Areas.SearchSystem;
    using UpdateCollectSystem = Game.Areas.UpdateCollectSystem;

    #endregion

    /// <summary>
    /// Cell Check System. Similar to vanilla's CellCheckSystem.
    /// Runs after CellCheckSystem and undoes some vanilla behavior to blocks where our custom parcels are present.
    ///
    /// - Undoes the blocking of size 1 blocks (This allows small parcels to function correctly.)
    /// - Checks blocks and clears vanilla zoning underneath parcels (This prevents vanilla lots from spawning buildings under our custom parcels.)
    ///
    /// Some part of this code remains unchanged from the original systems and might therefore be harder to parse.
    /// </summary>
    public partial class P_CellCheckSystem : GameSystemBase {
        private PrefixedLogger                   m_Log;
        private SearchSystem                     m_AreaSearchSystem;
        private Game.Net.SearchSystem            m_NetSearchSystem;
        private Game.Zones.SearchSystem          m_ZoneSearchSystem;
        private UpdateCollectSystem              m_AreaUpdateCollectSystem;
        private Game.Net.UpdateCollectSystem     m_NetUpdateCollectSystem;
        private Game.Objects.UpdateCollectSystem m_ObjectUpdateCollectSystem;
        private Game.Zones.UpdateCollectSystem   m_ZoneUpdateCollectSystem;
        private ZoneSystem                       m_ZoneSystem;
        private ModificationBarrier5             m_ModificationBarrier5;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            m_ObjectUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            m_NetUpdateCollectSystem    = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            m_AreaUpdateCollectSystem   = World.GetOrCreateSystemManaged<UpdateCollectSystem>();
            m_ZoneSearchSystem          = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_NetSearchSystem           = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_AreaSearchSystem          = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ZoneSystem                = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_ModificationBarrier5      = World.GetOrCreateSystemManaged<ModificationBarrier5>();

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

            var updatedBlocksList  = new NativeList<SortedEntity>(Allocator.TempJob);
            var blockOverlapQueue  = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            var blockOverlapList   = new NativeList<BlockOverlap>(Allocator.TempJob);
            var overlapGroupsList  = new NativeList<OverlapGroup>(Allocator.TempJob);
            var updatedBlocksArray = updatedBlocksList.AsDeferredJobArray();
            var zoneSearchTree     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var boundsQueue        = new NativeQueue<Bounds2>(Allocator.TempJob);

            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updatedBlocksList));

            var undoBlockedCellsJobHandle = new SanitizeCellsJob {
                m_Blocks            = updatedBlocksArray,
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_ParcelDataLookup  = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabRefLookup   = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_ValidAreaLookup   = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(updatedBlocksList, 1, Dependency);

            var findOverlappingBlocksJobHandle = new CellCheckHelpers.FindOverlappingBlocksJob {
                m_Blocks         = updatedBlocksArray,
                m_SearchTree     = zoneSearchTree,
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(updatedBlocksList, 1, JobHandle.CombineDependencies(undoBlockedCellsJobHandle, zoneSearchJobHandle));
            m_ZoneSearchSystem.AddSearchTreeReader(findOverlappingBlocksJobHandle);

            var groupOverlappingBlocksJobHandle = new CellCheckHelpers.GroupOverlappingBlocksJob {
                m_Blocks        = updatedBlocksArray,
                m_OverlapQueue  = blockOverlapQueue,
                m_BlockOverlaps = blockOverlapList,
                m_OverlapGroups = overlapGroupsList,
            }.Schedule(findOverlappingBlocksJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);

            var clearBlocksJobHandle = new ClearBlocksJob {
                m_OverlapGroups = overlapGroupsList.AsDeferredJobArray(),
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_ParcelDataLookup = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_BlockLookup = SystemAPI.GetComponentLookup<Block>(),
                m_BuildOrderLookup = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_ValidAreaLookup = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BlockOverlapArray = blockOverlapList.AsDeferredJobArray(),
                m_CellLookup = SystemAPI.GetBufferLookup<Cell>(),
            }.Schedule(overlapGroupsList, 1, groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(clearBlocksJobHandle);
            overlapGroupsList.Dispose(clearBlocksJobHandle);

            var updateBlocksJobHandle = new CellCheckHelpers.UpdateBlocksJob {
                m_Blocks    = updatedBlocksArray,
                m_BlockData = SystemAPI.GetComponentLookup<Block>(),
                m_Cells     = SystemAPI.GetBufferLookup<Cell>(),
            }.Schedule(updatedBlocksList, 1, clearBlocksJobHandle);

            var updateLotSizeJobHandle = new LotSizeJobs.UpdateLotSizeJob {
                m_Blocks         = updatedBlocksArray,
                m_ZonePrefabs    = m_ZoneSystem.GetPrefabs(),
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_UpdatedData    = SystemAPI.GetComponentLookup<Updated>(),
                m_ZoneData       = SystemAPI.GetComponentLookup<ZoneData>(),
                m_Cells          = SystemAPI.GetBufferLookup<Cell>(),
                m_SearchTree     = zoneSearchTree,
                m_VacantLots     = SystemAPI.GetBufferLookup<VacantLot>(),
                m_CommandBuffer  = m_ModificationBarrier5.CreateCommandBuffer().AsParallelWriter(),
                m_BoundsQueue    = boundsQueue.AsParallelWriter(),
            }.Schedule(updatedBlocksList, 1, updateBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(updateLotSizeJobHandle);
            updatedBlocksList.Dispose(updateLotSizeJobHandle);
            updatedBlocksArray.Dispose(updateLotSizeJobHandle);

            var updateBoundsJobHandle = new LotSizeJobs.UpdateBoundsJob {
                m_BoundsList  = m_ZoneUpdateCollectSystem.GetUpdatedBounds(false, out var updatedBoundsJobHandle),
                m_BoundsQueue = boundsQueue,
            }.Schedule(JobHandle.CombineDependencies(updateLotSizeJobHandle, updatedBoundsJobHandle));
            m_ZoneUpdateCollectSystem.AddBoundsWriter(updateBoundsJobHandle);
            boundsQueue.Dispose(updateBoundsJobHandle);

            Dependency = updateLotSizeJobHandle;
        }

        private JobHandle CollectUpdatedBlocks(NativeList<SortedEntity> updateBlocksList) {
            var zoneUpdateQueue   = new NativeQueue<Entity>(Allocator.TempJob);
            var objectUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var netUpdateQueue    = new NativeQueue<Entity>(Allocator.TempJob);
            var areaUpdateQueue   = new NativeQueue<Entity>(Allocator.TempJob);

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
                    m_ResultQueue = netUpdateQueue.AsParallelWriter(),
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
                    m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
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
                    m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedMapTileBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, searchJobHandle));
                m_AreaUpdateCollectSystem.AddMapTileBoundsReader(findUpdatedBlocksJob_MapTiles);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_MapTiles);
            }

            var collectBlocksJobHandle = new CollectBlocksJob {
                m_Queue1     = zoneUpdateQueue,
                m_Queue2     = objectUpdateQueue,
                m_Queue3     = netUpdateQueue,
                m_Queue4     = areaUpdateQueue,
                m_ResultList = updateBlocksList,
            }.Schedule(jobHandle);

            zoneUpdateQueue.Dispose(collectBlocksJobHandle);
            objectUpdateQueue.Dispose(collectBlocksJobHandle);
            netUpdateQueue.Dispose(collectBlocksJobHandle);
            areaUpdateQueue.Dispose(collectBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(jobHandle);

            return collectBlocksJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        public struct SanitizeCellsJob : IJobParallelForDefer {
            [ReadOnly] public required NativeArray<SortedEntity>    m_Blocks;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>  m_ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>   m_PrefabRefLookup;
            public required            ComponentLookup<ValidArea>   m_ValidAreaLookup;

            public void Execute(int index) {
                var entity = m_Blocks[index].m_Entity;

                // Specifically only reevaluate blocks part of a parcel
                if (!m_ParcelOwnerLookup.TryGetComponent(entity, out var parcelOwner)) {
                    return;
                }

                // Reset the valid area for all parcels
                var validArea  = m_ValidAreaLookup[entity];
                var prefabRef  = m_PrefabRefLookup[parcelOwner.m_Owner];
                var parcelData = m_ParcelDataLookup[prefabRef.m_Prefab];
                validArea.m_Area.y = parcelData.m_LotSize.x;
                validArea.m_Area.w = parcelData.m_LotSize.y;
                m_ValidAreaLookup[entity] = validArea;
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        public struct ClearBlocksJob : IJobParallelForDefer {
            [ReadOnly]                            public required NativeArray<OverlapGroup>    m_OverlapGroups;
            [ReadOnly]                            public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly]                            public required ComponentLookup<ParcelData>  m_ParcelDataLookup;
            [ReadOnly]                            public required ComponentLookup<PrefabRef>   m_PrefabRefLookup;
            [ReadOnly]                            public required ComponentLookup<Block>       m_BlockLookup;
            [ReadOnly]                            public required ComponentLookup<BuildOrder>  m_BuildOrderLookup;
            [NativeDisableParallelForRestriction] public required ComponentLookup<ValidArea>   m_ValidAreaLookup;
            [NativeDisableParallelForRestriction] public required NativeArray<BlockOverlap>    m_BlockOverlapArray;
            [NativeDisableParallelForRestriction] public required BufferLookup<Cell>           m_CellLookup;

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];

                var overlapIterator = new OverlapIterator(
                    blockLookup: m_BlockLookup,
                    validAreaLookup: m_ValidAreaLookup,
                    buildOrderLookup: m_BuildOrderLookup,
                    cellsBLook: m_CellLookup,
                    parcelOwnerLookup: m_ParcelOwnerLookup,
                    parcelDataLookup: m_ParcelDataLookup,
                    prefabRefLookup: m_PrefabRefLookup
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

                private ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
                private ComponentLookup<ParcelData>  m_ParcelDataLookup;
                private ComponentLookup<PrefabRef>   m_PrefabRefLookup;
                private ComponentLookup<Block>       m_BlockLookup;
                private ComponentLookup<ValidArea>   m_ValidAreaLookup;
                private ComponentLookup<BuildOrder>  m_BuildOrderLookup;
                private BufferLookup<Cell>           m_CellBLook;
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
                private DynamicBuffer<Cell>          m_OtherCells;
                private bool                         m_OtherBlockIsParcel;
                private int2                         m_OtherBlockParcelBounds;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerLookup, ComponentLookup<Block>      blockLookup,
                                       ComponentLookup<ValidArea>   validAreaLookup,   ComponentLookup<BuildOrder> buildOrderLookup,
                                       BufferLookup<Cell>           cellsBLook,        ComponentLookup<ParcelData> parcelDataLookup,
                                       ComponentLookup<PrefabRef>   prefabRefLookup) : this() {
                    m_ParcelOwnerLookup = parcelOwnerLookup;
                    m_BlockLookup       = blockLookup;
                    m_ValidAreaLookup   = validAreaLookup;
                    m_BuildOrderLookup  = buildOrderLookup;
                    m_CellBLook         = cellsBLook;
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
                    m_CurCellBuffer        = m_CellBLook[curBlockEntity];
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
                    m_OtherCells             = m_CellBLook[otherBlock];
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
                    var otherCell = m_OtherCells[otherIndex];

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

                    m_CurCellBuffer[curIndex] = curCell;
                    m_OtherCells[otherIndex]  = otherCell;
                }
            }
        }
    }
}