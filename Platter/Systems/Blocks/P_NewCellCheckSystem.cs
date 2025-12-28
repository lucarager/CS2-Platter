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
    using Game.Zones;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
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
    /// 2. The cell reduction logic which causes 1-wide parcels to be occupied incorrectly.
    /// 
    /// </summary>
    public partial class P_NewCellCheckSystem : PlatterGameSystemBase {
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
            var updatedBlocks = updatedBlocksList.AsDeferredJobArray();

            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updatedBlocksList));

            // [All] Process Updated Blocks, resetting parcel cell data and re-evaluating geometry collisions
            var processUpdatedBlocksJobHandle = new ProcessUpdatedBlocksJob {
                m_Blocks                      = updatedBlocks,
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
                m_ValidAreaLookup               = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(updatedBlocksList, 1, JobHandle.CombineDependencies(Dependency, netSearchJobHandle, areaSearchJobHandle));
            m_NetSearchSystem.AddNetSearchTreeReader(processUpdatedBlocksJobHandle);
            m_AreaSearchSystem.AddSearchTreeReader(processUpdatedBlocksJobHandle);

            //// [All] Create a filtered list of Narrow parcel blocks
            //var updatedNarrowParcelBlocksList = new NativeList<SortedEntity>(Allocator.TempJob);
            //var filterJobHandle = new FilterByOneWideParcelsJob {
            //    m_InputBlocks       = updatedBlocksArray,
            //    m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(true),
            //    m_ParcelDataLookup  = SystemAPI.GetComponentLookup<ParcelData>(true),
            //    m_PrefabRefLookup   = SystemAPI.GetComponentLookup<PrefabRef>(true),
            //    m_OutputBlocks      = updatedNarrowParcelBlocksList,
            //}.Schedule(processUpdatedBlocksJobHandle);
            //var updatedNarrowParcelBlocks = updatedNarrowParcelBlocksList.AsDeferredJobArray();

            // [] Create Overlap Queue
            var blockOverlapQueue = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            var zoneSearchTree    = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            var findOverlappingBlocksJobHandle = new CellCheckHelpers.FindOverlappingBlocksJob {
                m_Blocks         = updatedBlocks,
                m_SearchTree     = zoneSearchTree,
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(updatedBlocksList, 1, JobHandle.CombineDependencies(processUpdatedBlocksJobHandle, zoneSearchJobHandle));
            m_ZoneSearchSystem.AddSearchTreeReader(findOverlappingBlocksJobHandle);

            // [] Group Overlapping Blocks
            var blockOverlapList   = new NativeList<BlockOverlap>(Allocator.TempJob);
            var overlapGroupsList  = new NativeList<OverlapGroup>(Allocator.TempJob);
            var groupOverlappingBlocksJobHandle = new CellCheckHelpers.GroupOverlappingBlocksJob {
                m_Blocks        = updatedBlocks,
                m_OverlapQueue  = blockOverlapQueue,
                m_BlockOverlaps = blockOverlapList,
                m_OverlapGroups = overlapGroupsList,
            }.Schedule(findOverlappingBlocksJobHandle);
            blockOverlapQueue.Dispose(groupOverlappingBlocksJobHandle);

            // [] Overlap Iterator
            // todo check that wide parcels really dont need this job to run
            var processOverlapsJobHandle = new ProcessOverlapsJob {
                m_OverlapGroups     = overlapGroupsList.AsDeferredJobArray(),
                m_ParcelOwnerLookup = SystemAPI.GetComponentLookup<ParcelOwner>(),
                m_ParcelDataLookup  = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabRefLookup   = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_BlockLookup       = SystemAPI.GetComponentLookup<Block>(),
                m_BuildOrderLookup  = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_ValidAreaLookup   = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BlockOverlapArray = blockOverlapList.AsDeferredJobArray(),
                m_CellLookup        = SystemAPI.GetBufferLookup<Cell>(),
            }.Schedule(overlapGroupsList, 1, groupOverlappingBlocksJobHandle);
            blockOverlapList.Dispose(processOverlapsJobHandle);
            overlapGroupsList.Dispose(processOverlapsJobHandle);

            // [] Set "Visible" Flag
            var updateBlocksJobHandle = new CellCheckHelpers.UpdateBlocksJob {
                m_Blocks    = updatedBlocks,
                m_BlockData = SystemAPI.GetComponentLookup<Block>(true),
                m_Cells     = SystemAPI.GetBufferLookup<Cell>(false),
            }.Schedule(updatedBlocksList, 1, processOverlapsJobHandle);

            // [All] Update Lot Sizes
            // todo this needs to be a custom job to avoid removing Narrows
            var boundsQueue = new NativeQueue<Bounds2>(Allocator.TempJob);
            var ecb         = new EntityCommandBuffer(Allocator.TempJob);
            var updateLotSizeJobHandle = new LotSizeJobs.UpdateLotSizeJob {
                m_Blocks = updatedBlocks,
                m_ZonePrefabs = m_ZoneSystem.GetPrefabs(),
                m_BlockData = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<BuildOrder>(),
                m_UpdatedData = SystemAPI.GetComponentLookup<Updated>(),
                m_ZoneData = SystemAPI.GetComponentLookup<ZoneData>(),
                m_Cells = SystemAPI.GetBufferLookup<Cell>(),
                m_SearchTree = zoneSearchTree,
                m_VacantLots = SystemAPI.GetBufferLookup<VacantLot>(),
                m_CommandBuffer = ecb.AsParallelWriter(),
                m_BoundsQueue = boundsQueue.AsParallelWriter(),
            }.Schedule(updatedBlocksList, 1, updateBlocksJobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(updateLotSizeJobHandle);
            m_ZoneSystem.AddPrefabsReader(updateLotSizeJobHandle);
            updatedBlocksList.Dispose(updateLotSizeJobHandle);
            updatedBlocks.Dispose(updateLotSizeJobHandle);
            //updatedNarrowParcelBlocksList.Dispose(updateLotSizeJobHandle);
            //updatedNarrowParcelBlocks.Dispose(updateLotSizeJobHandle);

            // [All] Update Bounds
            var updateBoundsJobHandle = new LotSizeJobs.UpdateBoundsJob {
                m_BoundsList = m_ZoneUpdateCollectSystem.GetUpdatedBounds(false, out var updatedBoundsJobHandle),
                m_BoundsQueue = boundsQueue,
            }.Schedule(JobHandle.CombineDependencies(updateLotSizeJobHandle, updatedBoundsJobHandle));
            m_ZoneUpdateCollectSystem.AddBoundsWriter(updateBoundsJobHandle);
            boundsQueue.Dispose(updateBoundsJobHandle);

            // All done
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
        public struct ProcessOverlapsJob : IJobParallelForDefer {
            [ReadOnly] public required NativeArray<OverlapGroup> m_OverlapGroups;
            [ReadOnly] public required ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData> m_ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<Block> m_BlockLookup;
            [ReadOnly] public required ComponentLookup<BuildOrder> m_BuildOrderLookup;
            [NativeDisableParallelForRestriction] public required ComponentLookup<ValidArea> m_ValidAreaLookup;
            [NativeDisableParallelForRestriction] public required NativeArray<BlockOverlap> m_BlockOverlapArray;
            [NativeDisableParallelForRestriction] public required BufferLookup<Cell> m_CellLookup;

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
                    var blockOverlap = m_BlockOverlapArray[n];
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
                private ComponentLookup<BuildOrder> m_BuildOrderLookup;
                private BufferLookup<Cell> m_CellBufferLookup;
                private Entity m_CurBlockEntity;
                private ValidArea m_CurValidArea;
                private Bounds2 m_CurBounds;
                private Block m_CurBlock;
                private BuildOrder m_CurBuildOrder;
                private DynamicBuffer<Cell> m_CurCellBuffer;
                private Quad2 m_CurCorners;
                private bool m_CurBlockIsParcel;
                private int2 m_CurBlockParcelBounds;
                private Entity m_OtherBlockEntity;
                private Block m_OtherBlock;
                private ValidArea m_OtherValidArea;
                private BuildOrder m_OtherBuildOrder;
                private DynamicBuffer<Cell> m_OtherCellBuffer;
                private bool m_OtherBlockIsParcel;
                private int2 m_OtherBlockParcelBounds;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerLookup, ComponentLookup<Block> blockLookup,
                                       ComponentLookup<ValidArea> validAreaLookup, ComponentLookup<BuildOrder> buildOrderLookup,
                                       BufferLookup<Cell> cellsBufferLookup, ComponentLookup<ParcelData> parcelDataLookup,
                                       ComponentLookup<PrefabRef> prefabRefLookup) : this() {
                    m_ParcelOwnerLookup = parcelOwnerLookup;
                    m_BlockLookup = blockLookup;
                    m_ValidAreaLookup = validAreaLookup;
                    m_BuildOrderLookup = buildOrderLookup;
                    m_CellBufferLookup = cellsBufferLookup;
                    m_ParcelDataLookup = parcelDataLookup;
                    m_PrefabRefLookup = prefabRefLookup;
                }

                public void SetEntity(Entity curBlockEntity) {
                    var curValidArea = m_ValidAreaLookup[curBlockEntity];
                    var curBlock = m_BlockLookup[curBlockEntity];
                    var curCorners = ZoneUtils.CalculateCorners(curBlock, curValidArea);

                    // Set data
                    m_CurBlockEntity = curBlockEntity;
                    m_CurValidArea = curValidArea;
                    m_CurCorners = curCorners;
                    m_CurBounds = MathUtils.Bounds(curCorners);
                    m_CurBlock = curBlock;
                    m_CurBuildOrder = m_BuildOrderLookup[curBlockEntity];
                    m_CurCellBuffer = m_CellBufferLookup[curBlockEntity];
                    m_CurBlockIsParcel = m_ParcelOwnerLookup.HasComponent(curBlockEntity);
                    m_CurBlockParcelBounds = default;

                    if (m_CurBlockIsParcel) {
                        var prefab = m_PrefabRefLookup[curBlockEntity];
                        var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                        m_CurBlockParcelBounds = parcelData.m_LotSize;
                    }
                }

                public void Iterate(Entity otherBlock) {
                    m_OtherBlockEntity = otherBlock;
                    m_OtherBlock = m_BlockLookup[otherBlock];
                    m_OtherValidArea = m_ValidAreaLookup[otherBlock];
                    m_OtherBuildOrder = m_BuildOrderLookup[otherBlock];
                    m_OtherCellBuffer = m_CellBufferLookup[otherBlock];
                    m_OtherBlockIsParcel = m_ParcelOwnerLookup.HasComponent(otherBlock);
                    m_OtherBlockParcelBounds = default;

                    if (m_OtherBlockIsParcel) {
                        var prefab = m_PrefabRefLookup[otherBlock];
                        var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                        m_OtherBlockParcelBounds = parcelData.m_LotSize;
                    }

                    // Exit early if neither block is a parcel
                    if (!m_CurBlockIsParcel && !m_OtherBlockIsParcel) {
                        return;
                    }

                    // Exit early if the block we are checking against has a valid area of width 0
                    if (m_OtherValidArea.m_Area.y <= m_OtherValidArea.m_Area.x) {
                        return;
                    }

                    // Exit early if the blocks cannot share cells
                    if (!ZoneUtils.CanShareCells(m_CurBlock, m_OtherBlock, m_CurBuildOrder, m_OtherBuildOrder)) {
                        // todo, this should be the place where we block the cells 
                        return;
                    }

                    var otherBlockCorners = ZoneUtils.CalculateCorners(m_OtherBlock, m_OtherValidArea);

                    // Recursively iterate over cells, so that we can unzone and block cells that would otherwise
                    // share state with parcel cells.
                    CheckOverlapX1(
                        m_CurBounds,
                        MathUtils.Bounds(otherBlockCorners),
                        m_CurCorners,
                        otherBlockCorners,
                        m_CurValidArea.m_Area,
                        m_OtherValidArea.m_Area);
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

                    // Base case: X-range is a single column
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

                    // Base case: other block's X-range is a single column
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

                        // lerp to get subdivided quads along Z
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
                    // math.any used to test both components. Use logical OR to be explicit.
                    if (math.any(validArea.yw - validArea.xz >= 2) || math.any(otherValidArea.yw - otherValidArea.xz >= 2)) {
                        CheckOverlapX1(blockBounds, otherBounds, blockCorners, otherCorners, validArea, otherValidArea);
                        return;
                    }

                    // Now both regions are single cells: compute flat buffer indices.
                    var curIndex = validArea.z * m_CurBlock.m_Size.x + validArea.x;
                    var otherIndex = otherValidArea.z * m_OtherBlock.m_Size.x + otherValidArea.x;

                    var curCell = m_CurCellBuffer[curIndex];
                    var otherCell = m_OtherCellBuffer[otherIndex];

                    // In sharing mode we only allow sharing when cell centers are very close.
                    if (!(math.lengthsq(MathUtils.Center(blockCorners) - MathUtils.Center(otherCorners)) < 16f)) {
                        return;
                    }

                    if (m_CurBlockIsParcel) {
                        otherCell.m_Zone = ZoneType.None;
                        otherCell.m_State = CellFlags.Redundant | CellFlags.Blocked;
                    } else if (m_OtherBlockIsParcel) {
                        curCell.m_Zone = ZoneType.None;
                        curCell.m_State = CellFlags.Redundant | CellFlags.Blocked;
                    }

                    m_CurCellBuffer[curIndex] = curCell;
                    m_OtherCellBuffer[otherIndex] = otherCell;
                }
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct FilterByOneWideParcelsJob : IJob {
            [ReadOnly] public NativeArray<SortedEntity> m_InputBlocks;
            [ReadOnly] public ComponentLookup<ParcelOwner> m_ParcelOwnerLookup;
            [ReadOnly] public ComponentLookup<ParcelData> m_ParcelDataLookup;
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefLookup;
            public NativeList<SortedEntity> m_OutputBlocks;

            public void Execute() {
                for (var i = 0; i < m_InputBlocks.Length; i++) {
                    var entity = m_InputBlocks[i].m_Entity;
                    if (!m_ParcelOwnerLookup.TryGetComponent(entity, out var parcelOwner)) {
                        continue;
                    }

                    var prefab = m_PrefabRefLookup[parcelOwner.m_Owner];
                    var parcelData = m_ParcelDataLookup[prefab.m_Prefab];

                    if (parcelData.m_LotSize.x == 1) {
                        m_OutputBlocks.Add(m_InputBlocks[i]);
                    }
                }
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        public struct ProcessUpdatedBlocksJob : IJobParallelForDefer {
            [ReadOnly] public required NativeArray<CellCheckHelpers.SortedEntity>       m_Blocks;
            [ReadOnly] public required ComponentLookup<Block>                           m_BlockLookup;
            [ReadOnly] public required ComponentLookup<ParcelOwner>                     m_ParcelOwnerLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>                      m_ParcelDataLookup;
            [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ>         m_NetSearchTree;
            [ReadOnly] public required NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> m_AreaSearchTree;
            [ReadOnly] public required ComponentLookup<Owner>                           m_OwnerLookup;
            [ReadOnly] public required ComponentLookup<Game.Objects.Transform>          m_TransformLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>                    m_EdgeGeometryLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry>               m_StartNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry>                 m_EndNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<Composition>                     m_CompositionLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>                       m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<NetCompositionData>              m_NetCompositionLookup;
            [ReadOnly] public required ComponentLookup<RoadComposition>                 m_PrefabRoadCompositionLookup;
            [ReadOnly] public required ComponentLookup<AreaGeometryData>                m_PrefabAreaGeometryLookup;
            [ReadOnly] public required ComponentLookup<ObjectGeometryData>              m_PrefabObjectGeometryLookup;
            [ReadOnly] public required ComponentLookup<Native>                          m_NativeLookup;
            [ReadOnly] public required BufferLookup<Game.Areas.Node>                    m_AreaNodesLookup;
            [ReadOnly] public required BufferLookup<Triangle>                           m_AreaTrianglesLookup;

            [NativeDisableParallelForRestriction]
            public required BufferLookup<Cell> m_CellsLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<ValidArea> m_ValidAreaLookup;

            public void Execute(int index) {
                var entity = m_Blocks[index].m_Entity;

                // Exit early if it's not a parcel
                if (!m_ParcelOwnerLookup.TryGetComponent(entity, out var parcelOwner)) {
                    return;
                }

                // Retrieve data
                var block      = m_BlockLookup[entity];
                var prefab     = m_PrefabRefLookup[parcelOwner.m_Owner];
                var parcelData = m_ParcelDataLookup[prefab.m_Prefab];
                var cellBuffer = m_CellsLookup[entity];
                var validArea  = new ValidArea() {
                    m_Area = new int4(0, parcelData.m_LotSize.x, 0, parcelData.m_LotSize.y),
                };
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);
                var bounds    = parcelGeo.Bounds;
                var corners   = ZoneUtils.CalculateCorners(block, validArea);

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
            private static void NormalizeWideParcelCells(Block block, ParcelData parcelData, DynamicBuffer<Cell> cells) {
                for (var row = 0; row < block.m_Size.y; row++)
                for (var col = 0; col < block.m_Size.x; col++) {
                    var isOutsideLot = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;

                    if (!isOutsideLot) {
                        continue;
                    }

                    var i    = row * block.m_Size.x + col;
                    var cell = cells[i];

                    cell.m_State = CellFlags.Blocked;
                    cell.m_Zone  = P_ZoneCacheSystem.UnzonedZoneType;

                    cells[i] = cell;
                }
            }

            /// <summary>
            /// Normalizes the cells of a 1-cell-wide parcel block.
            /// </summary>
            /// <param name="block"></param>
            /// <param name="parcelData"></param>
            /// <param name="cells"></param>
            private static void NormalizeNarrowParcelCells(Block block, ParcelData parcelData, DynamicBuffer<Cell> cells) {
                for (var row = 0; row < block.m_Size.y; row++)
                for (var col = 0; col < block.m_Size.x; col++) {
                    var isOutsideLot = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;
                    var i            = row * block.m_Size.x + col;
                    var cell         = cells[i];

                    if (isOutsideLot)  {
                        cell.m_State = CellFlags.Blocked;
                        cell.m_Zone  = P_ZoneCacheSystem.UnzonedZoneType;
                    } else {
                        // We are only re-evaluating "Occupied" flags.
                        cell.m_State &= ~CellFlags.Occupied;
                    }

                    cells[i] = cell;
                }
            }

            // Exact copy of CleanBlockedCells from vanilla CellCheckSystem.
            // Propagates blocked cell states and calculates the valid buildable area within a block.
            // This method performs two main operations:
            // 1. Forward propagation: Blocks cells that are below other blocked cells (top-down blocking)
            // 2. Road flag propagation: Sets RoadLeft/RoadRight flags on cells adjacent to blocked road cells
            private static void CleanBlockedCells(Block blockData, ref ValidArea validAreaData, DynamicBuffer<Cell> cells) {
                var validArea = default(ValidArea);
                validArea.m_Area.xz = blockData.m_Size;
                for (var i = validAreaData.m_Area.x; i < validAreaData.m_Area.y; i++) {
                    var cell = cells[i];
                    var cell2 = cells[blockData.m_Size.x + i];
                    if (((cell.m_State & CellFlags.Blocked) == CellFlags.None) & ((cell2.m_State & CellFlags.Blocked) > CellFlags.None)) {
                        cell.m_State |= CellFlags.Blocked;
                        cells[i] = cell;
                    }
                    var num = 0;
                    for (var j = validAreaData.m_Area.z + 1; j < validAreaData.m_Area.w; j++) {
                        var num2 = j * blockData.m_Size.x + i;
                        var cell3 = cells[num2];
                        if (((cell3.m_State & CellFlags.Blocked) == CellFlags.None) & ((cell.m_State & CellFlags.Blocked) > CellFlags.None)) {
                            cell3.m_State |= CellFlags.Blocked;
                            cells[num2] = cell3;
                        }
                        if ((cell3.m_State & CellFlags.Blocked) == CellFlags.None) {
                            num = j + 1;
                        }
                        cell = cell3;
                    }
                    if (num > validAreaData.m_Area.z) {
                        validArea.m_Area.xz = math.min(validArea.m_Area.xz, new int2(i, validAreaData.m_Area.z));
                        validArea.m_Area.yw = math.max(validArea.m_Area.yw, new int2(i + 1, num));
                    }
                }
                validAreaData = validArea;
                for (var k = validAreaData.m_Area.z; k < validAreaData.m_Area.w; k++) {
                    for (var l = validAreaData.m_Area.x; l < validAreaData.m_Area.y; l++) {
                        var num3 = k * blockData.m_Size.x + l;
                        var cell4 = cells[num3];
                        if ((cell4.m_State & (CellFlags.Blocked | CellFlags.RoadLeft)) == CellFlags.None && l > 0 && (cells[num3 - 1].m_State & (CellFlags.Blocked | CellFlags.RoadLeft)) == (CellFlags.Blocked | CellFlags.RoadLeft)) {
                            cell4.m_State |= CellFlags.RoadLeft;
                            cells[num3] = cell4;
                        }
                        if ((cell4.m_State & (CellFlags.Blocked | CellFlags.RoadRight)) == CellFlags.None && l < blockData.m_Size.x - 1 && (cells[num3 + 1].m_State & (CellFlags.Blocked | CellFlags.RoadRight)) == (CellFlags.Blocked | CellFlags.RoadRight)) {
                            cell4.m_State |= CellFlags.RoadRight;
                            cells[num3] = cell4;
                        }
                    }
                }
            }

            // Exact copy of NetIterator from vanilla CellCheckSystem.
            private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds)) {
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
                                    var @float = math.max(objectGeometryData.m_Size - 0.16f, 0f);
                                    this.m_IgnoreCircle = new Circle2(@float.x * 0.5f, transform.m_Position.xz);
                                    this.m_HasIgnore.y = true;
                                } else {
                                    var bounds2 = MathUtils.Expand(objectGeometryData.m_Bounds, -0.08f);
                                    var float2 = MathUtils.Center(bounds2);
                                    var @bool = bounds2.min > bounds2.max;
                                    bounds2.min = math.select(bounds2.min, float2, @bool);
                                    bounds2.max = math.select(bounds2.max, float2, @bool);
                                    this.m_IgnoreQuad = ObjectUtils.CalculateBaseCorners(transform.m_Position, transform.m_Rotation, bounds2).xz;
                                    this.m_HasIgnore.x = true;
                                }
                            }
                        }
                    }
                    var composition = this.m_CompositionData[edgeEntity];
                    var edgeGeometry = this.m_EdgeGeometryData[edgeEntity];
                    var startNodeGeometry = this.m_StartNodeGeometryData[edgeEntity];
                    var endNodeGeometry = this.m_EndNodeGeometryData[edgeEntity];
                    if (MathUtils.Intersect(this.m_Bounds, edgeGeometry.m_Bounds.xz)) {
                        var netCompositionData = this.m_PrefabCompositionData[composition.m_Edge];
                        var roadComposition = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_Edge)) {
                            roadComposition = this.m_PrefabRoadCompositionData[composition.m_Edge];
                        }
                        this.CheckSegment(edgeGeometry.m_Start.m_Left, edgeGeometry.m_Start.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                        this.CheckSegment(edgeGeometry.m_End.m_Left, edgeGeometry.m_End.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                    }
                    if (MathUtils.Intersect(this.m_Bounds, startNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData2 = this.m_PrefabCompositionData[composition.m_StartNode];
                        var roadComposition2 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_StartNode)) {
                            roadComposition2 = this.m_PrefabRoadCompositionData[composition.m_StartNode];
                        }
                        if (startNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left, startNodeGeometry.m_Geometry.m_Left.m_Right, netCompositionData2, roadComposition2, new bool2(true, true));
                            var bezier4x = MathUtils.Lerp(startNodeGeometry.m_Geometry.m_Right.m_Left, startNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
                            bezier4x.d = startNodeGeometry.m_Geometry.m_Middle.d;
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Right.m_Left, bezier4x, netCompositionData2, roadComposition2, new bool2(true, false));
                            this.CheckSegment(bezier4x, startNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData2, roadComposition2, new bool2(false, true));
                        } else {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left, startNodeGeometry.m_Geometry.m_Middle, netCompositionData2, roadComposition2, new bool2(true, false));
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Middle, startNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData2, roadComposition2, new bool2(false, true));
                        }
                    }
                    if (MathUtils.Intersect(this.m_Bounds, endNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData3 = this.m_PrefabCompositionData[composition.m_EndNode];
                        var roadComposition3 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_EndNode)) {
                            roadComposition3 = this.m_PrefabRoadCompositionData[composition.m_EndNode];
                        }
                        if (endNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(endNodeGeometry.m_Geometry.m_Left.m_Left, endNodeGeometry.m_Geometry.m_Left.m_Right, netCompositionData3, roadComposition3, new bool2(true, true));
                            var bezier4x2 = MathUtils.Lerp(endNodeGeometry.m_Geometry.m_Right.m_Left, endNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
                            bezier4x2.d = endNodeGeometry.m_Geometry.m_Middle.d;
                            this.CheckSegment(endNodeGeometry.m_Geometry.m_Right.m_Left, bezier4x2, netCompositionData3, roadComposition3, new bool2(true, false));
                            this.CheckSegment(bezier4x2, endNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData3, roadComposition3, new bool2(false, true));
                            return;
                        }
                        this.CheckSegment(endNodeGeometry.m_Geometry.m_Left.m_Left, endNodeGeometry.m_Geometry.m_Middle, netCompositionData3, roadComposition3, new bool2(true, false));
                        this.CheckSegment(endNodeGeometry.m_Geometry.m_Middle, endNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData3, roadComposition3, new bool2(false, true));
                    }
                }

                private void CheckSegment(Bezier4x3 left, Bezier4x3 right, NetCompositionData prefabCompositionData, RoadComposition prefabRoadData, bool2 isEdge) {
                    if ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Tunnel) != (CompositionFlags.General)0U) {
                        return;
                    }
                    if ((prefabCompositionData.m_State & CompositionState.BlockZone) == (CompositionState)0) {
                        return;
                    }
                    var flag = (prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) > (CompositionFlags.General)0U;
                    flag |= (prefabCompositionData.m_State & CompositionState.ExclusiveGround) == (CompositionState)0;
                    if (!MathUtils.Intersect((MathUtils.Bounds(left) | MathUtils.Bounds(right)).xz, this.m_Bounds)) {
                        return;
                    }
                    isEdge &= ((prefabRoadData.m_Flags & Game.Prefabs.RoadFlags.EnableZoning) > (Game.Prefabs.RoadFlags)0) & ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) == (CompositionFlags.General)0U);
                    isEdge &= new bool2((prefabCompositionData.m_Flags.m_Left & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == (CompositionFlags.Side)0U, (prefabCompositionData.m_Flags.m_Right & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == (CompositionFlags.Side)0U);
                    Quad3 quad;
                    quad.a = left.a;
                    quad.b = right.a;
                    var bounds = NetIterator.SetHeightRange(MathUtils.Bounds(quad.a, quad.b), prefabCompositionData.m_HeightRange);
                    for (var i = 1; i <= 8; i++) {
                        var num = (float)i / 8f;
                        quad.d = MathUtils.Position(left, num);
                        quad.c = MathUtils.Position(right, num);
                        var bounds2 = NetIterator.SetHeightRange(MathUtils.Bounds(quad.d, quad.c), prefabCompositionData.m_HeightRange);
                        var bounds3 = bounds | bounds2;
                        if (MathUtils.Intersect(bounds3.xz, this.m_Bounds) && MathUtils.Intersect(this.m_Quad, quad.xz)) {
                            var cellFlags = CellFlags.Blocked;
                            if (isEdge.x) {
                                var block = new Block {
                                    m_Direction = math.normalizesafe(MathUtils.Right(quad.d.xz - quad.a.xz), default(float2))
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(this.m_BlockData, block);
                            }
                            if (isEdge.y) {
                                var block2 = new Block {
                                    m_Direction = math.normalizesafe(MathUtils.Left(quad.c.xz - quad.b.xz), default(float2))
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

                private static Bounds3 SetHeightRange(Bounds3 bounds, Bounds1 heightRange) {
                    bounds.min.y = bounds.min.y + heightRange.min;
                    bounds.max.y = bounds.max.y + heightRange.max;
                    return bounds;
                }

                private void CheckOverlapX(Bounds2 bounds1, Bounds3 bounds2, Quad2 quad1, Quad3 quad2, int4 xxzz1, CellFlags flags, bool isElevated) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
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
                        if (MathUtils.Intersect(bounds3, bounds2.xz)) {
                            this.CheckOverlapZ(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }
                        if (MathUtils.Intersect(bounds4, bounds2.xz)) {
                            this.CheckOverlapZ(bounds4, bounds2, quad4, quad2, int2, flags, isElevated);
                            return;
                        }
                    } else {
                        this.CheckOverlapZ(bounds1, bounds2, quad1, quad2, xxzz1, flags, isElevated);
                    }
                }

                private void CheckOverlapZ(Bounds2 bounds1, Bounds3 bounds2, Quad2 quad1, Quad3 quad2, int4 xxzz1, CellFlags flags, bool isElevated) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
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
                        if (MathUtils.Intersect(bounds3, bounds2.xz)) {
                            this.CheckOverlapX(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }
                        if (MathUtils.Intersect(bounds4, bounds2.xz)) {
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
                        quad1 = MathUtils.Expand(quad1, -0.0625f);
                        if (MathUtils.Intersect(quad1, quad2.xz)) {
                            if (math.any(this.m_HasIgnore)) {
                                if (this.m_HasIgnore.x && MathUtils.Intersect(quad1, this.m_IgnoreQuad)) {
                                    return;
                                }
                                if (this.m_HasIgnore.y && MathUtils.Intersect(quad1, this.m_IgnoreCircle)) {
                                    return;
                                }
                            }
                            if (isElevated) {
                                cell.m_Height = (short)math.clamp(Mathf.FloorToInt(bounds2.min.y), -32768, math.min((int)cell.m_Height, 32767));
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

                public Bounds2 m_Bounds;

                public Quad2 m_Quad;

                public Quad2 m_IgnoreQuad;

                public Circle2 m_IgnoreCircle;

                public bool2 m_HasIgnore;

                public DynamicBuffer<Cell> m_Cells;
                public ComponentLookup<Owner> m_OwnerData;
                public ComponentLookup<Game.Objects.Transform> m_TransformData;
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
            private struct AreaIterator : INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem areaItem) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds)) {
                        return;
                    }
                    var prefabRef = this.m_PrefabRefData[areaItem.m_Area];
                    var areaGeometryData = this.m_PrefabAreaGeometryData[prefabRef.m_Prefab];
                    if ((areaGeometryData.m_Flags & (Game.Areas.GeometryFlags.PhysicalGeometry | Game.Areas.GeometryFlags.ProtectedArea)) == (Game.Areas.GeometryFlags)0) {
                        return;
                    }
                    if ((areaGeometryData.m_Flags & Game.Areas.GeometryFlags.ProtectedArea) != (Game.Areas.GeometryFlags)0 && !this.m_NativeData.HasComponent(areaItem.m_Area)) {
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

                private void CheckOverlapX(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Triangle2 triangle2, int4 xxzz1) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
                        int2.x = @int.y;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad2.b = math.lerp(quad1.a, quad1.b, num);
                        quad2.c = math.lerp(quad1.d, quad1.c, num);
                        quad3.a = quad2.b;
                        quad3.d = quad2.c;
                        var bounds3 = MathUtils.Bounds(quad2);
                        var bounds4 = MathUtils.Bounds(quad3);
                        if (MathUtils.Intersect(bounds3, bounds2)) {
                            this.CheckOverlapZ(bounds3, bounds2, quad2, triangle2, @int);
                        }
                        if (MathUtils.Intersect(bounds4, bounds2)) {
                            this.CheckOverlapZ(bounds4, bounds2, quad3, triangle2, int2);
                            return;
                        }
                    } else {
                        this.CheckOverlapZ(bounds1, bounds2, quad1, triangle2, xxzz1);
                    }
                }

                private void CheckOverlapZ(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Triangle2 triangle2, int4 xxzz1) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
                        int2.z = @int.w;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad2.d = math.lerp(quad1.a, quad1.d, num);
                        quad2.c = math.lerp(quad1.b, quad1.c, num);
                        quad3.a = quad2.d;
                        quad3.b = quad2.c;
                        var bounds3 = MathUtils.Bounds(quad2);
                        var bounds4 = MathUtils.Bounds(quad3);
                        if (MathUtils.Intersect(bounds3, bounds2)) {
                            this.CheckOverlapX(bounds3, bounds2, quad2, triangle2, @int);
                        }
                        if (MathUtils.Intersect(bounds4, bounds2)) {
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
                        quad1 = MathUtils.Expand(quad1, -0.02f);
                        if (MathUtils.Intersect(quad1, triangle2)) {
                            cell.m_State |= CellFlags.Blocked;
                            this.m_Cells[num2] = cell;
                        }
                    }
                }

                public Entity m_BlockEntity;
                public Block m_BlockData;
                public ValidArea m_ValidAreaData;
                public Bounds2 m_Bounds;
                public Quad2 m_Quad;
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