// <copyright file="P_CellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
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
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;
    using static Game.Zones.CellCheckHelpers;
    using Block = Game.Zones.Block;
    using Transform = Game.Objects.Transform;

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
        private Game.Zones.SearchSystem          m_ZoneSearchSystem;
        private Game.Net.SearchSystem            m_NetSearchSystem;
        private Game.Areas.SearchSystem          m_AreaSearchSystem;
        private Game.Areas.UpdateCollectSystem   m_AreaUpdateCollectSystem;
        private Game.Net.UpdateCollectSystem     m_NetUpdateCollectSystem;
        private Game.Objects.UpdateCollectSystem m_ObjectUpdateCollectSystem;
        private Game.Zones.UpdateCollectSystem   m_ZoneUpdateCollectSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Zones.UpdateCollectSystem>();
            m_ObjectUpdateCollectSystem = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            m_NetUpdateCollectSystem    = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            m_AreaUpdateCollectSystem   = World.GetOrCreateSystemManaged<Game.Areas.UpdateCollectSystem>();
            m_ZoneSearchSystem          = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
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
            var blocksArray       = updateBlocksList.AsDeferredJobArray();

            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updateBlocksList));

            var undoBlockedCellsJobHandle = new UndoBlockedSizeOneCellsJob(
                blocksArray,
                SystemAPI.GetComponentLookup<Block>(),
                SystemAPI.GetComponentLookup<ParcelOwner>(),
                SystemAPI.GetComponentLookup<ParcelData>(),
                m_NetSearchSystem.GetNetSearchTree(true, out var netSearchJobHandle),
                m_AreaSearchSystem.GetSearchTree(true, out var areaSearchJobHandle),
                SystemAPI.GetComponentLookup<Owner>(),
                SystemAPI.GetComponentLookup<Transform>(),
                SystemAPI.GetComponentLookup<EdgeGeometry>(),
                SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                SystemAPI.GetComponentLookup<Composition>(),
                SystemAPI.GetComponentLookup<PrefabRef>(),
                SystemAPI.GetComponentLookup<NetCompositionData>(),
                SystemAPI.GetComponentLookup<RoadComposition>(),
                SystemAPI.GetComponentLookup<AreaGeometryData>(),
                SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                SystemAPI.GetComponentLookup<Native>(),
                SystemAPI.GetBufferLookup<Game.Areas.Node>(),
                SystemAPI.GetBufferLookup<Triangle>(),
                SystemAPI.GetBufferLookup<Cell>(),
                SystemAPI.GetComponentLookup<ValidArea>()
            ).Schedule(updateBlocksList, 1, JobHandle.CombineDependencies(Dependency, netSearchJobHandle, areaSearchJobHandle));
            m_NetSearchSystem.AddNetSearchTreeReader(undoBlockedCellsJobHandle);
            m_AreaSearchSystem.AddSearchTreeReader(undoBlockedCellsJobHandle);

            var findOverlappingBlocksJobHandle = new FindOverlappingBlocksJob {
                m_Blocks         = sortedEntityArray,
                m_SearchTree     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle),
                m_BlockData      = SystemAPI.GetComponentLookup<Block>(),
                m_ValidAreaData  = SystemAPI.GetComponentLookup<ValidArea>(),
                m_BuildOrderData = SystemAPI.GetComponentLookup<Game.Zones.BuildOrder>(),
                m_ResultQueue    = blockOverlapQueue.AsParallelWriter(),
            }.Schedule(updateBlocksList, 1, JobHandle.CombineDependencies(undoBlockedCellsJobHandle, zoneSearchJobHandle));
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
                blockLookup: SystemAPI.GetComponentLookup<Block>(),
                buildOrderLookup: SystemAPI.GetComponentLookup<Game.Zones.BuildOrder>(),
                parcelOwnerLookup: SystemAPI.GetComponentLookup<ParcelOwner>(),
                cellsBLook: SystemAPI.GetBufferLookup<Cell>(),
                validAreaLookup: SystemAPI.GetComponentLookup<ValidArea>()
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
        public struct UndoBlockedSizeOneCellsJob : IJobParallelForDefer {
            [ReadOnly]                            private NativeArray<SortedEntity>                        m_Blocks;
            [ReadOnly]                            private ComponentLookup<Block>                           m_BlockData;
            [ReadOnly]                            private ComponentLookup<ParcelOwner>                     m_ParcelOwnerData;
            [ReadOnly]                            private ComponentLookup<ParcelData>                      m_ParcelDataData;
            [ReadOnly]                            private NativeQuadTree<Entity, QuadTreeBoundsXZ>         m_NetSearchTree;
            [ReadOnly]                            private NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> m_AreaSearchTree;
            [ReadOnly]                            private ComponentLookup<Owner>                           m_OwnerData;
            [ReadOnly]                            private ComponentLookup<Transform>                       m_TransformData;
            [ReadOnly]                            private ComponentLookup<EdgeGeometry>                    m_EdgeGeometryData;
            [ReadOnly]                            private ComponentLookup<StartNodeGeometry>               m_StartNodeGeometryData;
            [ReadOnly]                            private ComponentLookup<EndNodeGeometry>                 m_EndNodeGeometryData;
            [ReadOnly]                            private ComponentLookup<Composition>                     m_CompositionData;
            [ReadOnly]                            private ComponentLookup<PrefabRef>                       m_PrefabRefData;
            [ReadOnly]                            private ComponentLookup<NetCompositionData>              m_PrefabCompositionData;
            [ReadOnly]                            private ComponentLookup<RoadComposition>                 m_PrefabRoadCompositionData;
            [ReadOnly]                            private ComponentLookup<AreaGeometryData>                m_PrefabAreaGeometryData;
            [ReadOnly]                            private ComponentLookup<ObjectGeometryData>              m_PrefabObjectGeometryData;
            [ReadOnly]                            private ComponentLookup<Native>                          m_NativeData;
            [ReadOnly]                            private BufferLookup<Game.Areas.Node>                    m_AreaNodes;
            [ReadOnly]                            private BufferLookup<Triangle>                           m_AreaTriangles;
            [NativeDisableParallelForRestriction] private BufferLookup<Cell>                               m_Cells;
            [NativeDisableParallelForRestriction] private ComponentLookup<ValidArea>                       m_ValidAreaData;

            public UndoBlockedSizeOneCellsJob(NativeArray<SortedEntity> blocks, ComponentLookup<Block> blockData, ComponentLookup<ParcelOwner> parcelOwnerData, ComponentLookup<ParcelData> parcelDataData,
                                              NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree,
                                              NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> areaSearchTree, ComponentLookup<Owner> ownerData,
                                              ComponentLookup<Transform> transformData, ComponentLookup<EdgeGeometry> edgeGeometryData,
                                              ComponentLookup<StartNodeGeometry> startNodeGeometryData, ComponentLookup<EndNodeGeometry> endNodeGeometryData,
                                              ComponentLookup<Composition> compositionData, ComponentLookup<PrefabRef> prefabRefData,
                                              ComponentLookup<NetCompositionData> prefabCompositionData,
                                              ComponentLookup<RoadComposition> prefabRoadCompositionData,
                                              ComponentLookup<AreaGeometryData> prefabAreaGeometryData,
                                              ComponentLookup<ObjectGeometryData> prefabObjectGeometryData, ComponentLookup<Native> nativeData,
                                              BufferLookup<Game.Areas.Node> areaNodes, BufferLookup<Triangle> areaTriangles, BufferLookup<Cell> cells,
                                              ComponentLookup<ValidArea> validAreaData) {
                m_Blocks                    = blocks;
                m_BlockData                 = blockData;
                m_ParcelOwnerData                = parcelOwnerData;
                m_ParcelDataData                = parcelDataData;
                m_NetSearchTree             = netSearchTree;
                m_AreaSearchTree            = areaSearchTree;
                m_OwnerData                 = ownerData;
                m_TransformData             = transformData;
                m_EdgeGeometryData          = edgeGeometryData;
                m_StartNodeGeometryData     = startNodeGeometryData;
                m_EndNodeGeometryData       = endNodeGeometryData;
                m_CompositionData           = compositionData;
                m_PrefabRefData             = prefabRefData;
                m_PrefabCompositionData     = prefabCompositionData;
                m_PrefabRoadCompositionData = prefabRoadCompositionData;
                m_PrefabAreaGeometryData    = prefabAreaGeometryData;
                m_PrefabObjectGeometryData  = prefabObjectGeometryData;
                m_NativeData                = nativeData;
                m_AreaNodes                 = areaNodes;
                m_AreaTriangles             = areaTriangles;
                m_Cells                     = cells;
                m_ValidAreaData             = validAreaData;
            }

            public void Execute(int index) {
                // Disable job until we can figure out a better solution
                return;
                var entity       = m_Blocks[index].m_Entity;
                var block        = m_BlockData[entity];

                // Specifically only reevaluate blocks part of a parcel
                if (!m_ParcelOwnerData.HasComponent(entity)) {
                    return;
                }

                var parcelOwner  = m_ParcelOwnerData[entity];
                var parcelPrefab = m_PrefabRefData[parcelOwner.m_Owner];
                var parcelData   = m_ParcelDataData[parcelPrefab.m_Prefab];

                // Specifically only reevaluate parcels of width 1
                if (parcelData.m_LotSize.x != 1) {
                    return;
                }

                var cellBuffer = m_Cells[entity];

                for (var col = 0; col < block.m_Size.x; col++) {
                    for (var row = 0; row < block.m_Size.y; row++) {
                        var i    = (row * block.m_Size.x) + col;
                        var cell = cellBuffer[i];

                        if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                            cell.m_State |= CellFlags.Blocked;
                        } else {
                            cell.m_State &= ~CellFlags.Blocked;
                        }
                        cellBuffer[i] = cell;
                    }
                }

                // Create a copy of the block data and set it to our parcel size
                var actualBlock = block;
                actualBlock.m_Size.x = parcelData.m_LotSize.x;
                actualBlock.m_Size.y = parcelData.m_LotSize.y;

                // Rest of the code is similar to vanilla's BlockCellsJob
                var validArea   = default(ValidArea);
                validArea.m_Area = new int4(0, actualBlock.m_Size.x, 0, actualBlock.m_Size.y);
                var bounds = ZoneUtils.CalculateBounds(actualBlock);
                var corners   = ZoneUtils.CalculateCorners(actualBlock);
                
                // Iterate over nets and check overlaps
                var netIterator = new NetIterator {
                    m_BlockEntity               = entity,
                    m_BlockData                 = actualBlock,
                    m_Bounds                    = bounds,
                    m_Corners                   = corners,
                    m_ValidAreaData             = validArea,
                    m_Cells                     = cellBuffer,
                    m_OwnerData                 = m_OwnerData,
                    m_TransformData             = m_TransformData,
                    m_EdgeGeometryData          = m_EdgeGeometryData,
                    m_StartNodeGeometryData     = m_StartNodeGeometryData,
                    m_EndNodeGeometryData       = m_EndNodeGeometryData,
                    m_CompositionData           = m_CompositionData,
                    m_PrefabRefData             = m_PrefabRefData,
                    m_PrefabCompositionData     = m_PrefabCompositionData,
                    m_PrefabRoadCompositionData = m_PrefabRoadCompositionData,
                    m_PrefabObjectGeometryData  = m_PrefabObjectGeometryData,
                };
                m_NetSearchTree.Iterate(ref netIterator);

                // Iterate over areas and check overlap
                var areaIterator = new AreaIterator {
                    m_BlockEntity            = entity,
                    m_BlockData              = actualBlock,
                    m_Bounds                 = bounds,
                    m_Corners                = corners,
                    m_ValidAreaData          = validArea,
                    m_Cells                  = cellBuffer,
                    m_NativeData             = m_NativeData,
                    m_PrefabRefData          = m_PrefabRefData,
                    m_PrefabAreaGeometryData = m_PrefabAreaGeometryData,
                    m_AreaNodes              = m_AreaNodes,
                    m_AreaTriangles          = m_AreaTriangles,
                };
                m_AreaSearchTree.Iterate(ref areaIterator);
                
                // Here we are skipping vailla's CleanBlockedCells

                m_ValidAreaData[entity] = validArea;
            }

            private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds); }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity edgeEntity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds)) {
                        return;
                    }

                    if (!m_EdgeGeometryData.HasComponent(edgeEntity)) {
                        return;
                    }

                    m_HasIgnore = false;
                    if (m_OwnerData.HasComponent(edgeEntity)) {
                        var owner = m_OwnerData[edgeEntity];
                        if (m_TransformData.HasComponent(owner.m_Owner)) {
                            var prefabRef = m_PrefabRefData[owner.m_Owner];
                            if (m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab)) {
                                var transform          = m_TransformData[owner.m_Owner];
                                var objectGeometryData = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                                if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Circular) != Game.Objects.GeometryFlags.None) {
                                    var @float = math.max(objectGeometryData.m_Size - 0.16f, 0f);
                                    m_IgnoreCircle = new Circle2(@float.x * 0.5f, transform.m_Position.xz);
                                    m_HasIgnore.y  = true;
                                } else {
                                    var bounds2 = MathUtils.Expand(objectGeometryData.m_Bounds, -0.08f);
                                    var float2  = MathUtils.Center(bounds2);
                                    var @bool   = bounds2.min > bounds2.max;
                                    bounds2.min   = math.select(bounds2.min, float2, @bool);
                                    bounds2.max   = math.select(bounds2.max, float2, @bool);
                                    m_IgnoreQuad  = ObjectUtils.CalculateBaseCorners(transform.m_Position, transform.m_Rotation, bounds2).xz;
                                    m_HasIgnore.x = true;
                                }
                            }
                        }
                    }

                    var composition       = m_CompositionData[edgeEntity];
                    var edgeGeometry      = m_EdgeGeometryData[edgeEntity];
                    var startNodeGeometry = m_StartNodeGeometryData[edgeEntity];
                    var endNodeGeometry   = m_EndNodeGeometryData[edgeEntity];
                    if (MathUtils.Intersect(m_Bounds, edgeGeometry.m_Bounds.xz)) {
                        var netCompositionData = m_PrefabCompositionData[composition.m_Edge];
                        var roadComposition    = default(RoadComposition);
                        if (m_PrefabRoadCompositionData.HasComponent(composition.m_Edge)) {
                            roadComposition = m_PrefabRoadCompositionData[composition.m_Edge];
                        }

                        CheckSegment(edgeGeometry.m_Start.m_Left, edgeGeometry.m_Start.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                        CheckSegment(edgeGeometry.m_End.m_Left, edgeGeometry.m_End.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                    }

                    if (MathUtils.Intersect(m_Bounds, startNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData2 = m_PrefabCompositionData[composition.m_StartNode];
                        var roadComposition2    = default(RoadComposition);
                        if (m_PrefabRoadCompositionData.HasComponent(composition.m_StartNode)) {
                            roadComposition2 = m_PrefabRoadCompositionData[composition.m_StartNode];
                        }

                        if (startNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            CheckSegment(
                                startNodeGeometry.m_Geometry.m_Left.m_Left,
                                startNodeGeometry.m_Geometry.m_Left.m_Right,
                                netCompositionData2,
                                roadComposition2,
                                new bool2(true, true));
                            var bezier4x = MathUtils.Lerp(startNodeGeometry.m_Geometry.m_Right.m_Left, startNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
                            bezier4x.d = startNodeGeometry.m_Geometry.m_Middle.d;
                            CheckSegment(startNodeGeometry.m_Geometry.m_Right.m_Left, bezier4x, netCompositionData2, roadComposition2, new bool2(true, false));
                            CheckSegment(bezier4x, startNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData2, roadComposition2, new bool2(false, true));
                        } else {
                            CheckSegment(
                                startNodeGeometry.m_Geometry.m_Left.m_Left,
                                startNodeGeometry.m_Geometry.m_Middle,
                                netCompositionData2,
                                roadComposition2,
                                new bool2(true, false));
                            CheckSegment(
                                startNodeGeometry.m_Geometry.m_Middle,
                                startNodeGeometry.m_Geometry.m_Right.m_Right,
                                netCompositionData2,
                                roadComposition2,
                                new bool2(false, true));
                        }
                    }

                    if (MathUtils.Intersect(m_Bounds, endNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        var netCompositionData3 = m_PrefabCompositionData[composition.m_EndNode];
                        var roadComposition3    = default(RoadComposition);
                        if (m_PrefabRoadCompositionData.HasComponent(composition.m_EndNode)) {
                            roadComposition3 = m_PrefabRoadCompositionData[composition.m_EndNode];
                        }

                        if (endNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            CheckSegment(
                                endNodeGeometry.m_Geometry.m_Left.m_Left,
                                endNodeGeometry.m_Geometry.m_Left.m_Right,
                                netCompositionData3,
                                roadComposition3,
                                new bool2(true, true));
                            var bezier4x2 = MathUtils.Lerp(endNodeGeometry.m_Geometry.m_Right.m_Left, endNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
                            bezier4x2.d = endNodeGeometry.m_Geometry.m_Middle.d;
                            CheckSegment(endNodeGeometry.m_Geometry.m_Right.m_Left, bezier4x2, netCompositionData3, roadComposition3, new bool2(true, false));
                            CheckSegment(bezier4x2, endNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData3, roadComposition3, new bool2(false, true));
                            return;
                        }

                        CheckSegment(
                            endNodeGeometry.m_Geometry.m_Left.m_Left,
                            endNodeGeometry.m_Geometry.m_Middle,
                            netCompositionData3,
                            roadComposition3,
                            new bool2(true, false));
                        CheckSegment(
                            endNodeGeometry.m_Geometry.m_Middle,
                            endNodeGeometry.m_Geometry.m_Right.m_Right,
                            netCompositionData3,
                            roadComposition3,
                            new bool2(false, true));
                    }
                }

                private void CheckSegment(Bezier4x3 left, Bezier4x3 right, NetCompositionData prefabCompositionData, RoadComposition prefabRoadData,
                                          bool2     isEdge) {
                    if ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Tunnel) != 0U) {
                        return;
                    }

                    if ((prefabCompositionData.m_State & CompositionState.BlockZone) == 0) {
                        return;
                    }

                    var flag = (prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) > 0U;
                    flag |= (prefabCompositionData.m_State & CompositionState.ExclusiveGround) == 0;
                    if (!MathUtils.Intersect((MathUtils.Bounds(left) | MathUtils.Bounds(right)).xz, m_Bounds)) {
                        return;
                    }

                    isEdge &= ((prefabRoadData.m_Flags                  & Game.Prefabs.RoadFlags.EnableZoning) > 0) &
                              ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated)   == 0U);
                    isEdge &= new bool2(
                        (prefabCompositionData.m_Flags.m_Left  & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == 0U,
                        (prefabCompositionData.m_Flags.m_Right & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == 0U);
                    Quad3 corners;
                    corners.a = left.a;
                    corners.b = right.a;
                    var bounds = SetHeightRange(MathUtils.Bounds(corners.a, corners.b), prefabCompositionData.m_HeightRange);
                    for (var i = 1; i <= 8; i++) {
                        var num = i / 8f;
                        corners.d = MathUtils.Position(left, num);
                        corners.c = MathUtils.Position(right, num);
                        var bounds2 = SetHeightRange(MathUtils.Bounds(corners.d, corners.c), prefabCompositionData.m_HeightRange);
                        var bounds3 = bounds | bounds2;
                        if (MathUtils.Intersect(bounds3.xz, m_Bounds) && MathUtils.Intersect(m_Corners, corners.xz)) {
                            //var cellFlags = CellFlags.Blocked;
                            var cellFlags = CellFlags.None;
                            if (isEdge.x) {
                                var block = new Block {
                                    m_Direction = math.normalizesafe(MathUtils.Right(corners.d.xz - corners.a.xz)),
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(m_BlockData, block);
                            }

                            if (isEdge.y) {
                                var block2 = new Block {
                                    m_Direction = math.normalizesafe(MathUtils.Left(corners.c.xz - corners.b.xz)),
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(m_BlockData, block2);
                            }

                            CheckOverlapX(m_Bounds, bounds3, m_Corners, corners, m_ValidAreaData.m_Area, cellFlags, flag);
                        }

                        corners.a = corners.d;
                        corners.b = corners.c;
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
                        @int.y = (xxzz1.x + xxzz1.y) >> 1;
                        int2.x = @int.y;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num   = (@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad3.b = math.lerp(quad1.a, quad1.b, num);
                        quad3.c = math.lerp(quad1.d, quad1.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds3, bounds2.xz)) {
                            CheckOverlapZ(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }

                        if (MathUtils.Intersect(bounds4, bounds2.xz)) {
                            CheckOverlapZ(bounds4, bounds2, quad4, quad2, int2, flags, isElevated);
                        }
                    } else {
                        CheckOverlapZ(bounds1, bounds2, quad1, quad2, xxzz1, flags, isElevated);
                    }
                }

                private void CheckOverlapZ(Bounds2 bounds1, Bounds3 bounds2, Quad2 quad1, Quad3 quad2, int4 xxzz1, CellFlags flags, bool isElevated) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = (xxzz1.z + xxzz1.w) >> 1;
                        int2.z = @int.w;
                        var quad3 = quad1;
                        var quad4 = quad1;
                        var num   = (@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad3.d = math.lerp(quad1.a, quad1.d, num);
                        quad3.c = math.lerp(quad1.b, quad1.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        var bounds3 = MathUtils.Bounds(quad3);
                        var bounds4 = MathUtils.Bounds(quad4);
                        if (MathUtils.Intersect(bounds3, bounds2.xz)) {
                            CheckOverlapX(bounds3, bounds2, quad3, quad2, @int, flags, isElevated);
                        }

                        if (MathUtils.Intersect(bounds4, bounds2.xz)) {
                            CheckOverlapX(bounds4, bounds2, quad4, quad2, int2, flags, isElevated);
                        }
                    } else {
                        if (xxzz1.y - xxzz1.x >= 2) {
                            CheckOverlapX(bounds1, bounds2, quad1, quad2, xxzz1, flags, isElevated);
                            return;
                        }

                        var num2 = xxzz1.z * m_BlockData.m_Size.x + xxzz1.x;
                        var cell = m_Cells[num2];
                        if ((cell.m_State & flags) == flags) {
                            return;
                        }

                        quad1 = MathUtils.Expand(quad1, -0.0625f);
                        if (MathUtils.Intersect(quad1, quad2.xz)) {
                            if (math.any(m_HasIgnore)) {
                                if (m_HasIgnore.x && MathUtils.Intersect(quad1, m_IgnoreQuad)) {
                                    return;
                                }

                                if (m_HasIgnore.y && MathUtils.Intersect(quad1, m_IgnoreCircle)) {
                                    return;
                                }
                            }

                            if (isElevated) {
                                cell.m_Height = (short)math.clamp(Mathf.FloorToInt(bounds2.min.y), -32768, math.min(cell.m_Height, 32767));
                            } else {
                                cell.m_State |= flags;
                            }

                            m_Cells[num2] = cell;
                        }
                    }
                }

                public Entity m_BlockEntity;
                public Block m_BlockData;
                public ValidArea m_ValidAreaData;
                public Bounds2 m_Bounds;
                public Quad2 m_Corners;
                public Quad2 m_IgnoreQuad;
                public Circle2 m_IgnoreCircle;
                public bool2 m_HasIgnore;
                public DynamicBuffer<Cell> m_Cells;
                public ComponentLookup<Owner> m_OwnerData;
                public ComponentLookup<Transform> m_TransformData;
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;
                public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;
                public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;
                public ComponentLookup<Composition> m_CompositionData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
                public ComponentLookup<RoadComposition> m_PrefabRoadCompositionData;
                public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
            }

            private struct AreaIterator : INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ> {
                public bool Intersect(QuadTreeBoundsXZ bounds) { return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds); }

                public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem areaItem) {
                    if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds)) {
                        return;
                    }

                    var prefabRef        = m_PrefabRefData[areaItem.m_Area];
                    var areaGeometryData = m_PrefabAreaGeometryData[prefabRef.m_Prefab];
                    if ((areaGeometryData.m_Flags & (Game.Areas.GeometryFlags.PhysicalGeometry | Game.Areas.GeometryFlags.ProtectedArea)) == 0) {
                        return;
                    }

                    if ((areaGeometryData.m_Flags & Game.Areas.GeometryFlags.ProtectedArea) != 0 && !m_NativeData.HasComponent(areaItem.m_Area)) {
                        return;
                    }

                    var dynamicBuffer  = m_AreaNodes[areaItem.m_Area];
                    var dynamicBuffer2 = m_AreaTriangles[areaItem.m_Area];
                    if (dynamicBuffer2.Length <= areaItem.m_Triangle) {
                        return;
                    }

                    var triangle = AreaUtils.GetTriangle3(dynamicBuffer, dynamicBuffer2[areaItem.m_Triangle]);
                    CheckOverlapX(m_Bounds, bounds.m_Bounds.xz, m_Corners, triangle.xz, m_ValidAreaData.m_Area);
                }

                private void CheckOverlapX(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Triangle2 triangle2, int4 xxzz1) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.y = (xxzz1.x + xxzz1.y) >> 1;
                        int2.x = @int.y;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num   = (@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad2.b = math.lerp(quad1.a, quad1.b, num);
                        quad2.c = math.lerp(quad1.d, quad1.c, num);
                        quad3.a = quad2.b;
                        quad3.d = quad2.c;
                        var bounds3 = MathUtils.Bounds(quad2);
                        var bounds4 = MathUtils.Bounds(quad3);
                        if (MathUtils.Intersect(bounds3, bounds2)) {
                            CheckOverlapZ(bounds3, bounds2, quad2, triangle2, @int);
                        }

                        if (MathUtils.Intersect(bounds4, bounds2)) {
                            CheckOverlapZ(bounds4, bounds2, quad3, triangle2, int2);
                        }
                    } else {
                        CheckOverlapZ(bounds1, bounds2, quad1, triangle2, xxzz1);
                    }
                }

                private void CheckOverlapZ(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Triangle2 triangle2, int4 xxzz1) {
                    if (xxzz1.w - xxzz1.z >= 2) {
                        var @int = xxzz1;
                        var int2 = xxzz1;
                        @int.w = (xxzz1.z + xxzz1.w) >> 1;
                        int2.z = @int.w;
                        var quad2 = quad1;
                        var quad3 = quad1;
                        var num   = (@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad2.d = math.lerp(quad1.a, quad1.d, num);
                        quad2.c = math.lerp(quad1.b, quad1.c, num);
                        quad3.a = quad2.d;
                        quad3.b = quad2.c;
                        var bounds3 = MathUtils.Bounds(quad2);
                        var bounds4 = MathUtils.Bounds(quad3);
                        if (MathUtils.Intersect(bounds3, bounds2)) {
                            CheckOverlapX(bounds3, bounds2, quad2, triangle2, @int);
                        }

                        if (MathUtils.Intersect(bounds4, bounds2)) {
                            CheckOverlapX(bounds4, bounds2, quad3, triangle2, int2);
                        }
                    } else {
                        if (xxzz1.y - xxzz1.x >= 2) {
                            CheckOverlapX(bounds1, bounds2, quad1, triangle2, xxzz1);
                            return;
                        }

                        var num2 = xxzz1.z * m_BlockData.m_Size.x + xxzz1.x;
                        var cell = m_Cells[num2];
                        if ((cell.m_State & CellFlags.Blocked) != CellFlags.None) {
                            return;
                        }

                        quad1 = MathUtils.Expand(quad1, -0.02f);
                        if (MathUtils.Intersect(quad1, triangle2)) {
                            cell.m_State  |= CellFlags.Blocked;
                            m_Cells[num2] =  cell;
                        }
                    }
                }

                public Entity m_BlockEntity;
                public Block m_BlockData;
                public ValidArea m_ValidAreaData;
                public Bounds2 m_Bounds;
                public Quad2 m_Corners;
                public DynamicBuffer<Cell> m_Cells;
                public ComponentLookup<Native> m_NativeData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<AreaGeometryData> m_PrefabAreaGeometryData;
                public BufferLookup<Game.Areas.Node> m_AreaNodes;
                public BufferLookup<Triangle> m_AreaTriangles;
            }
        }
#if USE_BURST
        [BurstCompile]
#endif
        public struct ClearBlocksJob : IJobParallelForDefer {
            [ReadOnly]                            private NativeArray<OverlapGroup>              m_OverlapGroups;
            [ReadOnly]                            private ComponentLookup<ParcelOwner>           m_ParcelOwnerLookup;
            [ReadOnly]                            private ComponentLookup<Block>                 m_BlockLookup;
            [ReadOnly]                            private ComponentLookup<Game.Zones.BuildOrder> m_BuildOrderLookup;
            [NativeDisableParallelForRestriction] private ComponentLookup<ValidArea>             m_ValidAreaLookup;
            [NativeDisableParallelForRestriction] private NativeArray<BlockOverlap>              m_BlockOverlapArray;
            [NativeDisableParallelForRestriction] private BufferLookup<Cell>                     m_CellBLook;

            public ClearBlocksJob(NativeArray<OverlapGroup>  overlapGroups,     ComponentLookup<ParcelOwner>           parcelOwnerLookup,
                                  ComponentLookup<Block>     blockLookup,        ComponentLookup<Game.Zones.BuildOrder> buildOrderLookup,
                                  NativeArray<BlockOverlap>  blockOverlapArray, BufferLookup<Cell>                     cellsBLook,
                                  ComponentLookup<ValidArea> validAreaLookup) {
                m_OverlapGroups     = overlapGroups;
                m_ParcelOwnerLookup  = parcelOwnerLookup;
                m_BlockLookup        = blockLookup;
                m_BuildOrderLookup   = buildOrderLookup;
                m_BlockOverlapArray = blockOverlapArray;
                m_CellBLook = cellsBLook;
                m_ValidAreaLookup = validAreaLookup;
            }

            public void Execute(int index) {
                var overlapGroup = m_OverlapGroups[index];

                var overlapIterator = new OverlapIterator(
                    blockLookup: m_BlockLookup,
                    validAreaLookup: m_ValidAreaLookup,
                    buildOrderLookup: m_BuildOrderLookup,
                    cellsBLook: m_CellBLook,
                    parcelOwnerLookup: m_ParcelOwnerLookup
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

                private ComponentLookup<ParcelOwner>           m_ParcelOwnerLookup;
                private ComponentLookup<ParcelData>            m_ParcelDataLookup;
                private ComponentLookup<PrefabRef>             m_PrefabRefLookup;
                private ComponentLookup<Block>                 m_BlockLookup;
                private ComponentLookup<ValidArea>             m_ValidAreaLookup;
                private ComponentLookup<Game.Zones.BuildOrder> m_BuildOrderLookup;
                private BufferLookup<Cell>                     m_CellBLook;
                private Entity                                 m_CurBlockEntity;
                private ValidArea                              m_CurValidArea;
                private Bounds2                                m_CurBounds;
                private Block                                  m_CurBlock;
                private Game.Zones.BuildOrder                  m_CurBuildOrder;
                private DynamicBuffer<Cell>                    m_CurCellBuffer;
                private Quad2                                  m_CurCorners;
                private bool                                   m_CurBlockIsParcel;
                private int2                                   m_CurBlockParcelBounds;
                private Entity                                 m_OtherBlockEntity;
                private Block                                  m_OtherBlock;
                private ValidArea                              m_OtherValidArea;
                private Game.Zones.BuildOrder                  m_OtherBuildOrder;
                private DynamicBuffer<Cell>                    m_OtherCells;
                private bool                                   m_OtherBlockIsParcel;
                private int2                                   m_OtherBlockParcelBounds;

                public OverlapIterator(ComponentLookup<ParcelOwner> parcelOwnerLookup, ComponentLookup<Block>                 blockLookup,
                                       ComponentLookup<ValidArea>   validAreaLookup,   ComponentLookup<Game.Zones.BuildOrder> buildOrderLookup,
                                       BufferLookup<Cell>           cellsBLook) : this() {
                    m_ParcelOwnerLookup = parcelOwnerLookup;
                    m_BlockLookup       = blockLookup;
                    m_ValidAreaLookup   = validAreaLookup;
                    m_BuildOrderLookup  = buildOrderLookup;
                    m_CellBLook        = cellsBLook;
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
                    m_OtherBlockEntity     = otherBlock;
                    m_OtherBlock           = m_BlockLookup[otherBlock];
                    m_OtherValidArea       = m_ValidAreaLookup[otherBlock];
                    m_OtherBuildOrder      = m_BuildOrderLookup[otherBlock];
                    m_OtherCells           = m_CellBLook[otherBlock];
                    m_OtherBlockIsParcel   = m_ParcelOwnerLookup.HasComponent(otherBlock);
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
                        otherCell.m_State = CellFlags.Redundant;
                    } else if (m_OtherBlockIsParcel) {
                        curCell.m_Zone  = ZoneType.None;
                        curCell.m_State = CellFlags.Redundant;
                    }

                    m_CurCellBuffer[curIndex] = curCell;
                    m_OtherCells[otherIndex]  = otherCell;
                }
            }
        }
    }
}