// <copyright file="P_GenerateZonesSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Components;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Block = Game.Zones.Block;

    // todo: ZoneAndOccupyCellsJob/CheckBlockOverlapJob still runs, need to prevent underlying zoning logic from vanilla
    public partial class P_GenerateZonesSystem : GameSystemBase {
        private EntityQuery          m_DefinitionQuery;
        private ModificationBarrier1 m_ModificationBarrier;
        private SearchSystem         m_ZoneSearchSystem;
        private P_ParcelSearchSystem m_ParcelSearchSystem;

        protected override void OnCreate() {
            base.OnCreate();
            m_ZoneSearchSystem    = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_DefinitionQuery = GetEntityQuery(
                ComponentType.ReadOnly<CreationDefinition>(), ComponentType.ReadOnly<Zoning>(),
                ComponentType.ReadOnly<Updated>());
            RequireForUpdate(m_DefinitionQuery);
        }

        protected override void OnUpdate() {
            var zonedCellsHashMap = new NativeParallelMultiHashMap<Entity, CellData>(1000, Allocator.TempJob);
            var zonedBlocksList   = new NativeList<Entity>(20, Allocator.TempJob);
            var commandBuffer     = new EntityCommandBuffer(Allocator.TempJob);

            var fillBlocksListJob = new FillBlocksListJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<CreationDefinition>(),
                SystemAPI.GetComponentTypeHandle<Zoning>(),
                SystemAPI.GetComponentLookup<Block>(),
                SystemAPI.GetComponentLookup<ParcelOwner>(),
                SystemAPI.GetComponentLookup<ZoneData>(),
                SystemAPI.GetBufferLookup<Cell>(),
                m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchJobHandle),
                zonedCellsHashMap,
                zonedBlocksList,
                commandBuffer.AsParallelWriter()
            ).Schedule(m_DefinitionQuery, JobHandle.CombineDependencies(Dependency, zoneSearchJobHandle));
            
            m_ZoneSearchSystem.AddSearchTreeReader(fillBlocksListJob);
            fillBlocksListJob.Complete();
            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();

            var createBlocksJobHandle = new CreateBlocksJob(
                SystemAPI.GetComponentLookup<Block>(),
                SystemAPI.GetComponentLookup<PrefabRef>(),
                SystemAPI.GetComponentLookup<ZoneBlockData>(),
                SystemAPI.GetBufferLookup<Cell>(),
                commandBuffer: m_ModificationBarrier.CreateCommandBuffer().AsParallelWriter(),
                zonedCells: zonedCellsHashMap,
                zonedBlocks: zonedBlocksList.AsArray()
            ).Schedule(zonedBlocksList, 1, fillBlocksListJob);

            zonedCellsHashMap.Dispose(createBlocksJobHandle);
            zonedBlocksList.Dispose(createBlocksJobHandle);
            m_ModificationBarrier.AddJobHandleForProducer(createBlocksJobHandle);

            Dependency = createBlocksJobHandle;
        }

        private struct CellData {
            public int2 m_Location;
            public ZoneType m_ZoneType;
        }

        private struct BaseCell {
            public Entity m_Block;
            public int2 m_Location;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct FillBlocksListJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle      m_EntityTh;
            [ReadOnly] private ComponentTypeHandle<CreationDefinition>      m_CreationDefinitionTh;
            [ReadOnly] private ComponentTypeHandle<Zoning>                  m_ZoningTh;
            [ReadOnly] private ComponentLookup<Block>                       m_BlockCLook;
            [ReadOnly] private ComponentLookup<ParcelOwner>                 m_ParcelOwnerCLook;
            [ReadOnly] private ComponentLookup<ZoneData>                    m_ZoneCLook;
            [ReadOnly] private BufferLookup<Cell>                           m_CellsBLook;
            [ReadOnly] private NativeQuadTree<Entity, Bounds2>              m_ZoneSearchTree;
            private            NativeParallelMultiHashMap<Entity, CellData> m_ZonedCells;
            private            NativeList<Entity>                           m_ZonedBlocks;
            private            EntityCommandBuffer.ParallelWriter           m_CommandBuffer;

            public FillBlocksListJob(EntityTypeHandle entityTh, ComponentTypeHandle<CreationDefinition> creationDefinitionTh, ComponentTypeHandle<Zoning> zoningTh, ComponentLookup<Block> blockCLook, ComponentLookup<ParcelOwner> parcelOwnerCLook, ComponentLookup<ZoneData> zoneCLook, BufferLookup<Cell> cellsBLook, NativeQuadTree<Entity, Bounds2> zoneSearchTree, NativeParallelMultiHashMap<Entity, CellData> zonedCells, NativeList<Entity> zonedBlocks, EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTh = entityTh;
                m_CreationDefinitionTh = creationDefinitionTh;
                m_ZoningTh = zoningTh;
                m_BlockCLook = blockCLook;
                m_ParcelOwnerCLook = parcelOwnerCLook;
                m_ZoneCLook = zoneCLook;
                m_CellsBLook = cellsBLook;
                m_ZoneSearchTree = zoneSearchTree;
                m_ZonedCells = zonedCells;
                m_ZonedBlocks = zonedBlocks;
                m_CommandBuffer = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray             = chunk.GetNativeArray(m_EntityTh);
                var creationDefinitionArray = chunk.GetNativeArray(ref m_CreationDefinitionTh);
                var zoningArray             = chunk.GetNativeArray(ref m_ZoningTh);

                for (var i = 0; i < creationDefinitionArray.Length; i++) {
                    var entity = entityArray[i];
                    var creationDefinition = creationDefinitionArray[i];
                    var zoning             = zoningArray[i];

                    if (creationDefinition.m_Prefab == Entity.Null) {
                        continue;
                    }

                    var isZoning   = (zoning.m_Flags & ZoningFlags.Zone)   != 0U;
                    var isDezoning = (zoning.m_Flags & ZoningFlags.Dezone) != 0U;

                    if (!isZoning && !isDezoning) {
                        return;
                    }

                    // Retrieve cells targeted by the zoning tool
                    var baseCells = new NativeList<BaseCell>(10, Allocator.Temp);
                    AddCells(zoning.m_Position.xz.bc, baseCells);

                    // If none, we can exit early
                    if (baseCells.Length == 0) {
                        return;
                    }

                    // Now that we're here, remove the CreationDefinition and Updated components to avoid vanilla processing
                    m_CommandBuffer.RemoveComponent<CreationDefinition>(unfilteredChunkIndex, entity);

                    // If we have parcels targetd, we run our own logic.
                    var zoneData = m_ZoneCLook[creationDefinition.m_Prefab];
                    var zoneType = isZoning ? zoneData.m_ZoneType : ZoneType.None;

                    if ((zoning.m_Flags & ZoningFlags.FloodFill) != 0U) {
                        FloodFillBlocks(creationDefinition, zoning, zoneData, zoneType, baseCells);
                    }

                    if ((zoning.m_Flags & ZoningFlags.Paint) != 0U) {
                        PaintBlocks(creationDefinition, zoning, zoneData, zoneType, baseCells);
                    }

                    if ((zoning.m_Flags & ZoningFlags.Marquee) != 0U) {
                        MarqueeBlocks(creationDefinition, zoning, zoneData, zoneType, baseCells);
                    }
                }
            }

            private void MarqueeBlocks(CreationDefinition creationDefinition, Zoning               zoning, ZoneData zoneData,
                                       ZoneType           zoneType,           NativeList<BaseCell> baseCells) {
                var marqueeIterator = new MarqueeIterator(
                    MathUtils.Bounds(zoning.m_Position.xz),
                    zoning.m_Position.xz,
                    zoneType,
                    (zoning.m_Flags & ZoningFlags.Overwrite) > 0U,
                    m_BlockCLook,
                    m_CellsBLook,
                    m_ZonedCells,
                    m_ZonedBlocks
                );

                m_ZoneSearchTree.Iterate(ref marqueeIterator);
            }

            private void PaintBlocks(CreationDefinition creationDefinition, Zoning               zoning, ZoneData zoneData,
                                     ZoneType           zoneType,           NativeList<BaseCell> baseCells) {
                foreach (var baseCell in baseCells) {
                    var block    = m_BlockCLook[baseCell.m_Block];
                    var cellData = default(CellData);
                    cellData.m_Location = baseCell.m_Location;
                    cellData.m_ZoneType = zoneType;

                    if (!math.all((cellData.m_Location >= 0) & (cellData.m_Location < block.m_Size))) {
                        continue;
                    }

                    var cell = m_CellsBLook[baseCell.m_Block][cellData.m_Location.y * block.m_Size.x + cellData.m_Location.x];

                    // Skip if the cell is not visible OR if the Shared flag is NOT set.
                    if ((cell.m_State & CellFlags.Visible) == CellFlags.None ||
                        (cell.m_State & CellFlags.Shared) == CellFlags.None) {
                        continue;
                    }

                    if (!m_ZonedCells.TryGetFirstValue(baseCell.m_Block, out _, out _)) {
                        m_ZonedBlocks.Add(in baseCell.m_Block);
                    }

                    if ((zoning.m_Flags & ZoningFlags.Overwrite) == 0U && !cell.m_Zone.Equals(ZoneType.None)) {
                        cellData.m_ZoneType = cell.m_Zone;
                    }

                    m_ZonedCells.Add(baseCell.m_Block, cellData);
                }
            }

            private void FloodFillBlocks(CreationDefinition creationDefinition, Zoning               zoning, ZoneData zoneData,
                                         ZoneType           zoneType,           NativeList<BaseCell> baseCells) {
                var cellLocationsHash = new NativeParallelHashSet<int>(1000, Allocator.Temp);
                var cellLocationsList = new NativeList<int2>(1000, Allocator.Temp);

                foreach (var baseCell in baseCells) {
                    var block            = m_BlockCLook[baseCell.m_Block];
                    var cellsBuffer      = m_CellsBLook[baseCell.m_Block];
                    var baseCellLocation = baseCell.m_Location;
                    var cell             = cellsBuffer[baseCellLocation.y * block.m_Size.x + baseCellLocation.x];

                    var floodFillIterator = new FloodFillIterator(
                        block,
                        cell.m_State & (CellFlags.Visible | CellFlags.Occupied),
                        cell.m_Zone,
                        zoneType,
                        (zoning.m_Flags & ZoningFlags.Overwrite) > 0U,
                        m_BlockCLook,
                        m_CellsBLook,
                        m_ZonedCells,
                        m_ZonedBlocks
                    );

                    cellLocationsHash.Add(PackToInt(baseCellLocation));
                    cellLocationsList.Add(in baseCellLocation);

                    var j = 0;
                    while (j < cellLocationsList.Length) {
                        var cellLocation             = cellLocationsList[j++];
                        floodFillIterator.Position   = ZoneUtils.GetCellPosition(block, cellLocation).xz;
                        floodFillIterator.FoundCells = 0;

                        m_ZoneSearchTree.Iterate(ref floodFillIterator);

                        if (floodFillIterator.FoundCells == 0) {
                            continue;
                        }

                        var int2 = cellLocation;
                        var int3 = cellLocation;
                        var int4 = cellLocation;
                        var int5 = cellLocation;
                        int2.x--;
                        int3.y--;
                        int4.x++;
                        int5.y++;
                        if (cellLocationsHash.Add(PackToInt(int2))) {
                            cellLocationsList.Add(in int2);
                        }

                        if (cellLocationsHash.Add(PackToInt(int3))) {
                            cellLocationsList.Add(in int3);
                        }

                        if (cellLocationsHash.Add(PackToInt(int4))) {
                            cellLocationsList.Add(in int4);
                        }

                        if (cellLocationsHash.Add(PackToInt(int5))) {
                            cellLocationsList.Add(in int5);
                        }
                    }

                    cellLocationsHash.Clear();
                    cellLocationsList.Clear();
                }
            }

            private static int PackToInt(int2 cellIndex) { return (cellIndex.y << 16) | (cellIndex.x & 65535); }

            private void AddCells(Line2.Segment line, NativeList<BaseCell> baseCells) {
                var baseLineIterator = new BaseLineIterator(
                    line,
                    m_BlockCLook,
                    m_ParcelOwnerCLook,
                    m_CellsBLook,
                    baseCells
                );
                m_ZoneSearchTree.Iterate(ref baseLineIterator);
            }

            private struct MarqueeIterator : INativeQuadTreeIterator<Entity, Bounds2> {
                private Bounds2 m_Bounds;
                private Quad2 m_Quad;
                private ZoneType m_NewZoneType;
                private bool m_Overwrite;
                private ComponentLookup<Block> m_BlockData;
                private BufferLookup<Cell> m_CellBLook;
                private NativeParallelMultiHashMap<Entity, CellData> m_ZonedCells;
                private NativeList<Entity> m_ZonedBlocks;

                public MarqueeIterator(Bounds2 bounds, Quad2 quad, ZoneType newZoneType, bool overwrite, ComponentLookup<Block> blockData, BufferLookup<Cell> cellBLook, NativeParallelMultiHashMap<Entity, CellData> zonedCells, NativeList<Entity> zonedBlocks) {
                    m_Bounds = bounds;
                    m_Quad = quad;
                    m_NewZoneType = newZoneType;
                    m_Overwrite = overwrite;
                    m_BlockData = blockData;
                    m_CellBLook = cellBLook;
                    m_ZonedCells = zonedCells;
                    m_ZonedBlocks = zonedBlocks;
                }

                public bool               Intersect(Bounds2 bounds) { return MathUtils.Intersect(bounds, m_Bounds); }

                public void Iterate(Bounds2 bounds, Entity blockEntity) {
                    if (!MathUtils.Intersect(bounds, m_Bounds)) {
                        return;
                    }

                    var block        = m_BlockData[blockEntity];
                    var blockCorners = ZoneUtils.CalculateCorners(block);

                    if (!MathUtils.Intersect(m_Quad, blockCorners)) {
                        return;
                    }

                    var cellBuffer = m_CellBLook[blockEntity];

                    var cellData = default(CellData);
                    cellData.m_ZoneType   = m_NewZoneType;
                    cellData.m_Location.y = 0;

                    while (cellData.m_Location.y < block.m_Size.y) {
                        cellData.m_Location.x = 0;

                        while (cellData.m_Location.x < block.m_Size.x) {
                            var num  = cellData.m_Location.y * block.m_Size.x + cellData.m_Location.x;
                            var cell = cellBuffer[num];

                            if ((cell.m_State & CellFlags.Visible) != CellFlags.None && (cell.m_State & CellFlags.Shared) == CellFlags.None) {
                                var cellPosition = ZoneUtils.GetCellPosition(block, cellData.m_Location);
                                if (MathUtils.Intersect(m_Quad, cellPosition.xz) &&
                                    m_Overwrite | cell.m_Zone.Equals(ZoneType.None)) {
                                    CellData                                   cellData2;
                                    NativeParallelMultiHashMapIterator<Entity> nativeParallelMultiHashMapIterator;
                                    if (!m_ZonedCells.TryGetFirstValue(
                                            blockEntity, out cellData2, out nativeParallelMultiHashMapIterator)) {
                                        m_ZonedBlocks.Add(in blockEntity);
                                    }

                                    m_ZonedCells.Add(blockEntity, cellData);
                                }
                            }

                            cellData.m_Location.x = cellData.m_Location.x + 1;
                        }

                        cellData.m_Location.y = cellData.m_Location.y + 1;
                    }
                }
            }

            private struct BaseLineIterator : INativeQuadTreeIterator<Entity, Bounds2> {
                private Line2.Segment          m_Line;
                private ComponentLookup<Block> m_BlockCLook;
                private ComponentLookup<ParcelOwner> m_ParcelOwnerCLook;
                private BufferLookup<Cell>     m_CellsBLook;
                private NativeList<BaseCell>   m_BaseCellsList;

                public BaseLineIterator(Line2.Segment line, ComponentLookup<Block> blockCLook, ComponentLookup<ParcelOwner> parcelOwnerCLook, BufferLookup<Cell> cellsBLook, NativeList<BaseCell> baseCellsList) {
                    m_Line = line;
                    m_BlockCLook = blockCLook;
                    m_ParcelOwnerCLook = parcelOwnerCLook;
                    m_CellsBLook = cellsBLook;
                    m_BaseCellsList = baseCellsList;
                }

                public bool Intersect(Bounds2 bounds) { return MathUtils.Intersect(bounds, m_Line, out _); }

                public void Iterate(Bounds2 bounds, Entity blockEntity) {
                    if (!MathUtils.Intersect(bounds, m_Line, out _)) {
                        return;
                    }

                    if (!m_ParcelOwnerCLook.HasComponent(blockEntity)) {
                        return;
                    }

                    var block      = m_BlockCLook[blockEntity];
                    var cellIndex  = ZoneUtils.GetCellIndex(block, m_Line.a);
                    var cellIndex2 = ZoneUtils.GetCellIndex(block, m_Line.b);
                    var @int       = math.max(math.min(cellIndex, cellIndex2), 0);
                    var int2       = math.min(math.max(cellIndex, cellIndex2), block.m_Size - 1);

                    if (!math.all(int2 >= @int)) {
                        return;
                    }

                    var dynamicBuffer = m_CellsBLook[blockEntity];
                    var quad          = ZoneUtils.CalculateCorners(block);
                    var float2        = new float2(1f) / block.m_Size;
                    var quad2         = default(Quad2);

                    quad2.a = math.lerp(quad.a, quad.d, @int.y * float2.y);
                    quad2.b = math.lerp(quad.b, quad.c, @int.y * float2.y);

                    for (var i = @int.y; i <= int2.y; i++) {
                        quad2.d = math.lerp(quad.a, quad.d, (i + 1) * float2.y);
                        quad2.c = math.lerp(quad.b, quad.c, (i + 1) * float2.y);
                        var quad3 = default(Quad2);
                        quad3.a = math.lerp(quad2.a, quad2.b, @int.x * float2.x);
                        quad3.d = math.lerp(quad2.d, quad2.c, @int.x * float2.x);
                        for (var j = @int.x; j <= int2.x; j++) {
                            quad3.b = math.lerp(quad2.a, quad2.b, (j + 1) * float2.x);
                            quad3.c = math.lerp(quad2.d, quad2.c, (j + 1) * float2.x);

                            if ((dynamicBuffer[i * block.m_Size.x + j].m_State & CellFlags.Visible) != CellFlags.None &&
                                MathUtils.Intersect(quad3, m_Line, out _)) {
                                var baseCell = default(BaseCell);
                                baseCell.m_Block    = blockEntity;
                                baseCell.m_Location = new int2(j, i);
                                m_BaseCellsList.Add(in baseCell);
                            }

                            quad3.a = quad3.b;
                            quad3.d = quad3.c;
                        }

                        quad2.a = quad2.d;
                        quad2.b = quad2.c;
                    }
                }
            }

            private struct FloodFillIterator : INativeQuadTreeIterator<Entity, Bounds2> {
                public  float2                                       Position;
                public  int                                          FoundCells;
                private Block                                        m_BaseBlock;
                private CellFlags                                    m_StateMask;
                private ZoneType                                     m_OldZoneType;
                private ZoneType                                     m_NewZoneType;
                private bool                                         m_Overwrite;
                private ComponentLookup<Block>                       m_BlockCLook;
                private BufferLookup<Cell>                           m_CellsBLook;
                private NativeParallelMultiHashMap<Entity, CellData> m_ZonedCells;
                private NativeList<Entity>                           m_ZonedBlocks;

                public FloodFillIterator(Block baseBlock, CellFlags stateMask, ZoneType oldZoneType, ZoneType newZoneType,
                                         bool overwrite, ComponentLookup<Block> blockCLook, BufferLookup<Cell> cellsBLook,
                                         NativeParallelMultiHashMap<Entity, CellData> zonedCells,
                                         NativeList<Entity> zonedBlocks) {
                    Position      = default;
                    FoundCells    = 0;
                    m_BaseBlock   = baseBlock;
                    m_StateMask   = stateMask;
                    m_OldZoneType = oldZoneType;
                    m_NewZoneType = newZoneType;
                    m_Overwrite   = overwrite;
                    m_BlockCLook  = blockCLook;
                    m_CellsBLook  = cellsBLook;
                    m_ZonedCells  = zonedCells;
                    m_ZonedBlocks = zonedBlocks;
                }

                public bool Intersect(Bounds2 bounds) { return MathUtils.Intersect(bounds, Position); }

                public void Iterate(Bounds2 bounds, Entity blockEntity) {
                    // Quick reject if the quad node doesn't cover the target position
                    if (!MathUtils.Intersect(bounds, Position)) {
                        return;
                    }

                    var block = m_BlockCLook[blockEntity];

                    // Compute cell index within the block from world position
                    var cellData = default(CellData);
                    cellData.m_Location = ZoneUtils.GetCellIndex(block, Position);
                    cellData.m_ZoneType = m_NewZoneType;

                    // Ensure index is inside the block
                    if (!math.all((cellData.m_Location >= 0) & (cellData.m_Location < block.m_Size))) {
                        return;
                    }

                    // Ensure it's the same cell as the base cell
                    if (!m_BaseBlock.Equals(block)) {
                        return;
                    }

                    // Get the cell 
                    var cellBuffer = m_CellsBLook[blockEntity];
                    var flatIndex  = cellData.m_Location.y * block.m_Size.x + cellData.m_Location.x;
                    var cell       = cellBuffer[flatIndex];

                    // Check state mask and original zone
                    var stateVisibleOccupied = cell.m_State & (CellFlags.Visible | CellFlags.Occupied);
                    if (!((stateVisibleOccupied == m_StateMask) & cell.m_Zone.Equals(m_OldZoneType))) {
                        return;
                    }

                    // If this is the first zoned cell for this block, register the block
                    if (!m_ZonedCells.TryGetFirstValue(blockEntity, out _, out var iterator)) {
                        m_ZonedBlocks.Add(in blockEntity);
                    }

                    // If not overwriting and the cell already has a zone, keep the existing zone
                    if (!m_Overwrite && !cell.m_Zone.Equals(ZoneType.None)) {
                        cellData.m_ZoneType = cell.m_Zone;
                    }

                    // Record the cell to apply zoning later and count it
                    m_ZonedCells.Add(blockEntity, cellData);
                    FoundCells++;
                }
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct CreateBlocksJob : IJobParallelForDefer {
            [ReadOnly] private ComponentLookup<Block>                       m_BlockData;
            [ReadOnly] private ComponentLookup<PrefabRef>                   m_PrefabRefData;
            [ReadOnly] private ComponentLookup<ZoneBlockData>               m_ZoneBlockDataData;
            [ReadOnly] private BufferLookup<Cell>                           m_Cells;
            [ReadOnly] private NativeParallelMultiHashMap<Entity, CellData> m_ZonedCells;
            [ReadOnly] private NativeArray<Entity>                          m_ZonedBlocks;
            private            EntityCommandBuffer.ParallelWriter           m_CommandBuffer;

            public CreateBlocksJob(ComponentLookup<Block> blockData, ComponentLookup<PrefabRef> prefabRefData,
                                   ComponentLookup<ZoneBlockData> zoneBlockDataData, BufferLookup<Cell> cells,
                                   NativeParallelMultiHashMap<Entity, CellData> zonedCells, NativeArray<Entity> zonedBlocks,
                                   EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_BlockData         = blockData;
                m_PrefabRefData     = prefabRefData;
                m_ZoneBlockDataData = zoneBlockDataData;
                m_Cells             = cells;
                m_ZonedCells        = zonedCells;
                m_ZonedBlocks       = zonedBlocks;
                m_CommandBuffer     = commandBuffer;
            }

            public void Execute(int index) {
                var entity        = m_ZonedBlocks[index];
                var block         = m_BlockData[entity];
                var prefabRef     = m_PrefabRefData[entity];
                var dynamicBuffer = m_Cells[entity];
                var zoneBlockData = m_ZoneBlockDataData[prefabRef.m_Prefab];
                var entity2       = m_CommandBuffer.CreateEntity(index, zoneBlockData.m_Archetype);

                m_CommandBuffer.SetComponent(index, entity2, prefabRef);
                m_CommandBuffer.SetComponent(index, entity2, block);
                var dynamicBuffer2 = m_CommandBuffer.SetBuffer<Cell>(index, entity2);
                m_CommandBuffer.AddComponent(
                    index, entity2, new Temp {
                    m_Original = entity,
                    });
                m_CommandBuffer.AddComponent(index, entity, default(Hidden));
                m_CommandBuffer.AddComponent(index, entity, default(BatchesUpdated));
                
                foreach (var t in dynamicBuffer) dynamicBuffer2.Add(t);

                CellData                                   cellData;
                NativeParallelMultiHashMapIterator<Entity> nativeParallelMultiHashMapIterator;
                if (!m_ZonedCells.TryGetFirstValue(entity, out cellData, out nativeParallelMultiHashMapIterator)) {
                    return;
                }

                do {
                    var num  = cellData.m_Location.y * block.m_Size.x + cellData.m_Location.x;
                    var cell = dynamicBuffer2[num];
                    cell.m_State        |= CellFlags.Selected;
                    cell.m_Zone         =  cellData.m_ZoneType;
                    dynamicBuffer2[num] =  cell;
                } while (m_ZonedCells.TryGetNextValue(out cellData, ref nativeParallelMultiHashMapIterator));
            }
        }
    }
}