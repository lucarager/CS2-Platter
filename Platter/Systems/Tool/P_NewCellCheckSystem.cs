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
    /// - Undoes the blocking of size 1 blocks (This allows small parcels to function correctly.)
    /// - Checks blocks and clears vanilla zoning underneath parcels (This prevents vanilla lots from spawning buildings under our custom parcels.)
    ///
    /// Some part of this code remains unchanged from the original systems and might therefore be harder to parse.
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
            //var blockOverlapQueue  = new NativeQueue<BlockOverlap>(Allocator.TempJob);
            //var blockOverlapList   = new NativeList<BlockOverlap>(Allocator.TempJob);
            //var overlapGroupsList  = new NativeList<OverlapGroup>(Allocator.TempJob);
            var updatedBlocksArray = updatedBlocksList.AsDeferredJobArray();
            //var zoneSearchTree     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle);
            //var boundsQueue        = new NativeQueue<Bounds2>(Allocator.TempJob);

            Dependency = JobHandle.CombineDependencies(Dependency, CollectUpdatedBlocks(updatedBlocksList));

            var processUpdatedBlocksJobHandle = new ProcessUpdatedBlocksJob {
                m_Blocks                      = updatedBlocksArray,
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
                m_ValidAreaData               = SystemAPI.GetComponentLookup<ValidArea>(),
            }.Schedule(updatedBlocksList, 1, JobHandle.CombineDependencies(Dependency, netSearchJobHandle, areaSearchJobHandle));
            m_NetSearchSystem.AddNetSearchTreeReader(processUpdatedBlocksJobHandle);
            m_AreaSearchSystem.AddSearchTreeReader(processUpdatedBlocksJobHandle);
            updatedBlocksList.Dispose(processUpdatedBlocksJobHandle);
            updatedBlocksArray.Dispose(processUpdatedBlocksJobHandle);

            Dependency = processUpdatedBlocksJobHandle;
            Dependency.Complete();
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
            public ComponentLookup<ValidArea> m_ValidAreaData;

            public void Execute(int index) {
                var entity = m_Blocks[index].m_Entity;

                // Exit early if it's not a parcel
                if (!m_ParcelOwnerLookup.TryGetComponent(entity, out var parcelOwner)) {
                    return;
                }

                // Retrieve data
                var block      = m_BlockLookup[entity];
                var parcelData = m_ParcelDataLookup[parcelOwner.m_Owner];
                var cellBuffer = m_CellsLookup[entity];
                var validArea  = new ValidArea() {
                    m_Area = new int4(0, block.m_Size.x, 0, block.m_Size.y),
                };
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);
                var bounds    = parcelGeo.Bounds;
                var corners   = ZoneUtils.CalculateCorners(block, validArea);

                // Sanitize the parcel data, so that we can re-calculate it from scratch
                ClearBlockStatus(block, cellBuffer);

                //// Run the cell blocking iterators
                //var netIterator = new NetIterator {
                //    m_BlockEntity               = entity,
                //    m_BlockData                 = block,
                //    m_Bounds                    = bounds.xz,
                //    m_Quad                      = corners,
                //    m_ValidAreaData             = validArea,
                //    m_Cells                     = cellBuffer,
                //    m_OwnerData                 = this.m_OwnerLookup,
                //    m_TransformData             = this.m_TransformLookup,
                //    m_EdgeGeometryData          = this.m_EdgeGeometryLookup,
                //    m_StartNodeGeometryData     = this.m_StartNodeGeometryLookup,
                //    m_EndNodeGeometryData       = this.m_EndNodeGeometryLookup,
                //    m_CompositionData           = this.m_CompositionLookup,
                //    m_PrefabRefData             = this.m_PrefabRefLookup,
                //    m_PrefabCompositionData     = this.m_NetCompositionLookup,
                //    m_PrefabRoadCompositionData = this.m_PrefabRoadCompositionLookup,
                //    m_PrefabObjectGeometryData  = this.m_PrefabObjectGeometryLookup
                //};
                //m_NetSearchTree.Iterate(ref netIterator, 0);

                //var areaIterator = new AreaIterator {
                //    m_BlockEntity            = entity,
                //    m_BlockData              = block,
                //    m_Bounds                 = bounds.xz,
                //    m_Quad                   = corners,
                //    m_ValidAreaData          = validArea,
                //    m_Cells                  = cellBuffer,
                //    m_NativeData             = this.m_NativeLookup,
                //    m_PrefabRefData          = this.m_PrefabRefLookup,
                //    m_PrefabAreaGeometryData = this.m_PrefabAreaGeometryLookup,
                //    m_AreaNodes              = this.m_AreaNodesLookup,
                //    m_AreaTriangles          = this.m_AreaTrianglesLookup
                //};
                //m_AreaSearchTree.Iterate(ref areaIterator, 0);

                // Apply additional logic to account for parcel block size
                NormalizeParcelCells(block, parcelData, cellBuffer);

                //// Process the results
                //CleanBlockedCells(block, ref validArea, cellBuffer);

                //// Set final valid area data
                //m_ValidAreaData[entity] = validArea;
            }

            // Removes blocked flag from parcel cells
            private static void ClearBlockStatus(Block blockData, DynamicBuffer<Cell> cells) {
                for (var i = 0; i < cells.Length; i++) {
                    var cell = cells[i];
                    cell.m_State &= ~CellFlags.Blocked;
                    cell.m_Zone  =  new ZoneType { m_Index = 11 };
                    cells[i]     =  cell;
                }
            }

            // Sets parcel cells to blocked if they are outside the parcel size.
            private static void NormalizeParcelCells(Block block, ParcelData parcelData, DynamicBuffer<Cell> cellsBuffer) {
                for (var col = 0; col < block.m_Size.x; col++) {
                    for (var row = 0; row < block.m_Size.y; row++) {
                        var i = row * block.m_Size.x + col;
                        var cell  = cellsBuffer[i];

                        if (col < parcelData.m_LotSize.x && row < parcelData.m_LotSize.y) {
                            continue;
                        }

                        cell.m_State   |= CellFlags.Blocked;
                        cellsBuffer[i] =  cell;
                    }
                }
            }

            // Exact copy of CleanBlockedCells from vanilla CellCheckSystem.
            private static void CleanBlockedCells(Block blockData, ref ValidArea validAreaData, DynamicBuffer<Cell> cells) {
                ValidArea validArea = default(ValidArea);
                validArea.m_Area.xz = blockData.m_Size;
                for (int i = validAreaData.m_Area.x; i < validAreaData.m_Area.y; i++) {
                    Cell cell = cells[i];
                    Cell cell2 = cells[blockData.m_Size.x + i];
                    if (((cell.m_State & CellFlags.Blocked) == CellFlags.None) & ((cell2.m_State & CellFlags.Blocked) > CellFlags.None)) {
                        cell.m_State |= CellFlags.Blocked;
                        cells[i] = cell;
                    }
                    int num = 0;
                    for (int j = validAreaData.m_Area.z + 1; j < validAreaData.m_Area.w; j++) {
                        int num2 = j * blockData.m_Size.x + i;
                        Cell cell3 = cells[num2];
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
                for (int k = validAreaData.m_Area.z; k < validAreaData.m_Area.w; k++) {
                    for (int l = validAreaData.m_Area.x; l < validAreaData.m_Area.y; l++) {
                        int num3 = k * blockData.m_Size.x + l;
                        Cell cell4 = cells[num3];
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
                        Owner owner = this.m_OwnerData[edgeEntity];
                        if (this.m_TransformData.HasComponent(owner.m_Owner)) {
                            PrefabRef prefabRef = this.m_PrefabRefData[owner.m_Owner];
                            if (this.m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab)) {
                                Game.Objects.Transform transform = this.m_TransformData[owner.m_Owner];
                                ObjectGeometryData objectGeometryData = this.m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                                if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Circular) != Game.Objects.GeometryFlags.None) {
                                    float3 @float = math.max(objectGeometryData.m_Size - 0.16f, 0f);
                                    this.m_IgnoreCircle = new Circle2(@float.x * 0.5f, transform.m_Position.xz);
                                    this.m_HasIgnore.y = true;
                                } else {
                                    Bounds3 bounds2 = MathUtils.Expand(objectGeometryData.m_Bounds, -0.08f);
                                    float3 float2 = MathUtils.Center(bounds2);
                                    bool3 @bool = bounds2.min > bounds2.max;
                                    bounds2.min = math.select(bounds2.min, float2, @bool);
                                    bounds2.max = math.select(bounds2.max, float2, @bool);
                                    this.m_IgnoreQuad = ObjectUtils.CalculateBaseCorners(transform.m_Position, transform.m_Rotation, bounds2).xz;
                                    this.m_HasIgnore.x = true;
                                }
                            }
                        }
                    }
                    Composition composition = this.m_CompositionData[edgeEntity];
                    EdgeGeometry edgeGeometry = this.m_EdgeGeometryData[edgeEntity];
                    StartNodeGeometry startNodeGeometry = this.m_StartNodeGeometryData[edgeEntity];
                    EndNodeGeometry endNodeGeometry = this.m_EndNodeGeometryData[edgeEntity];
                    if (MathUtils.Intersect(this.m_Bounds, edgeGeometry.m_Bounds.xz)) {
                        NetCompositionData netCompositionData = this.m_PrefabCompositionData[composition.m_Edge];
                        RoadComposition roadComposition = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_Edge)) {
                            roadComposition = this.m_PrefabRoadCompositionData[composition.m_Edge];
                        }
                        this.CheckSegment(edgeGeometry.m_Start.m_Left, edgeGeometry.m_Start.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                        this.CheckSegment(edgeGeometry.m_End.m_Left, edgeGeometry.m_End.m_Right, netCompositionData, roadComposition, new bool2(true, true));
                    }
                    if (MathUtils.Intersect(this.m_Bounds, startNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        NetCompositionData netCompositionData2 = this.m_PrefabCompositionData[composition.m_StartNode];
                        RoadComposition roadComposition2 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_StartNode)) {
                            roadComposition2 = this.m_PrefabRoadCompositionData[composition.m_StartNode];
                        }
                        if (startNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left, startNodeGeometry.m_Geometry.m_Left.m_Right, netCompositionData2, roadComposition2, new bool2(true, true));
                            Bezier4x3 bezier4x = MathUtils.Lerp(startNodeGeometry.m_Geometry.m_Right.m_Left, startNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
                            bezier4x.d = startNodeGeometry.m_Geometry.m_Middle.d;
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Right.m_Left, bezier4x, netCompositionData2, roadComposition2, new bool2(true, false));
                            this.CheckSegment(bezier4x, startNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData2, roadComposition2, new bool2(false, true));
                        } else {
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Left.m_Left, startNodeGeometry.m_Geometry.m_Middle, netCompositionData2, roadComposition2, new bool2(true, false));
                            this.CheckSegment(startNodeGeometry.m_Geometry.m_Middle, startNodeGeometry.m_Geometry.m_Right.m_Right, netCompositionData2, roadComposition2, new bool2(false, true));
                        }
                    }
                    if (MathUtils.Intersect(this.m_Bounds, endNodeGeometry.m_Geometry.m_Bounds.xz)) {
                        NetCompositionData netCompositionData3 = this.m_PrefabCompositionData[composition.m_EndNode];
                        RoadComposition roadComposition3 = default(RoadComposition);
                        if (this.m_PrefabRoadCompositionData.HasComponent(composition.m_EndNode)) {
                            roadComposition3 = this.m_PrefabRoadCompositionData[composition.m_EndNode];
                        }
                        if (endNodeGeometry.m_Geometry.m_MiddleRadius > 0f) {
                            this.CheckSegment(endNodeGeometry.m_Geometry.m_Left.m_Left, endNodeGeometry.m_Geometry.m_Left.m_Right, netCompositionData3, roadComposition3, new bool2(true, true));
                            Bezier4x3 bezier4x2 = MathUtils.Lerp(endNodeGeometry.m_Geometry.m_Right.m_Left, endNodeGeometry.m_Geometry.m_Right.m_Right, 0.5f);
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
                    bool flag = (prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) > (CompositionFlags.General)0U;
                    flag |= (prefabCompositionData.m_State & CompositionState.ExclusiveGround) == (CompositionState)0;
                    if (!MathUtils.Intersect((MathUtils.Bounds(left) | MathUtils.Bounds(right)).xz, this.m_Bounds)) {
                        return;
                    }
                    isEdge &= ((prefabRoadData.m_Flags & Game.Prefabs.RoadFlags.EnableZoning) > (Game.Prefabs.RoadFlags)0) & ((prefabCompositionData.m_Flags.m_General & CompositionFlags.General.Elevated) == (CompositionFlags.General)0U);
                    isEdge &= new bool2((prefabCompositionData.m_Flags.m_Left & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == (CompositionFlags.Side)0U, (prefabCompositionData.m_Flags.m_Right & (CompositionFlags.Side.Raised | CompositionFlags.Side.Lowered)) == (CompositionFlags.Side)0U);
                    Quad3 quad;
                    quad.a = left.a;
                    quad.b = right.a;
                    Bounds3 bounds = NetIterator.SetHeightRange(MathUtils.Bounds(quad.a, quad.b), prefabCompositionData.m_HeightRange);
                    for (int i = 1; i <= 8; i++) {
                        float num = (float)i / 8f;
                        quad.d = MathUtils.Position(left, num);
                        quad.c = MathUtils.Position(right, num);
                        Bounds3 bounds2 = NetIterator.SetHeightRange(MathUtils.Bounds(quad.d, quad.c), prefabCompositionData.m_HeightRange);
                        Bounds3 bounds3 = bounds | bounds2;
                        if (MathUtils.Intersect(bounds3.xz, this.m_Bounds) && MathUtils.Intersect(this.m_Quad, quad.xz)) {
                            CellFlags cellFlags = CellFlags.Blocked;
                            if (isEdge.x) {
                                Block block = new Block {
                                    m_Direction = math.normalizesafe(MathUtils.Right(quad.d.xz - quad.a.xz), default(float2))
                                };
                                cellFlags |= ZoneUtils.GetRoadDirection(this.m_BlockData, block);
                            }
                            if (isEdge.y) {
                                Block block2 = new Block {
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
                        int4 @int = xxzz1;
                        int4 int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
                        int2.x = @int.y;
                        Quad2 quad3 = quad1;
                        Quad2 quad4 = quad1;
                        float num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad3.b = math.lerp(quad1.a, quad1.b, num);
                        quad3.c = math.lerp(quad1.d, quad1.c, num);
                        quad4.a = quad3.b;
                        quad4.d = quad3.c;
                        Bounds2 bounds3 = MathUtils.Bounds(quad3);
                        Bounds2 bounds4 = MathUtils.Bounds(quad4);
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
                        int4 @int = xxzz1;
                        int4 int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
                        int2.z = @int.w;
                        Quad2 quad3 = quad1;
                        Quad2 quad4 = quad1;
                        float num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad3.d = math.lerp(quad1.a, quad1.d, num);
                        quad3.c = math.lerp(quad1.b, quad1.c, num);
                        quad4.a = quad3.d;
                        quad4.b = quad3.c;
                        Bounds2 bounds3 = MathUtils.Bounds(quad3);
                        Bounds2 bounds4 = MathUtils.Bounds(quad4);
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
                        int num2 = xxzz1.z * this.m_BlockData.m_Size.x + xxzz1.x;
                        Cell cell = this.m_Cells[num2];
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
                    PrefabRef prefabRef = this.m_PrefabRefData[areaItem.m_Area];
                    AreaGeometryData areaGeometryData = this.m_PrefabAreaGeometryData[prefabRef.m_Prefab];
                    if ((areaGeometryData.m_Flags & (Game.Areas.GeometryFlags.PhysicalGeometry | Game.Areas.GeometryFlags.ProtectedArea)) == (Game.Areas.GeometryFlags)0) {
                        return;
                    }
                    if ((areaGeometryData.m_Flags & Game.Areas.GeometryFlags.ProtectedArea) != (Game.Areas.GeometryFlags)0 && !this.m_NativeData.HasComponent(areaItem.m_Area)) {
                        return;
                    }
                    DynamicBuffer<Game.Areas.Node> dynamicBuffer = this.m_AreaNodes[areaItem.m_Area];
                    DynamicBuffer<Triangle> dynamicBuffer2 = this.m_AreaTriangles[areaItem.m_Area];
                    if (dynamicBuffer2.Length <= areaItem.m_Triangle) {
                        return;
                    }
                    Triangle3 triangle = AreaUtils.GetTriangle3(dynamicBuffer, dynamicBuffer2[areaItem.m_Triangle]);
                    this.CheckOverlapX(this.m_Bounds, bounds.m_Bounds.xz, this.m_Quad, triangle.xz, this.m_ValidAreaData.m_Area);
                }

                private void CheckOverlapX(Bounds2 bounds1, Bounds2 bounds2, Quad2 quad1, Triangle2 triangle2, int4 xxzz1) {
                    if (xxzz1.y - xxzz1.x >= 2) {
                        int4 @int = xxzz1;
                        int4 int2 = xxzz1;
                        @int.y = xxzz1.x + xxzz1.y >> 1;
                        int2.x = @int.y;
                        Quad2 quad2 = quad1;
                        Quad2 quad3 = quad1;
                        float num = (float)(@int.y - xxzz1.x) / (float)(xxzz1.y - xxzz1.x);
                        quad2.b = math.lerp(quad1.a, quad1.b, num);
                        quad2.c = math.lerp(quad1.d, quad1.c, num);
                        quad3.a = quad2.b;
                        quad3.d = quad2.c;
                        Bounds2 bounds3 = MathUtils.Bounds(quad2);
                        Bounds2 bounds4 = MathUtils.Bounds(quad3);
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
                        int4 @int = xxzz1;
                        int4 int2 = xxzz1;
                        @int.w = xxzz1.z + xxzz1.w >> 1;
                        int2.z = @int.w;
                        Quad2 quad2 = quad1;
                        Quad2 quad3 = quad1;
                        float num = (float)(@int.w - xxzz1.z) / (float)(xxzz1.w - xxzz1.z);
                        quad2.d = math.lerp(quad1.a, quad1.d, num);
                        quad2.c = math.lerp(quad1.b, quad1.c, num);
                        quad3.a = quad2.d;
                        quad3.b = quad2.c;
                        Bounds2 bounds3 = MathUtils.Bounds(quad2);
                        Bounds2 bounds4 = MathUtils.Bounds(quad3);
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
                        int num2 = xxzz1.z * this.m_BlockData.m_Size.x + xxzz1.x;
                        Cell cell = this.m_Cells[num2];
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