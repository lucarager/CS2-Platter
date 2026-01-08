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
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
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
    using static Unity.Collections.AllocatorManager;
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
    /// Note the difference between the Occupied and Blocked flags:
    /// - 
    ///
    /// </summary>
    public partial class P_NewCellCheckSystem : PlatterGameSystemBase {
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

            m_Log.Debug("Running P_NewCellCheckSystem");

            var updatedBlocksList  = new NativeList<SortedEntity>(Allocator.TempJob);
            var blockOverlapQueue  = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            var blockOverlapList   = new NativeList<BlockOverlap>(Allocator.TempJob);
            var overlapGroupsList  = new NativeList<OverlapGroup>(Allocator.TempJob);
            var boundsQueue        = new NativeQueue<Bounds2>(Allocator.TempJob);
            var filteredBlocksList = new NativeList<SortedEntity>(Allocator.TempJob);
            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updatedBlocksList));

            // Filter blocks to only include those that are part of a parcel
            var filterBlocksJobHandle = new FilterBlocksToParcelsJob {
                m_InputBlocks       = updatedBlocksList,
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(true),
                m_OutputBlocks      = filteredBlocksList,
            }.Schedule(Dependency);
            var filteredBlocks = filteredBlocksList.AsDeferredJobArray();

            // Process Updated Blocks. This is one of the jobs we maintain ourselves.
            var processUpdatedBlocksJobHandle = new BlockCellsJob {
                m_Blocks                      = filteredBlocks,
                m_BlockLookup                 = SystemAPI.GetComponentLookup<Block>(),
                m_ParcelOwnerLookup           = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_ParcelDataLookup            = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabRefLookup             = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_CellsLookup                 = SystemAPI.GetBufferLookup<Cell>(),
                m_ValidAreaLookup             = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(filteredBlocksList, 1, filterBlocksJobHandle);

            // Find Overlapping Blocks
            var zoneSearchTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var findOverlappingBlocksJobHandle = new CellCheckHelpers.FindOverlappingBlocksJob {
                m_Blocks         = filteredBlocks,
                m_SearchTree     = zoneSearchTree,
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(true),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(true),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(true),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(filteredBlocksList, 1, JobHandle.CombineDependencies(processUpdatedBlocksJobHandle, zoneSearchJobHandle));
            m_ZoneSearchSystem.AddSearchTreeReader(findOverlappingBlocksJobHandle);

            // Group Overlapping Blocks
            var groupOverlappingBlocksJobHandle = new CellCheckHelpers.GroupOverlappingBlocksJob {
                m_Blocks        = filteredBlocks,
                m_OverlapQueue  = blockOverlapQueue,
                m_BlockOverlaps = blockOverlapList,
                m_OverlapGroups = overlapGroupsList,
            }.Schedule(findOverlappingBlocksJobHandle);

            // Zone and Occupy Cells
            // todo check if we can run this on one-wides only
            var deletedBlocksList = m_DeletedBlocksQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var deletedBlocksJobHandle);
            var zoneAndOccupyCellsJobHandle = new CellOccupyJobs.ZoneAndOccupyCellsJob {
                m_Blocks                        = filteredBlocks,
                m_DeletedBlockChunks            = deletedBlocksList,
                m_ZonePrefabs                   = m_ZoneSystem.GetPrefabs(),
                m_EntityType                    = SystemAPI.GetEntityTypeHandle(),
                m_BlockData                     = SystemAPI.GetComponentLookup<Block>(true),
                m_ValidAreaData                 = SystemAPI.GetComponentLookup<ValidArea>(true),
                m_ObjectSearchTree              = m_ObjectSearchSystem.GetStaticSearchTree(true, out var objectSearchJobHandle),
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
            m_ObjectSearchSystem.AddStaticSearchTreeReader(zoneAndOccupyCellsJobHandle);

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
            m_ZoneSearchSystem.AddSearchTreeReader(updateLotSizeJobHandle);
            m_ZoneSystem.AddPrefabsReader(updateLotSizeJobHandle);
            m_ModificationBarrier5.AddJobHandleForProducer(updateLotSizeJobHandle);

            // Update Bounds
            var updateBoundsJobHandle = new LotSizeJobs.UpdateBoundsJob {
                m_BoundsList  = m_ZoneUpdateCollectSystem.GetUpdatedBounds(false, out var zoneUpdateJobHandle),
                m_BoundsQueue = boundsQueue,
            }.Schedule(JobHandle.CombineDependencies(updateLotSizeJobHandle, zoneUpdateJobHandle));
            m_ZoneUpdateCollectSystem.AddBoundsWriter(updateBoundsJobHandle);

            updatedBlocksList.Dispose(filterBlocksJobHandle);
            filteredBlocksList.Dispose(updateLotSizeJobHandle);
            filteredBlocks.Dispose(updateLotSizeJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(checkBlockOverlapJobHandle);
            overlapGroupsList.Dispose(checkBlockOverlapJobHandle);
            boundsQueue.Dispose(updateBoundsJobHandle);
            deletedBlocksList.Dispose(zoneAndOccupyCellsJobHandle);

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

            var collectBlocksJobHandle = new CellCheckHelpers.CollectBlocksJob {
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
    }
}