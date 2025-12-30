// <copyright file="P_NewCellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Text;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;
    using static Game.Zones.CellCheckHelpers;
    using static Game.Zones.CellOccupyJobs;
    using static Game.Zones.CellOverlapJobs;
    using static Game.Zones.LotSizeJobs;
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
    /// After the vanilla system runs, our two types of Blocks have the following state:
    /// 
    /// "Wide" Parcels:
    /// They will always take priority over vanilla cells and therefore should
    /// have all correct flags set, except for the cells over their lot depth which will incorrectly be visible.
    ///
    /// "Narrow" Parcels:
    /// If placed in the wild, they will have all correct flags set. They will be 2 wide and 6 deep.
    /// If placed overlapping vanilla grid, inside cells are correct, outside are shared. The underlying vanilla grid will be shared inside, normal outside.
    /// If placed overlaying vanilla grid at an offset, they are blocked.
    /// Most importantly, the logic that "occpies" them if they are 1 wide is caused by the depth smoothing logic inside the CellReduction struct within CellOverlapJobs.cs.
    ///
    /// So we need to do the following:
    /// We want to mostly re-run the vanilla CellCheckSystem, except for:
    /// 1. The first job (BlockCellsJob) where we want to trim cells over lot depth/width depending on parcel type.
    /// 2. The overlap job (CheckBlockOverlapJob) which contains the cell reduction logic which causes 1-wide parcels to be occupied incorrectly.
    /// which we will rewrite and run as replacement to the base game jobs here.
    ///
    /// </summary>
    public partial class P_NewCellCheckSystem : PlatterGameSystemBase {
        private SearchSystem                     m_AreaSearchSystem;
        private Game.Net.SearchSystem            m_NetSearchSystem;
        private Game.Zones.SearchSystem          m_ZoneSearchSystem;
        private Game.Objects.SearchSystem        m_ObjectSearchSystem;
        private UpdateCollectSystem              m_AreaUpdateCollectSystem;
        private Game.Net.UpdateCollectSystem     m_NetUpdateCollectSystem;
        private Game.Objects.UpdateCollectSystem m_ObjectUpdateCollectSystem;
        private Game.Zones.UpdateCollectSystem   m_ZoneUpdateCollectSystem;
        private ZoneSystem                       m_ZoneSystem;
        private ModificationBarrier5             m_ModificationBarrier5;
        private EntityQuery                      m_DeletedBlocksQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            m_ObjectUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            m_NetUpdateCollectSystem    = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            m_AreaUpdateCollectSystem   = World.GetOrCreateSystemManaged<UpdateCollectSystem>();
            m_ZoneSearchSystem          = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_ObjectSearchSystem        = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_NetSearchSystem           = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_AreaSearchSystem          = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ZoneSystem                = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_ModificationBarrier5      = World.GetOrCreateSystemManaged<ModificationBarrier5>();

            m_DeletedBlocksQuery = SystemAPI.QueryBuilder().WithAll<Block, Deleted>().WithNone<Temp>().Build();
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
            var boundsQueue        = new NativeQueue<Bounds2>(Allocator.TempJob);
            var filteredBlocksList = new NativeList<SortedEntity>(Allocator.TempJob);

            var collectUpdatedBlocksJobHandle = CollectUpdatedBlocks(updatedBlocksList);

            // Filter blocks to only include those that are part of a parcel
            var filterBlocksJobHandle = new FilterBlocksToParcelsJob {
                m_InputBlocks       = updatedBlocksList,
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(true),
                m_OutputBlocks      = filteredBlocksList,
            }.Schedule(JobHandle.CombineDependencies(Dependency, collectUpdatedBlocksJobHandle));
            var filteredBlocks = filteredBlocksList.AsDeferredJobArray();

            var zoneSearchTree    = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var deletedBlocksList = m_DeletedBlocksQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var deletedBlocksJobHandle);

            // Process Updated Blocks. This is one of the jobs we maintain ourselves.
            var processUpdatedBlocksJobHandle = new BlockCellsJob {
                m_Blocks                      = filteredBlocks,
                m_BlockLookup                 = SystemAPI.GetComponentLookup<Block>(),
                m_ParcelOwnerLookup           = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_ParcelDataLookup            = SystemAPI.GetComponentLookup<ParcelData>(),
                m_NetSearchTree               = m_NetSearchSystem.GetNetSearchTree(true, out var netSearchJobHandle),
                m_AreaSearchTree              = m_AreaSearchSystem.GetSearchTree(true, out var areaSearchJobHandle),
                m_OwnerLookup                 = SystemAPI.GetComponentLookup<Owner>(),
                m_TransformLookup             = SystemAPI.GetComponentLookup<Game.Objects.Transform>(),
                m_EdgeGeometryLookup          = SystemAPI.GetComponentLookup<EdgeGeometry>(),
                m_StartNodeGeometryLookup     = SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                m_EndNodeGeometryLookup       = SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                m_CompositionLookup           = SystemAPI.GetComponentLookup<Composition>(),
                m_PrefabRefLookup             = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_NetCompositionLookup        = SystemAPI.GetComponentLookup<NetCompositionData>(),
                m_PrefabRoadCompositionLookup = SystemAPI.GetComponentLookup<RoadComposition>(),
                m_PrefabAreaGeometryLookup    = SystemAPI.GetComponentLookup<AreaGeometryData>(),
                m_PrefabObjectGeometryLookup  = SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                m_NativeLookup                = SystemAPI.GetComponentLookup<Native>(),
                m_CellsLookup                 = SystemAPI.GetBufferLookup<Cell>(),
                m_AreaNodesLookup             = SystemAPI.GetBufferLookup<Game.Areas.Node>(),
                m_AreaTrianglesLookup         = SystemAPI.GetBufferLookup<Game.Areas.Triangle>(),
                m_ValidAreaLookup             = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(filteredBlocksList, 1, JobHandle.CombineDependencies(filterBlocksJobHandle, netSearchJobHandle, areaSearchJobHandle));
            m_NetSearchSystem.AddNetSearchTreeReader(processUpdatedBlocksJobHandle);
            m_AreaSearchSystem.AddSearchTreeReader(processUpdatedBlocksJobHandle);

            // Find Overlapping Blocks
            var findOverlappingBlocksJobHandle = new CellCheckHelpers.FindOverlappingBlocksJob {
                m_Blocks         = filteredBlocks,
                m_SearchTree     = zoneSearchTree,
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(true),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(true),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(true),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(filteredBlocksList, 1, JobHandle.CombineDependencies(processUpdatedBlocksJobHandle, zoneSearchJobHandle));

            // Group Overlapping Blocks
            var groupOverlappingBlocksJobHandle = new CellCheckHelpers.GroupOverlappingBlocksJob {
                m_Blocks        = filteredBlocks,
                m_OverlapQueue  = blockOverlapQueue,
                m_BlockOverlaps = blockOverlapList,
                m_OverlapGroups = overlapGroupsList,
            }.Schedule(findOverlappingBlocksJobHandle);

            // Zone and Occupy Cells
            var objectSearchTree = m_ObjectSearchSystem.GetStaticSearchTree(true, out var objectSearchJobHandle);
            var zoneAndOccupyCellsJobHandle = new CellOccupyJobs.ZoneAndOccupyCellsJob {
                m_Blocks                        = filteredBlocks,
                m_DeletedBlockChunks            = deletedBlocksList,
                m_ZonePrefabs                   = m_ZoneSystem.GetPrefabs(),
                m_EntityType                    = SystemAPI.GetEntityTypeHandle(),
                m_BlockData                     = SystemAPI.GetComponentLookup<Block>(true),
                m_ValidAreaData                 = SystemAPI.GetComponentLookup<ValidArea>(true),
                m_ObjectSearchTree              = objectSearchTree,
                m_TransformData                 = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                m_ElevationData                 = SystemAPI.GetComponentLookup<Game.Objects.Elevation>(true),
                m_PrefabRefData                 = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_PrefabObjectGeometryData      = SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                m_PrefabSpawnableBuildingData   = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true),
                m_PrefabSignatureBuildingData   = SystemAPI.GetComponentLookup<SignatureBuildingData>(true),
                m_PrefabPlaceholderBuildingData = SystemAPI.GetComponentLookup<PlaceholderBuildingData>(true),
                m_PrefabZoneData                = SystemAPI.GetComponentLookup<ZoneData>(true),
                m_PrefabData                    = SystemAPI.GetComponentLookup<PrefabData>(true),
                m_Cells                         = SystemAPI.GetBufferLookup<Cell>(false),
            }.Schedule(filteredBlocksList, 1, JobHandle.CombineDependencies(processUpdatedBlocksJobHandle, objectSearchJobHandle, deletedBlocksJobHandle));

            // Check Block Overlap
            var checkBlockOverlapJobHandle = new CheckBlockOverlapJob {
                m_BlockOverlaps   = blockOverlapList.AsDeferredJobArray(),
                m_OverlapGroups   = overlapGroupsList.AsDeferredJobArray(),
                m_ZonePrefabs     = m_ZoneSystem.GetPrefabs(),
                m_BlockData       = SystemAPI.GetComponentLookup<Block>(true),
                m_BuildOrderData  = SystemAPI.GetComponentLookup<BuildOrder>(true),
                m_ZoneData        = SystemAPI.GetComponentLookup<ZoneData>(true),
                m_Cells           = SystemAPI.GetBufferLookup<Cell>(false),
                m_ValidAreaData   = SystemAPI.GetComponentLookup<ValidArea>(false),
                // Add new lookups for narrow parcel detection
                m_ParcelOwnerData = SystemAPI.GetComponentLookup<ParcelOwner>(true),
                m_PrefabRefData   = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_ParcelData      = SystemAPI.GetComponentLookup<ParcelData>(true),
            }.Schedule(overlapGroupsList, 1, JobHandle.CombineDependencies(groupOverlappingBlocksJobHandle, zoneAndOccupyCellsJobHandle));

            // Update Blocks
            var updateBlocksJobHandle = new CellCheckHelpers.UpdateBlocksJob {
                m_Blocks    = filteredBlocks,
                m_BlockData = SystemAPI.GetComponentLookup<Block>(true),
                m_Cells     = SystemAPI.GetBufferLookup<Cell>(false),
            }.Schedule(filteredBlocksList, 1, checkBlockOverlapJobHandle);

            // Update Lot Size
            var updateLotSizeJobHandle = new LotSizeJobs.UpdateLotSizeJob {
                m_Blocks         = filteredBlocks,
                m_ZonePrefabs    = m_ZoneSystem.GetPrefabs(),
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(true),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(true),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(true),
                m_UpdatedData    = SystemAPI.GetComponentLookup<Updated>(true),
                m_ZoneData       = SystemAPI.GetComponentLookup<ZoneData>(true),
                m_Cells          = SystemAPI.GetBufferLookup<Cell>(true),
                m_SearchTree     = zoneSearchTree,
                m_VacantLots     = SystemAPI.GetBufferLookup<VacantLot>(false),
                m_CommandBuffer  = m_ModificationBarrier5.CreateCommandBuffer().AsParallelWriter(),
                m_BoundsQueue    = boundsQueue.AsParallelWriter(),
            }.Schedule(filteredBlocksList, 1, updateBlocksJobHandle);

            // Update Bounds
            var updateBoundsJobHandle = new LotSizeJobs.UpdateBoundsJob {
                m_BoundsList  = m_ZoneUpdateCollectSystem.GetUpdatedBounds(false, out var zoneUpdateJobHandle),
                m_BoundsQueue = boundsQueue,
            }.Schedule(JobHandle.CombineDependencies(updateLotSizeJobHandle, zoneUpdateJobHandle));

            updatedBlocksList.Dispose(filterBlocksJobHandle);
            filteredBlocksList.Dispose(updateLotSizeJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(checkBlockOverlapJobHandle);
            overlapGroupsList.Dispose(checkBlockOverlapJobHandle);
            boundsQueue.Dispose(updateBoundsJobHandle);
            deletedBlocksList.Dispose(zoneAndOccupyCellsJobHandle);

            m_ZoneSearchSystem.AddSearchTreeReader(updateLotSizeJobHandle);
            m_ObjectSearchSystem.AddStaticSearchTreeReader(zoneAndOccupyCellsJobHandle);
            m_ZoneSystem.AddPrefabsReader(updateLotSizeJobHandle);
            m_ModificationBarrier5.AddJobHandleForProducer(updateLotSizeJobHandle);
            m_ZoneUpdateCollectSystem.AddBoundsWriter(updateBoundsJobHandle);

            Dependency = updateLotSizeJobHandle;
        }

        private JobHandle CollectUpdatedBlocks(NativeList<SortedEntity> updateBlocksList) {
            var zoneUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var objectUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var netUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var areaUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);

            var zoneSearchTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var jobHandle = default(JobHandle);

            if (m_ZoneUpdateCollectSystem.isUpdated) {
                var updatedBounds = m_ZoneUpdateCollectSystem.GetUpdatedBounds(true, out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Zones = new FindUpdatedBlocksSingleIterationJob {
                    m_Bounds = updatedBounds.AsDeferredJobArray(),
                    m_SearchTree = zoneSearchTree,
                    m_ResultQueue = zoneUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_ZoneUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Zones);
                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Zones);
            }

            if (m_ObjectUpdateCollectSystem.isUpdated) {
                var updatedBounds = m_ObjectUpdateCollectSystem.GetUpdatedBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Objects = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds = updatedBounds.AsDeferredJobArray(),
                    m_SearchTree = zoneSearchTree,
                    m_ResultQueue = objectUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_ObjectUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Objects);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Objects);
            }

            if (m_NetUpdateCollectSystem.netsUpdated) {
                var updatedNetBounds = m_NetUpdateCollectSystem.GetUpdatedNetBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Nets = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds = updatedNetBounds.AsDeferredJobArray(),
                    m_SearchTree = zoneSearchTree,
                    m_ResultQueue = netUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedNetBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_NetUpdateCollectSystem.AddNetBoundsReader(findUpdatedBlocksJob_Nets);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Nets);
            }

            var searchJobHandle = zoneSearchJobHandle;
            if (m_AreaUpdateCollectSystem.lotsUpdated) {
                var updatedLotBounds = m_AreaUpdateCollectSystem.GetUpdatedLotBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Areas = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds = updatedLotBounds.AsDeferredJobArray(),
                    m_SearchTree = zoneSearchTree,
                    m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedLotBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, zoneSearchJobHandle));
                m_AreaUpdateCollectSystem.AddLotBoundsReader(findUpdatedBlocksJob_Areas);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Areas);
                searchJobHandle = findUpdatedBlocksJob_Areas;
            }

            if (m_AreaUpdateCollectSystem.mapTilesUpdated) {
                var updatedMapTileBounds = m_AreaUpdateCollectSystem.GetUpdatedMapTileBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_MapTiles = new FindUpdatedBlocksDoubleIterationJob {
                    m_Bounds = updatedMapTileBounds.AsDeferredJobArray(),
                    m_SearchTree = zoneSearchTree,
                    m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
                }.Schedule(updatedMapTileBounds, 1, JobHandle.CombineDependencies(updatedBoundsJobHandle, searchJobHandle));
                m_AreaUpdateCollectSystem.AddMapTileBoundsReader(findUpdatedBlocksJob_MapTiles);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_MapTiles);
            }

            var collectBlocksJobHandle = new CollectBlocksJob {
                m_Queue1 = zoneUpdateQueue,
                m_Queue2 = objectUpdateQueue,
                m_Queue3 = netUpdateQueue,
                m_Queue4 = areaUpdateQueue,
                m_ResultList = updateBlocksList,
            }.Schedule(jobHandle);

            zoneUpdateQueue.Dispose(collectBlocksJobHandle);
            objectUpdateQueue.Dispose(collectBlocksJobHandle);
            netUpdateQueue.Dispose(collectBlocksJobHandle);
            areaUpdateQueue.Dispose(collectBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(jobHandle);

            return collectBlocksJobHandle;
        }

        /// <summary>
        /// Filters the input list of blocks to only include those that are part of a parcel.
        /// </summary>
        #if USE_BURST
        [BurstCompile]
        #endif
        private struct FilterBlocksToParcelsJob : IJob {
            [ReadOnly] public required NativeList<SortedEntity>     m_InputBlocks;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            public required            NativeList<SortedEntity>     m_OutputBlocks;

            public void Execute() {
                for (var i = 0; i < m_InputBlocks.Length; i++) {
                    var entity = m_InputBlocks[i].m_Entity;
                    if (m_ParcelOwnerLookup.HasComponent(entity)) {
                        m_OutputBlocks.Add(m_InputBlocks[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Modified copy of Game.Zones.CellOverlapJobs.CheckBlockOverlapJob from the base game.
        /// Checks for overlaps between blocks within a group and updates cell states accordingly.
        ///
        /// CUSTOM MODIFICATIONS:
        /// 1. Narrow Parcel Protection - Added IsNarrowParcel() and m_IsNarrowParcel flag to CellReduction.
        ///    When a block belongs to a 1-wide parcel, depth reduction is skipped entirely.
        /// 2. Parcel Zone Clearing - Added m_CurBlockIsParcel/m_OtherBlockIsParcel to OverlapIterator.
        ///    In cell sharing phase, when one block is a parcel, we clear the zone from the non-parcel
        ///    block's cells instead of copying zones.
        ///
        /// ADDED FIELDS: m_ParcelOwnerData, m_PrefabRefData, m_ParcelData
        /// MODIFIED STRUCTS: CellReduction (m_IsNarrowParcel), OverlapIterator (parcel detection + zone clearing)
        /// </summary>
        #if USE_BURST
        [BurstCompile]
        #endif
        public struct CheckBlockOverlapJob : IJobParallelForDefer {
            // [CUSTOM] Checks if a block belongs to a narrow (1-wide) parcel
            private bool IsNarrowParcel(Entity blockEntity) {
                if (m_ParcelOwnerData.TryGetComponent(blockEntity, out var parcelOwner) &&
                    m_PrefabRefData.TryGetComponent(parcelOwner.m_Owner, out var prefabRef) &&
                    m_ParcelData.TryGetComponent(prefabRef.m_Prefab, out var parcelData)) {
                    return parcelData.m_LotSize.x == 1;
                }
                
                return false;
            }

            public void Execute(int index) {
                var overlapGroup   = m_OverlapGroups[index];
                var blockOverlap   = default(CellCheckHelpers.BlockOverlap);
                var num            = 0;
                var block          = default(Block);
                var buildOrder     = default(BuildOrder);

                // Phase 1: Identify Neighbors
                // Iterate through overlaps to identify left and right neighbors for each block.
                for (var i = overlapGroup.m_StartIndex; i < overlapGroup.m_EndIndex; i++) {
                    var blockOverlap2 = m_BlockOverlaps[i];
                    if (blockOverlap2.m_Block != blockOverlap.m_Block) {
                        if (blockOverlap.m_Block != Entity.Null) m_BlockOverlaps[num] = blockOverlap;
                        blockOverlap = blockOverlap2;
                        num = i;
                        block = m_BlockData[blockOverlap2.m_Block];
                        var validArea = m_ValidAreaData[blockOverlap2.m_Block];
                        buildOrder = m_BuildOrderData[blockOverlap2.m_Block];
                        var dynamicBuffer = m_Cells[blockOverlap2.m_Block];
                    }

                    if (blockOverlap2.m_Other != Entity.Null) {
                        var block2 = m_BlockData[blockOverlap2.m_Other];
                        var buildOrder2 = m_BuildOrderData[blockOverlap2.m_Other];

                        // Check if the other block is a valid neighbor and determine direction (Left/Right)
                        if (ZoneUtils.IsNeighbor(block, block2, buildOrder, buildOrder2)) {
                            if (math.dot(block2.m_Position.xz - block.m_Position.xz,
                                    MathUtils.Right(block.m_Direction)) > 0f)
                                blockOverlap.m_Left = blockOverlap2.m_Other;
                            else
                                blockOverlap.m_Right = blockOverlap2.m_Other;
                        }
                    }
                }

                if (blockOverlap.m_Block != Entity.Null) m_BlockOverlaps[num] = blockOverlap;

                // Setup iterators for processing overlaps
                var overlapIterator = default(OverlapIterator);
                overlapIterator.m_BlockDataFromEntity = m_BlockData;
                overlapIterator.m_ValidAreaDataFromEntity = m_ValidAreaData;
                overlapIterator.m_BuildOrderDataFromEntity = m_BuildOrderData;
                overlapIterator.m_CellsFromEntity = m_Cells;
                overlapIterator.m_ParcelOwnerData = m_ParcelOwnerData; // [CUSTOM]

                var cellReduction = default(CellReduction);
                cellReduction.m_BlockDataFromEntity = m_BlockData;
                cellReduction.m_ValidAreaDataFromEntity = m_ValidAreaData;
                cellReduction.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction.m_CellsFromEntity = m_Cells;

                // Phase 2: Mark Redundant Cells (First Pass)
                // Propagate redundancy flags based on initial overlap data.
                cellReduction.m_Flag = CellFlags.Redundant;
                for (var j = overlapGroup.m_StartIndex; j < overlapGroup.m_EndIndex; j++) {
                    var blockOverlap3 = m_BlockOverlaps[j];
                    if (blockOverlap3.m_Block != overlapIterator.m_BlockEntity) {
                        if (cellReduction.m_BlockEntity != Entity.Null) cellReduction.Perform();
                        cellReduction.m_BlockEntity = blockOverlap3.m_Block;
                        cellReduction.m_LeftNeightbor = blockOverlap3.m_Left;
                        cellReduction.m_RightNeightbor = blockOverlap3.m_Right;
                        cellReduction.m_IsNarrowParcel = IsNarrowParcel(blockOverlap3.m_Block); // [CUSTOM]
                        overlapIterator.m_BlockEntity = blockOverlap3.m_Block;
                        overlapIterator.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap3.m_Block); // [CUSTOM]
                        overlapIterator.m_BlockData = m_BlockData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_ValidAreaData = m_ValidAreaData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_BuildOrderData = m_BuildOrderData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Cells = m_Cells[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Quad = ZoneUtils.CalculateCorners(overlapIterator.m_BlockData,
                            overlapIterator.m_ValidAreaData);
                        overlapIterator.m_Bounds = MathUtils.Bounds(overlapIterator.m_Quad);
                    }

                    // Check for geometric overlaps if valid area exists
                    if (overlapIterator.m_ValidAreaData.m_Area.y > overlapIterator.m_ValidAreaData.m_Area.x &&
                        blockOverlap3.m_Other != Entity.Null) overlapIterator.Iterate(blockOverlap3.m_Other);
                }

                if (cellReduction.m_BlockEntity != Entity.Null) cellReduction.Perform();

                // Phase 3: Check Physical Overlaps (Blocking)
                // Iterate again to mark cells as Blocked where physical overlaps occur.
                overlapIterator.m_BlockEntity = Entity.Null;
                overlapIterator.m_CheckBlocking = true;
                cellReduction.m_BlockEntity = Entity.Null;
                for (var k = overlapGroup.m_StartIndex; k < overlapGroup.m_EndIndex; k++) {
                    var blockOverlap4 = m_BlockOverlaps[k];
                    if (blockOverlap4.m_Block != overlapIterator.m_BlockEntity) {
                        if (cellReduction.m_BlockEntity != Entity.Null) {
                            // Clear redundant flags before applying blocked flags to ensure accuracy
                            cellReduction.m_Flag = CellFlags.Redundant;
                            cellReduction.Clear();
                            cellReduction.m_Flag = CellFlags.Blocked;
                            cellReduction.Perform();
                        }

                        cellReduction.m_BlockEntity = blockOverlap4.m_Block;
                        cellReduction.m_LeftNeightbor = blockOverlap4.m_Left;
                        cellReduction.m_RightNeightbor = blockOverlap4.m_Right;
                        cellReduction.m_IsNarrowParcel = IsNarrowParcel(blockOverlap4.m_Block);
                        overlapIterator.m_BlockEntity = blockOverlap4.m_Block;
                        overlapIterator.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap4.m_Block);
                        overlapIterator.m_BlockData = m_BlockData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_ValidAreaData = m_ValidAreaData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_BuildOrderData = m_BuildOrderData[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Cells = m_Cells[overlapIterator.m_BlockEntity];
                        overlapIterator.m_Quad = ZoneUtils.CalculateCorners(overlapIterator.m_BlockData,
                            overlapIterator.m_ValidAreaData);
                        overlapIterator.m_Bounds = MathUtils.Bounds(overlapIterator.m_Quad);
                    }

                    if (overlapIterator.m_ValidAreaData.m_Area.y > overlapIterator.m_ValidAreaData.m_Area.x &&
                        blockOverlap4.m_Other != Entity.Null) overlapIterator.Iterate(blockOverlap4.m_Other);
                }

                if (cellReduction.m_BlockEntity != Entity.Null) {
                    cellReduction.m_Flag = CellFlags.Redundant;
                    cellReduction.Clear();
                    cellReduction.m_Flag = CellFlags.Blocked;
                    cellReduction.Perform();
                }

                // Phase 4: Redundant Cleanup
                // Final pass to ensure redundant cells are correctly marked after blocking logic.
                var cellReduction2 = default(CellReduction);
                cellReduction2.m_BlockDataFromEntity = m_BlockData;
                cellReduction2.m_ValidAreaDataFromEntity = m_ValidAreaData;
                cellReduction2.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction2.m_CellsFromEntity = m_Cells;
                cellReduction2.m_Flag = CellFlags.Redundant;
                for (var l = overlapGroup.m_StartIndex; l < overlapGroup.m_EndIndex; l++) {
                    var blockOverlap5 = m_BlockOverlaps[l];
                    if (blockOverlap5.m_Block != cellReduction2.m_BlockEntity) {
                        cellReduction2.m_BlockEntity = blockOverlap5.m_Block;
                        cellReduction2.m_LeftNeightbor = blockOverlap5.m_Left;
                        cellReduction2.m_RightNeightbor = blockOverlap5.m_Right;
                        cellReduction2.m_IsNarrowParcel = IsNarrowParcel(blockOverlap5.m_Block);
                        cellReduction2.Perform();
                    }
                }

                // Phase 5: Process Occupied Cells
                // Handle cells that are already occupied (e.g., by buildings), adjusting depth based on neighbors.
                var cellReduction3 = default(CellReduction);
                cellReduction3.m_ZonePrefabs = m_ZonePrefabs;
                cellReduction3.m_BlockDataFromEntity = m_BlockData;
                cellReduction3.m_ValidAreaDataFromEntity = m_ValidAreaData;
                cellReduction3.m_BuildOrderDataFromEntity = m_BuildOrderData;
                cellReduction3.m_ZoneData = m_ZoneData;
                cellReduction3.m_CellsFromEntity = m_Cells;
                cellReduction3.m_Flag = CellFlags.Occupied;
                for (var m = overlapGroup.m_StartIndex; m < overlapGroup.m_EndIndex; m++) {
                    var blockOverlap6 = m_BlockOverlaps[m];
                    if (blockOverlap6.m_Block != cellReduction3.m_BlockEntity) {
                        cellReduction3.m_BlockEntity = blockOverlap6.m_Block;
                        cellReduction3.m_LeftNeightbor = blockOverlap6.m_Left;
                        cellReduction3.m_RightNeightbor = blockOverlap6.m_Right;
                        cellReduction3.m_IsNarrowParcel = IsNarrowParcel(blockOverlap6.m_Block);
                        cellReduction3.Perform();
                    }
                }

                // Phase 6: Check Cell Sharing
                // Determine if cells can be shared between blocks (e.g., corner lots).
                var overlapIterator2 = default(OverlapIterator);
                overlapIterator2.m_BlockDataFromEntity = m_BlockData;
                overlapIterator2.m_ValidAreaDataFromEntity = m_ValidAreaData;
                overlapIterator2.m_BuildOrderDataFromEntity = m_BuildOrderData;
                overlapIterator2.m_CellsFromEntity = m_Cells;
                overlapIterator2.m_ParcelOwnerData = m_ParcelOwnerData;
                overlapIterator2.m_CheckSharing = true;
                for (var n = overlapGroup.m_StartIndex; n < overlapGroup.m_EndIndex; n++) {
                    var blockOverlap7 = m_BlockOverlaps[n];
                    if (blockOverlap7.m_Block != overlapIterator2.m_BlockEntity) {
                        overlapIterator2.m_BlockEntity = blockOverlap7.m_Block;
                        overlapIterator2.m_CurBlockIsParcel = m_ParcelOwnerData.HasComponent(blockOverlap7.m_Block);
                        overlapIterator2.m_BlockData = m_BlockData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_ValidAreaData = m_ValidAreaData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_BuildOrderData = m_BuildOrderData[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_Cells = m_Cells[overlapIterator2.m_BlockEntity];
                        overlapIterator2.m_Quad = ZoneUtils.CalculateCorners(overlapIterator2.m_BlockData,
                            overlapIterator2.m_ValidAreaData);
                        overlapIterator2.m_Bounds = MathUtils.Bounds(overlapIterator2.m_Quad);
                    }

                    if (overlapIterator2.m_ValidAreaData.m_Area.y > overlapIterator2.m_ValidAreaData.m_Area.x &&
                        blockOverlap7.m_Other != Entity.Null) overlapIterator2.Iterate(blockOverlap7.m_Other);
                }
            }

            [NativeDisableParallelForRestriction] public NativeArray<CellCheckHelpers.BlockOverlap> m_BlockOverlaps;

            [ReadOnly] public NativeArray<CellCheckHelpers.OverlapGroup> m_OverlapGroups;

            [ReadOnly] public ZonePrefabs m_ZonePrefabs;

            [ReadOnly] public ComponentLookup<Block> m_BlockData;

            [ReadOnly] public ComponentLookup<BuildOrder> m_BuildOrderData;

            [ReadOnly] public ComponentLookup<ZoneData> m_ZoneData;

            [NativeDisableParallelForRestriction] public BufferLookup<Cell> m_Cells;

            [NativeDisableParallelForRestriction] public ComponentLookup<ValidArea> m_ValidAreaData;

            // --- [CUSTOM] Parcel detection lookups ---
            [ReadOnly] public ComponentLookup<ParcelOwner> m_ParcelOwnerData;
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
            [ReadOnly] public ComponentLookup<ParcelData> m_ParcelData;

            /// <summary>
            /// Modified vanilla CellReduction. [CUSTOM] Added m_IsNarrowParcel - when true, Perform() exits early.
            /// </summary>
            private struct CellReduction {
                /// <summary>
                /// Clears the specified flag from all cells in the valid area.
                /// </summary>
                public void Clear() {
                    m_BlockData = m_BlockDataFromEntity[m_BlockEntity];
                    m_ValidAreaData = m_ValidAreaDataFromEntity[m_BlockEntity];
                    m_Cells = m_CellsFromEntity[m_BlockEntity];
                    for (var i = m_ValidAreaData.m_Area.x; i < m_ValidAreaData.m_Area.y; i++)
                        for (var j = m_ValidAreaData.m_Area.z; j < m_ValidAreaData.m_Area.w; j++) {
                            var num = j * m_BlockData.m_Size.x + i;
                            var cell = m_Cells[num];
                            if ((cell.m_State & m_Flag) != CellFlags.None) {
                                cell.m_State &= ~m_Flag;
                                m_Cells[num] = cell;
                            }
                        }
                }

                /// <summary>
                /// Performs the reduction logic, propagating flags and adjusting depth based on neighbors.
                /// </summary>
                public void Perform() {
                    m_BlockData = m_BlockDataFromEntity[m_BlockEntity];
                    m_ValidAreaData = m_ValidAreaDataFromEntity[m_BlockEntity];
                    m_BuildOrderData = m_BuildOrderDataFromEntity[m_BlockEntity];
                    m_Cells = m_CellsFromEntity[m_BlockEntity];

                    // [CUSTOM] Skip depth reduction for narrow parcels
                    if (m_IsNarrowParcel) {
                        // Only update ValidArea if we're in the Blocked phase
                        if (m_Flag == CellFlags.Blocked) {
                            m_ValidAreaDataFromEntity[m_BlockEntity] = m_ValidAreaData;
                        }
                        return;
                    }

                    if (m_LeftNeightbor != Entity.Null) {
                        m_LeftBlockData = m_BlockDataFromEntity[m_LeftNeightbor];
                        m_LeftValidAreaData = m_ValidAreaDataFromEntity[m_LeftNeightbor];
                        m_LeftBuildOrderData = m_BuildOrderDataFromEntity[m_LeftNeightbor];
                        m_LeftCells = m_CellsFromEntity[m_LeftNeightbor];
                    } else {
                        m_LeftBlockData = default;
                    }

                    if (m_RightNeightbor != Entity.Null) {
                        m_RightBlockData = m_BlockDataFromEntity[m_RightNeightbor];
                        m_RightValidAreaData = m_ValidAreaDataFromEntity[m_RightNeightbor];
                        m_RightBuildOrderData = m_BuildOrderDataFromEntity[m_RightNeightbor];
                        m_RightCells = m_CellsFromEntity[m_RightNeightbor];
                    } else {
                        m_RightBlockData = default;
                    }

                    var cellFlags = m_Flag | CellFlags.Blocked;
                    for (var i = m_ValidAreaData.m_Area.x; i < m_ValidAreaData.m_Area.y; i++) {
                        var cell = m_Cells[i];
                        var cell2 = m_Cells[m_BlockData.m_Size.x + i];
                        if (((cell.m_State & cellFlags) == CellFlags.None) & ((cell2.m_State & cellFlags) == m_Flag)) {
                            cell.m_State |= m_Flag;
                            m_Cells[i] = cell;
                        }

                        for (var j = m_ValidAreaData.m_Area.z + 1; j < m_ValidAreaData.m_Area.w; j++) {
                            var num = j * m_BlockData.m_Size.x + i;
                            var cell3 = m_Cells[num];
                            if (((cell3.m_State & cellFlags) == CellFlags.None) &
                                ((cell.m_State & cellFlags) == m_Flag)) {
                                cell3.m_State |= m_Flag;
                                m_Cells[num] = cell3;
                            }

                            cell = cell3;
                        }
                    }

                    var num2 = m_ValidAreaData.m_Area.x;
                    var k = m_ValidAreaData.m_Area.y - 1;
                    var validArea = default(ValidArea);
                    validArea.m_Area.xz = m_BlockData.m_Size;
                    while (k >= m_ValidAreaData.m_Area.x) {
                        if (m_Flag == CellFlags.Occupied) {
                            var cell4 = m_Cells[num2];
                            var cell5 = m_Cells[k];
                            var entity = m_ZonePrefabs[cell4.m_Zone];
                            var entity2 = m_ZonePrefabs[cell5.m_Zone];
                            var ptr = m_ZoneData[entity];
                            var zoneData = m_ZoneData[entity2];
                            if ((ptr.m_ZoneFlags & ZoneFlags.SupportNarrow) == (ZoneFlags)0) {
                                var num3 = CalculateLeftDepth(num2, cell4.m_Zone);
                                ReduceDepth(num2, num3);
                            }

                            if ((zoneData.m_ZoneFlags & ZoneFlags.SupportNarrow) == (ZoneFlags)0) {
                                var num4 = CalculateRightDepth(k, cell5.m_Zone);
                                ReduceDepth(k, num4);
                            }
                        } else {
                            var num5 = CalculateLeftDepth(num2, ZoneType.None);
                            ReduceDepth(num2, num5);
                            var num6 = CalculateRightDepth(k, ZoneType.None);
                            ReduceDepth(k, num6);
                            if (k <= num2 && m_Flag == CellFlags.Blocked) {
                                if (num5 != 0 && num2 != k) {
                                    validArea.m_Area.xz = math.min(validArea.m_Area.xz,
                                        new int2(num2, m_ValidAreaData.m_Area.z));
                                    validArea.m_Area.yw = math.max(validArea.m_Area.yw, new int2(num2 + 1, num5));
                                }

                                if (num6 != 0) {
                                    validArea.m_Area.xz = math.min(validArea.m_Area.xz,
                                        new int2(k, m_ValidAreaData.m_Area.z));
                                    validArea.m_Area.yw = math.max(validArea.m_Area.yw, new int2(k + 1, num6));
                                }
                            }
                        }

                        num2++;
                        k--;
                    }

                    if (m_Flag == CellFlags.Blocked) m_ValidAreaDataFromEntity[m_BlockEntity] = validArea;
                }

                private int CalculateLeftDepth(int x, ZoneType zoneType) {
                    var leftNeighborDepth = GetDepth(x - 1, zoneType);
                    var currentDepth      = GetDepth(x, zoneType);
                    if (currentDepth <= leftNeighborDepth) return currentDepth;
                    var rightNeighborDepth = GetDepth(x + 1, zoneType);
                    var farLeftNeighborDepth = GetDepth(x - 2, zoneType);
                    if ((leftNeighborDepth != farLeftNeighborDepth ) & (leftNeighborDepth != 0)) return leftNeighborDepth;
                    if (rightNeighborDepth  - currentDepth < currentDepth - leftNeighborDepth) return math.min(math.max(leftNeighborDepth, rightNeighborDepth ), currentDepth);
                    if (GetDepth(x + 2, zoneType) != rightNeighborDepth ) return math.min(math.max(leftNeighborDepth, rightNeighborDepth ), currentDepth);
                    return leftNeighborDepth;
                }

                private int CalculateRightDepth(int x, ZoneType zoneType) {
                    var rightNeighborDepth = GetDepth(x + 1, zoneType);
                    var currentDepth             = GetDepth(x, zoneType);
                    if (currentDepth <= rightNeighborDepth) return currentDepth;
                    var leftNeighborDepth = GetDepth(x - 1, zoneType);
                    var farRightNeighborDepth = GetDepth(x + 2, zoneType);
                    if ((rightNeighborDepth != farRightNeighborDepth) & (rightNeighborDepth != 0)) return rightNeighborDepth;
                    if (leftNeighborDepth - currentDepth < currentDepth - rightNeighborDepth) return math.min(math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    if (GetDepth(x - 2, zoneType) != leftNeighborDepth) return math.min(math.max(leftNeighborDepth, rightNeighborDepth), currentDepth);
                    return rightNeighborDepth;
                }

                private int GetDepth(int x, ZoneType zoneType) {
                    if (x < 0) {
                        x += m_LeftBlockData.m_Size.x;
                        if (x < 0) return 0;
                        if ((m_BuildOrderData.m_Order < m_LeftBuildOrderData.m_Order) & (m_Flag == CellFlags.Blocked))
                            return GetDepth(m_BlockData, m_ValidAreaData, m_Cells, 0, m_Flag | CellFlags.Blocked,
                                zoneType);
                        return GetDepth(m_LeftBlockData, m_LeftValidAreaData, m_LeftCells, x,
                            m_Flag | CellFlags.Blocked, zoneType);
                    } else {
                        if (x < m_BlockData.m_Size.x)
                            return GetDepth(m_BlockData, m_ValidAreaData, m_Cells, x, m_Flag | CellFlags.Blocked,
                                zoneType);
                        x -= m_BlockData.m_Size.x;
                        if (x >= m_RightBlockData.m_Size.x) return 0;
                        if ((m_BuildOrderData.m_Order < m_RightBuildOrderData.m_Order) & (m_Flag == CellFlags.Blocked))
                            return GetDepth(m_BlockData, m_ValidAreaData, m_Cells, m_BlockData.m_Size.x - 1,
                                m_Flag | CellFlags.Blocked, zoneType);
                        return GetDepth(m_RightBlockData, m_RightValidAreaData, m_RightCells, x,
                            m_Flag | CellFlags.Blocked, zoneType);
                    }
                }

                private int GetDepth(Block blockData, ValidArea validAreaData, DynamicBuffer<Cell> cells, int x,
                    CellFlags flags, ZoneType zoneType) {
                    var num = validAreaData.m_Area.z;
                    var num2 = x;
                    if (m_Flag == CellFlags.Occupied)
                        while (num < validAreaData.m_Area.w && (cells[num2].m_State & flags) == CellFlags.None) {
                            if (!cells[num2].m_Zone.Equals(zoneType)) break;
                            num2 += blockData.m_Size.x;
                            num++;
                        }
                    else
                        while (num < validAreaData.m_Area.w && (cells[num2].m_State & flags) == CellFlags.None) {
                            num2 += blockData.m_Size.x;
                            num++;
                        }

                    return num;
                }

                private void ReduceDepth(int x, int newDepth) {
                    var cellFlags = m_Flag | CellFlags.Blocked;
                    var num = m_BlockData.m_Size.x * newDepth + x;
                    for (var i = newDepth; i < m_ValidAreaData.m_Area.w; i++) {
                        var cell = m_Cells[num];
                        if ((cell.m_State & cellFlags) != CellFlags.None) return;
                        cell.m_State |= m_Flag;
                        m_Cells[num] = cell;
                        num += m_BlockData.m_Size.x;
                    }
                }

                public Entity m_BlockEntity;

                public Entity m_LeftNeightbor;

                public Entity m_RightNeightbor;

                public CellFlags m_Flag;

                /// <summary>[CUSTOM] When true, skip depth reduction to protect narrow parcels.</summary>
                public bool m_IsNarrowParcel;

                public ZonePrefabs m_ZonePrefabs;

                public ComponentLookup<Block> m_BlockDataFromEntity;

                public ComponentLookup<ValidArea> m_ValidAreaDataFromEntity;

                public ComponentLookup<BuildOrder> m_BuildOrderDataFromEntity;

                public ComponentLookup<ZoneData> m_ZoneData;

                public BufferLookup<Cell> m_CellsFromEntity;

                private Block m_BlockData;

                private Block m_LeftBlockData;

                private Block m_RightBlockData;

                private ValidArea m_ValidAreaData;

                private ValidArea m_LeftValidAreaData;

                private ValidArea m_RightValidAreaData;

                private BuildOrder m_BuildOrderData;

                private BuildOrder m_LeftBuildOrderData;

                private BuildOrder m_RightBuildOrderData;

                private DynamicBuffer<Cell> m_Cells;

                private DynamicBuffer<Cell> m_LeftCells;

                private DynamicBuffer<Cell> m_RightCells;
            }

            /// <summary>
            /// Modified vanilla OverlapIterator. [CUSTOM] Added parcel detection and zone clearing in CheckOverlapZ2().
            /// </summary>
            private struct OverlapIterator {
                /// <summary>
                /// Iterates over the overlap between the current block and another block.
                /// </summary>
                /// <param name="blockEntity2">The entity of the other block.</param>
                public void Iterate(Entity blockEntity2) {
                    m_BlockData2 = m_BlockDataFromEntity[blockEntity2];
                    m_ValidAreaData2 = m_ValidAreaDataFromEntity[blockEntity2];
                    m_BuildOrderData2 = m_BuildOrderDataFromEntity[blockEntity2];
                    m_Cells2 = m_CellsFromEntity[blockEntity2];
                    if (m_ValidAreaData2.m_Area.y <= m_ValidAreaData2.m_Area.x) return;

                    // [CUSTOM] Check if the other block is a parcel
                    m_OtherBlockIsParcel = m_ParcelOwnerData.HasComponent(blockEntity2);

                    if (ZoneUtils.CanShareCells(m_BlockData, m_BlockData2, m_BuildOrderData, m_BuildOrderData2)) {
                        if (!m_CheckSharing) return;
                        m_CheckDepth = false;
                    } else {
                        if (m_CheckSharing) return;
                        m_CheckDepth = math.dot(m_BlockData.m_Direction, m_BlockData2.m_Direction) < -0.6946584f;
                    }

                    var quad = ZoneUtils.CalculateCorners(m_BlockData2, m_ValidAreaData2);
                    CheckOverlapX1(m_Bounds, MathUtils.Bounds(quad), m_Quad, quad, m_ValidAreaData.m_Area,
                        m_ValidAreaData2.m_Area);
                }

                /// <summary>
                /// Recursively checks for overlaps along the X-axis (first pass).
                /// </summary>
                private void CheckOverlapX1(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Quad2 quad2, int4 xxzz1,
                    int4 xxzz2) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = (xxzz1.x + xxzz1.y) >> 1;
                        int2.x = @int.y;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad3.b = math.lerp(quad1.a, quad1.b, num);
                        quad3.c = math.lerp(quad1.d, quad1.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds3, bounds2))
                            CheckOverlapZ1(bounds3, bounds2, quad3, quad2, @int, xxzz2);
                        if (MathUtils.Intersect(bounds4, bounds2)) {
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
                private void CheckOverlapZ1(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Quad2 quad2, int4 xxzz1,
                    int4 xxzz2) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = (xxzz1.z + xxzz1.w) >> 1;
                        int2.z = @int.w;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad3.d = math.lerp(quad1.a, quad1.d, num);
                        quad3.c = math.lerp(quad1.b, quad1.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds3, bounds2))
                            CheckOverlapX2(bounds3, bounds2, quad3, quad2, @int, xxzz2);
                        if (MathUtils.Intersect(bounds4, bounds2)) {
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
                private void CheckOverlapX2(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Quad2 quad2, int4 xxzz1,
                    int4 xxzz2) {
                    if (xxzz2.y - xxzz2.x >= 2) {
                        var @int = xxzz2;
                        var int2 = xxzz2;
                        @int.y = (xxzz2.x + xxzz2.y) >> 1;
                        int2.x = @int.y;
                        var quad3 = quad2;
                        var quad4 = quad2;
                        var num = (float)(@int.y - xxzz2.x) / (float)(xxzz2.y - xxzz2.x);
                        quad3.b = math.lerp(quad2.a, quad2.b, num);
                        quad3.c = math.lerp(quad2.d, quad2.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds1, bounds3))
                            CheckOverlapZ2(bounds1, bounds3, quad1, quad3, xxzz1, @int);
                        if (MathUtils.Intersect(bounds1, bounds4)) {
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
                private void CheckOverlapZ2(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Quad2 quad2, int4 xxzz1,
                    int4 xxzz2) {
                    if (xxzz2.w - xxzz2.z >= 2) {
                        var @int = xxzz2;
                        var int2 = xxzz2;
                        @int.w = (xxzz2.z + xxzz2.w) >> 1;
                        int2.z = @int.w;
                        var quad3 = quad2;
                        var quad4 = quad2;
                        var num = (float)(@int.w - xxzz2.z) / (float)(xxzz2.w - xxzz2.z);
                        quad3.d = math.lerp(quad2.a, quad2.d, num);
                        quad3.c = math.lerp(quad2.b, quad2.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds1, bounds3))
                            CheckOverlapX1(bounds1, bounds3, quad1, quad3, xxzz1, @int);
                        if (MathUtils.Intersect(bounds1, bounds4)) {
                            CheckOverlapX1(bounds1, bounds4, quad1, quad4, xxzz1, int2);
                            return;
                        }
                    } else {
                        if (math.any(xxzz1.yw - xxzz1.xz >= 2) | math.any(xxzz2.yw - xxzz2.xz >= 2)) {
                            CheckOverlapX1(bounds1, bounds2, quad1, quad2, xxzz1, xxzz2);
                            return;
                        }

                        var num2 = xxzz1.z * m_BlockData.m_Size.x + xxzz1.x;
                        var num3 = xxzz2.z * m_BlockData2.m_Size.x + xxzz2.x;
                        var cell = m_Cells[num2];
                        var cell2 = m_Cells2[num3];
                        if (((cell.m_State | cell2.m_State) & CellFlags.Blocked) != CellFlags.None) return;
                        if (m_CheckSharing) {
                          if (math.lengthsq(MathUtils.Center(quad1) - MathUtils.Center(quad2)) < 16f) {
                                // [CUSTOM] When one block is a parcel, clear zone from the non-parcel block
                                if (m_CurBlockIsParcel || m_OtherBlockIsParcel) {
                                    if (m_CurBlockIsParcel) {
                                        cell2.m_Zone = ZoneType.None;
                                        cell2.m_State |= CellFlags.Redundant | CellFlags.Blocked;
                                        m_Cells2[num3] = cell2;
                                    } else {
                                        cell.m_Zone = ZoneType.None;
                                        cell.m_State |= CellFlags.Redundant | CellFlags.Blocked;
                                        m_Cells[num2] = cell;
                                    }
                                    return;
                                }
                                // [END CUSTOM]

                                // Original sharing logic for non-parcel blocks
                                if (CheckPriority(cell, cell2, xxzz1.z, xxzz2.z, m_BuildOrderData.m_Order,
                                        m_BuildOrderData2.m_Order) &&
                                    (cell2.m_State & CellFlags.Shared) == CellFlags.None) {
                                    cell.m_State |= CellFlags.Shared;
                                    cell.m_State = (cell.m_State & ~CellFlags.Overridden) |
                                                   (cell2.m_State & CellFlags.Overridden);
                                    cell.m_Zone = cell2.m_Zone;
                                }

                                if ((cell2.m_State & CellFlags.Roadside) != CellFlags.None && xxzz2.z == 0)
                                    cell.m_State |= ZoneUtils.GetRoadDirection(m_BlockData, m_BlockData2);
                                cell.m_State &= ~CellFlags.Occupied | (cell2.m_State & CellFlags.Occupied);
                                m_Cells[num2] = cell;
                                return;
                          }
                        } else if (CheckPriority(cell, cell2, xxzz1.z, xxzz2.z, m_BuildOrderData.m_Order,
                                       m_BuildOrderData2.m_Order)) {
                            quad1 = MathUtils.Expand(quad1, -0.01f);
                            quad2 = MathUtils.Expand(quad2, -0.01f);
                            if (MathUtils.Intersect(quad1, quad2)) {
                                cell.m_State = (cell.m_State & ~CellFlags.Shared) |
                                               (m_CheckBlocking ? CellFlags.Blocked : CellFlags.Redundant);
                                m_Cells[num2] = cell;
                                return;
                            }
                        } else if (math.lengthsq(MathUtils.Center(quad1) - MathUtils.Center(quad2)) < 64f &&
                                   (cell2.m_State & CellFlags.Roadside) != CellFlags.None && xxzz2.z == 0) {
                            cell.m_State |= ZoneUtils.GetRoadDirection(m_BlockData, m_BlockData2);
                            m_Cells[num2] = cell;
                        }
                    }
                }

                /// <summary>
                /// Determines which cell has priority in an overlap scenario.
                /// </summary>
                private bool CheckPriority(Cell cell1, Cell cell2, int depth1, int depth2, uint order1, uint order2) {
                    if ((cell2.m_State & CellFlags.Updating) == CellFlags.None)
                        return (cell2.m_State & CellFlags.Visible) > CellFlags.None;
                    if (m_CheckBlocking) return (cell1.m_State & ~cell2.m_State & CellFlags.Redundant) > CellFlags.None;
                    if (m_CheckDepth) {
                        if (cell1.m_Zone.Equals(ZoneType.None) != cell2.m_Zone.Equals(ZoneType.None))
                            return cell1.m_Zone.Equals(ZoneType.None);
                        if (cell1.m_Zone.Equals(ZoneType.None) &&
                            ((cell1.m_State | cell2.m_State) & CellFlags.Overridden) == CellFlags.None &&
                            math.max(0, depth1 - 1) != math.max(0, depth2 - 1)) return depth2 < depth1;
                    }

                    if (((cell1.m_State ^ cell2.m_State) & CellFlags.Visible) != CellFlags.None)
                        return (cell2.m_State & CellFlags.Visible) > CellFlags.None;
                    return order2 < order1;
                }

                public Entity m_BlockEntity;

                public Quad2 m_Quad;

                public Bounds2 m_Bounds;

                public Block m_BlockData;

                public ValidArea m_ValidAreaData;

                public BuildOrder m_BuildOrderData;

                public DynamicBuffer<Cell> m_Cells;

                public ComponentLookup<Block> m_BlockDataFromEntity;

                public ComponentLookup<ValidArea> m_ValidAreaDataFromEntity;

                public ComponentLookup<BuildOrder> m_BuildOrderDataFromEntity;

                public BufferLookup<Cell> m_CellsFromEntity;

                public bool m_CheckSharing;

                public bool m_CheckBlocking;

                public bool m_CheckDepth;

                private Block m_BlockData2;

                private ValidArea m_ValidAreaData2;

                private BuildOrder m_BuildOrderData2;

                private DynamicBuffer<Cell> m_Cells2;

                // --- [CUSTOM] Parcel detection fields ---
                public ComponentLookup<ParcelOwner> m_ParcelOwnerData;
                public bool m_CurBlockIsParcel;
                private bool m_OtherBlockIsParcel;
            }
        }
    }
}