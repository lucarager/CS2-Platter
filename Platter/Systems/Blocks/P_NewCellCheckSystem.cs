// <copyright file="P_NewCellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Areas;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Burst;
    using Unity.Jobs;
    using static Game.Zones.CellCheckHelpers;
    using static Game.Zones.CellOccupyJobs;
    using static Game.Zones.LotSizeJobs;
    using Block = Game.Zones.Block;
    using BlockOverlap = Game.Zones.CellCheckHelpers.BlockOverlap;
    using BuildOrder = Game.Zones.BuildOrder;
    using Elevation = Game.Objects.Elevation;
    using Node = Game.Areas.Node;
    using OverlapGroup = Game.Zones.CellCheckHelpers.OverlapGroup;
    using SearchSystem = Game.Objects.SearchSystem;
    using UpdateCollectSystem = Game.Areas.UpdateCollectSystem;

    #endregion

    /// <summary>
    ///     Cell Check System. Similar to vanilla's CellCheckSystem.
    ///     Runs after CellCheckSystem and undoes some vanilla behavior to blocks where our custom parcels are present.
    ///     After the vanilla system runs, our two types of Blocks have the following state:
    ///     "Wide" Parcels:
    ///     They will always take priority over vanilla cells and therefore should
    ///     have all correct flags set, except for the cells over their lot depth which will incorrectly be visible.
    ///     "Narrow" Parcels:
    ///     If placed in the wild, they will have all correct flags set. They will be 2 wide and 6 deep.
    ///     If placed overlapping vanilla grid, inside cells are correct, outside are shared. The underlying vanilla grid will
    ///     be shared inside, normal outside.
    ///     If placed overlaying vanilla grid at an offset, they are blocked.
    ///     Most importantly, the logic that "occpies" them if they are 1 wide is caused by the depth smoothing logic inside
    ///     the CellReduction struct within CellOverlapJobs.cs.
    ///     Note the difference between the Occupied and Blocked flags
    /// </summary>
    public partial class P_NewCellCheckSystem : PlatterGameSystemBase {
        private Game.Areas.SearchSystem          m_AreaSearchSystem;
        private UpdateCollectSystem              m_AreaUpdateCollectSystem;
        private EntityQuery                      m_DeletedBlocksQuery;
        private Game.Net.SearchSystem            m_NetSearchSystem;
        private Game.Net.UpdateCollectSystem     m_NetUpdateCollectSystem;
        private SearchSystem                     m_ObjectSearchSystem;
        private Game.Objects.UpdateCollectSystem m_ObjectUpdateCollectSystem;
        private Game.Zones.SearchSystem          m_ZoneSearchSystem;
        private ZoneSystem                       m_ZoneSystem;
        private Game.Zones.UpdateCollectSystem   m_ZoneUpdateCollectSystem;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            m_ObjectUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            m_NetUpdateCollectSystem    = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            m_AreaUpdateCollectSystem   = World.GetOrCreateSystemManaged<UpdateCollectSystem>();
            m_ZoneSearchSystem          = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_ObjectSearchSystem        = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ZoneSystem                = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_NetSearchSystem           = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_AreaSearchSystem          = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();

            m_DeletedBlocksQuery = SystemAPI.QueryBuilder().WithAll<Block, Deleted>().WithNone<Temp>().Build();
        }

        /// <inheritdoc />
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

            var allBlocksList     = new NativeList<SortedEntity>(Allocator.TempJob);
            var parcelBlocksList  = new NativeList<SortedEntity>(Allocator.TempJob);
            var blockOverlapQueue = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            var blockOverlapList  = new NativeList<BlockOverlap>(Allocator.TempJob);
            var overlapGroupsList = new NativeList<OverlapGroup>(Allocator.TempJob);
            var boundsQueue       = new NativeQueue<Bounds2>(Allocator.TempJob);
            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(allBlocksList));
            var allBlocks = allBlocksList.AsDeferredJobArray();

            // Filter blocks to only include those that are part of a parcel
            var filterBlocksJobHandle = new FilterBlocksToParcelsJob {
                m_InputBlocks = allBlocksList,
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_OutputBlocks = parcelBlocksList,
            }.Schedule(Dependency);
            var parcelBlocks = parcelBlocksList.AsDeferredJobArray();

            // First, we re-run the vanilla BlockCellsJob on our parcel blocks to sanitize the cell data and check for network or object conflicts
            var blockCellsJobHandle = new CellBlockJobs.BlockCellsJob {
                m_Blocks                    = parcelBlocks,
                m_BlockData                 = SystemAPI.GetComponentLookup<Block>(),
                m_NetSearchTree             = m_NetSearchSystem.GetNetSearchTree(true, out var netSearchJobHandle),
                m_AreaSearchTree            = m_AreaSearchSystem.GetSearchTree(true, out var areaSearchJobHandle),
                m_OwnerData                 = SystemAPI.GetComponentLookup<Owner>(),
                m_TransformData             = SystemAPI.GetComponentLookup<Transform>(),
                m_EdgeGeometryData          = SystemAPI.GetComponentLookup<EdgeGeometry>(),
                m_StartNodeGeometryData     = SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                m_EndNodeGeometryData       = SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                m_CompositionData           = SystemAPI.GetComponentLookup<Composition>(),
                m_PrefabRefData             = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_PrefabCompositionData     = SystemAPI.GetComponentLookup<NetCompositionData>(),
                m_PrefabRoadCompositionData = SystemAPI.GetComponentLookup<RoadComposition>(),
                m_PrefabAreaGeometryData    = SystemAPI.GetComponentLookup<AreaGeometryData>(),
                m_PrefabObjectGeometryData  = SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                m_NativeData                = SystemAPI.GetComponentLookup<Native>(),
                m_AreaNodes                 = SystemAPI.GetBufferLookup<Node>(),
                m_AreaTriangles             = SystemAPI.GetBufferLookup<Triangle>(),
                m_Cells                     = SystemAPI.GetBufferLookup<Cell>(),
                m_ValidAreaData             = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(parcelBlocksList, 1, JobHandle.CombineDependencies(filterBlocksJobHandle, netSearchJobHandle, areaSearchJobHandle));
            m_NetSearchSystem.AddNetSearchTreeReader(blockCellsJobHandle);
            m_AreaSearchSystem.AddSearchTreeReader(blockCellsJobHandle);

            // Then, we run our own modified BlockCellsJob to process the updated parcel blocks.
            // This will fix up the cell data for our custom parcels, which the vanilla job doesn't know how to handle.
            var processUpdatedBlocksJobHandle = new BlockCellsJob
                                                {
                                                    m_Blocks            = parcelBlocks,
                                                    m_BlockLookup       = SystemAPI.GetComponentLookup<Block>(),
                                                    m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                                                    m_ParcelDataLookup  = SystemAPI.GetComponentLookup<ParcelData>(),
                                                    m_PrefabRefLookup   = SystemAPI.GetComponentLookup<PrefabRef>(),
                                                    m_CellsLookup       = SystemAPI.GetBufferLookup<Cell>(),
                                                    m_ValidAreaLookup   = SystemAPI.GetComponentLookup<ValidArea>(),
                                                }.Schedule(parcelBlocksList, 1, blockCellsJobHandle);

            // We proceed with vanilla jobs...
            // Find Overlapping Blocks.
            var zoneSearchTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var findOverlappingBlocksJobHandle = new FindOverlappingBlocksJob
                                                 {
                                                     m_Blocks         = allBlocks,
                                                     m_SearchTree     = zoneSearchTree,
                                                     m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                                                     m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                                                     m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                                                     m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
                                                 }.Schedule(
                                                            allBlocksList,
                                                            1,
                                                            JobHandle.CombineDependencies(
                                                                                          processUpdatedBlocksJobHandle,
                                                                                          zoneSearchJobHandle));
            m_ZoneSearchSystem.AddSearchTreeReader(findOverlappingBlocksJobHandle);

            // We proceed with vanilla jobs...
            // Group Overlapping Blocks
            var groupOverlappingBlocksJobHandle = new GroupOverlappingBlocksJob
                                                  {
                                                      m_Blocks        = allBlocks,
                                                      m_OverlapQueue  = blockOverlapQueue,
                                                      m_BlockOverlaps = blockOverlapList,
                                                      m_OverlapGroups = overlapGroupsList,
                                                  }.Schedule(findOverlappingBlocksJobHandle);

            // We proceed with vanilla jobs...
            // Zone and Occupy Cells
            // todo check if we can run this on one-wides only
            var deletedBlocksList = m_DeletedBlocksQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var deletedBlocksJobHandle);
            var zoneAndOccupyCellsJobHandle = new ZoneAndOccupyCellsJob
                                              {
                                                  m_Blocks             = allBlocks,
                                                  m_DeletedBlockChunks = deletedBlocksList,
                                                  m_ZonePrefabs        = m_ZoneSystem.GetPrefabs(),
                                                  m_EntityType         = SystemAPI.GetEntityTypeHandle(),
                                                  m_BlockData          = SystemAPI.GetComponentLookup<Block>(),
                                                  m_ValidAreaData      = SystemAPI.GetComponentLookup<ValidArea>(),
                                                  m_ObjectSearchTree =
                                                      m_ObjectSearchSystem.GetStaticSearchTree(true, out var objectSearchJobHandle),
                                                  m_TransformData            = SystemAPI.GetComponentLookup<Transform>(),
                                                  m_ElevationData            = SystemAPI.GetComponentLookup<Elevation>(),
                                                  m_PrefabRefData            = SystemAPI.GetComponentLookup<PrefabRef>(),
                                                  m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                                                  m_PrefabSpawnableBuildingData =
                                                      SystemAPI.GetComponentLookup<SpawnableBuildingData>(),
                                                  m_PrefabSignatureBuildingData =
                                                      SystemAPI.GetComponentLookup<SignatureBuildingData>(),
                                                  m_PrefabPlaceholderBuildingData =
                                                      SystemAPI.GetComponentLookup<PlaceholderBuildingData>(),
                                                  m_PrefabZoneData = SystemAPI.GetComponentLookup<ZoneData>(),
                                                  m_PrefabData     = SystemAPI.GetComponentLookup<PrefabData>(),
                                                  m_Cells          = SystemAPI.GetBufferLookup<Cell>(),
                                              }.Schedule(
                                                         allBlocksList,
                                                         1,
                                                         JobHandle.CombineDependencies(
                                                                                       processUpdatedBlocksJobHandle,
                                                                                       objectSearchJobHandle,
                                                                                       deletedBlocksJobHandle));
            m_ObjectSearchSystem.AddStaticSearchTreeReader(zoneAndOccupyCellsJobHandle);

            // We run our own custom job CustomCellsJob to process overlapping blocks. This job will generally prioritize parcel blocks
            // and ensure vanilla blocks are blocked rather than parcel ones.
            var checkBlockOverlapJobHandle = new CustomCellsJob
                                             {
                                                 m_BlockOverlaps     = blockOverlapList.AsDeferredJobArray(),
                                                 m_OverlapGroups     = overlapGroupsList.AsDeferredJobArray(),
                                                 m_BlockLookup       = SystemAPI.GetComponentLookup<Block>(),
                                                 m_CellLookup        = SystemAPI.GetBufferLookup<Cell>(),
                                                 m_ValidAreaLookup   = SystemAPI.GetComponentLookup<ValidArea>(),
                                                 m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                                                 m_ParcelDataLookup  = SystemAPI.GetComponentLookup<ParcelData>(),
                                                 m_PrefabRefLookup   = SystemAPI.GetComponentLookup<PrefabRef>(),
                                             }.Schedule(
                                                        overlapGroupsList,
                                                        1,
                                                        JobHandle.CombineDependencies(groupOverlappingBlocksJobHandle, zoneAndOccupyCellsJobHandle));

            // Proceed with vanilla jobs...
            // Update Blocks
            var updateBlocksJobHandle = new UpdateBlocksJob
                                        {
                                            m_Blocks    = allBlocks,
                                            m_BlockData = SystemAPI.GetComponentLookup<Block>(),
                                            m_Cells     = SystemAPI.GetBufferLookup<Cell>(),
                                        }.Schedule(allBlocksList, 1, checkBlockOverlapJobHandle);

            // Proceed with vanilla jobs...
            // Update Lot Size
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var updateLotSizeJobHandle = new UpdateLotSizeJob
                                         {
                                             m_Blocks         = allBlocks,
                                             m_ZonePrefabs    = m_ZoneSystem.GetPrefabs(),
                                             m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                                             m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                                             m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                                             m_UpdatedData    = SystemAPI.GetComponentLookup<Updated>(),
                                             m_ZoneData       = SystemAPI.GetComponentLookup<ZoneData>(),
                                             m_Cells          = SystemAPI.GetBufferLookup<Cell>(),
                                             m_SearchTree     = zoneSearchTree,
                                             m_VacantLots     = SystemAPI.GetBufferLookup<VacantLot>(),
                                             m_CommandBuffer  = ecb.AsParallelWriter(),
                                             m_BoundsQueue    = boundsQueue.AsParallelWriter(),
                                         }.Schedule(allBlocksList, 1, updateBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(updateLotSizeJobHandle);
            m_ZoneSystem.AddPrefabsReader(updateLotSizeJobHandle);

            // Proceed with vanilla jobs...
            // Update Bounds
            var updateBoundsJobHandle = new UpdateBoundsJob
                                        {
                                            m_BoundsList  = m_ZoneUpdateCollectSystem.GetUpdatedBounds(false, out var zoneUpdateJobHandle),
                                            m_BoundsQueue = boundsQueue,
                                        }.Schedule(JobHandle.CombineDependencies(updateLotSizeJobHandle, zoneUpdateJobHandle));
            m_ZoneUpdateCollectSystem.AddBoundsWriter(updateBoundsJobHandle);

            parcelBlocksList.Dispose(updateLotSizeJobHandle);
            parcelBlocks.Dispose(updateLotSizeJobHandle);
            allBlocksList.Dispose(updateLotSizeJobHandle);
            allBlocks.Dispose(updateLotSizeJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(checkBlockOverlapJobHandle);
            overlapGroupsList.Dispose(checkBlockOverlapJobHandle);
            boundsQueue.Dispose(updateBoundsJobHandle);
            deletedBlocksList.Dispose(zoneAndOccupyCellsJobHandle);

            Dependency = updateLotSizeJobHandle;
            Dependency.Complete();
            ecb.Playback(EntityManager);
            ecb.Dispose();
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
                var findUpdatedBlocksJob_Zones = new FindUpdatedBlocksSingleIterationJob
                                                 {
                                                     m_Bounds      = updatedBounds.AsDeferredJobArray(),
                                                     m_SearchTree  = zoneSearchTree,
                                                     m_ResultQueue = zoneUpdateQueue.AsParallelWriter(),
                                                 }.Schedule(
                                                            updatedBounds,
                                                            1,
                                                            JobHandle.CombineDependencies(
                                                                                          updatedBoundsJobHandle,
                                                                                          zoneSearchJobHandle));
                m_ZoneUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Zones);
                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Zones);
            }

            if (m_ObjectUpdateCollectSystem.isUpdated) {
                var updatedBounds = m_ObjectUpdateCollectSystem.GetUpdatedBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Objects = new FindUpdatedBlocksDoubleIterationJob
                                                   {
                                                       m_Bounds      = updatedBounds.AsDeferredJobArray(),
                                                       m_SearchTree  = zoneSearchTree,
                                                       m_ResultQueue = objectUpdateQueue.AsParallelWriter(),
                                                   }.Schedule(
                                                              updatedBounds,
                                                              1,
                                                              JobHandle.CombineDependencies(
                                                                                            updatedBoundsJobHandle,
                                                                                            zoneSearchJobHandle));
                m_ObjectUpdateCollectSystem.AddBoundsReader(findUpdatedBlocksJob_Objects);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Objects);
            }

            if (m_NetUpdateCollectSystem.netsUpdated) {
                var updatedNetBounds = m_NetUpdateCollectSystem.GetUpdatedNetBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Nets = new FindUpdatedBlocksDoubleIterationJob
                                                {
                                                    m_Bounds      = updatedNetBounds.AsDeferredJobArray(),
                                                    m_SearchTree  = zoneSearchTree,
                                                    m_ResultQueue = netUpdateQueue.AsParallelWriter(),
                                                }.Schedule(
                                                           updatedNetBounds,
                                                           1,
                                                           JobHandle.CombineDependencies(
                                                                                         updatedBoundsJobHandle,
                                                                                         zoneSearchJobHandle));
                m_NetUpdateCollectSystem.AddNetBoundsReader(findUpdatedBlocksJob_Nets);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Nets);
            }

            var searchJobHandle = zoneSearchJobHandle;
            if (m_AreaUpdateCollectSystem.lotsUpdated) {
                var updatedLotBounds = m_AreaUpdateCollectSystem.GetUpdatedLotBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_Areas = new FindUpdatedBlocksDoubleIterationJob
                                                 {
                                                     m_Bounds      = updatedLotBounds.AsDeferredJobArray(),
                                                     m_SearchTree  = zoneSearchTree,
                                                     m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
                                                 }.Schedule(
                                                            updatedLotBounds,
                                                            1,
                                                            JobHandle.CombineDependencies(
                                                                                          updatedBoundsJobHandle,
                                                                                          zoneSearchJobHandle));
                m_AreaUpdateCollectSystem.AddLotBoundsReader(findUpdatedBlocksJob_Areas);

                jobHandle       = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_Areas);
                searchJobHandle = findUpdatedBlocksJob_Areas;
            }

            if (m_AreaUpdateCollectSystem.mapTilesUpdated) {
                var updatedMapTileBounds = m_AreaUpdateCollectSystem.GetUpdatedMapTileBounds(out var updatedBoundsJobHandle);
                var findUpdatedBlocksJob_MapTiles = new FindUpdatedBlocksDoubleIterationJob
                                                    {
                                                        m_Bounds      = updatedMapTileBounds.AsDeferredJobArray(),
                                                        m_SearchTree  = zoneSearchTree,
                                                        m_ResultQueue = areaUpdateQueue.AsParallelWriter(),
                                                    }.Schedule(
                                                               updatedMapTileBounds,
                                                               1,
                                                               JobHandle.CombineDependencies(
                                                                                             updatedBoundsJobHandle,
                                                                                             searchJobHandle));
                m_AreaUpdateCollectSystem.AddMapTileBoundsReader(findUpdatedBlocksJob_MapTiles);

                jobHandle = JobHandle.CombineDependencies(jobHandle, findUpdatedBlocksJob_MapTiles);
            }

            var collectBlocksJobHandle = new CollectBlocksJob
                                         {
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

        /// <summary>
        ///     Filters the input list of blocks to only include those that are part of a parcel.
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
