// <copyright file="P_ParcelUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Components;
    using Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// System responsible for updating Parcel and Cell data when a parcel is placed, moved, or deleted.
    /// </summary>
    public partial class P_ParcelUpdateSystem : GameSystemBase {
        private EntityQuery          m_DeletedQuery;
        private EntityQuery          m_UpdatedQuery;
        private ModificationBarrier2 m_ModificationBarrier2;
        private PrefixedLogger       m_Log;

        // Hardcoded block size minimums
        // This is needed because the game requires
        //   - blocks to be at least 2 wide to not be automatically set to occupied (even though LOTS can be 1 wide, like for townhomes)
        //   - blocks to be 6 deep so that the rendering / culling system does not get confused and not render cells on certain smaller parcel sizes
        public const int MinBlockDepth = 6;
        public const int MinBlockWidth = 2;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelUpdateSystem));
            m_Log.Debug("OnCreate()");

            // Retrieve Systems
            m_ModificationBarrier2 = World.GetOrCreateSystemManaged<ModificationBarrier2>();

            // Queries
            m_UpdatedQuery = SystemAPI.QueryBuilder()
                                      // Uninitialized parcels, that just got swapped from being a placeholder
                                      .WithAllRW<Parcel>()
                                      .WithNone<Initialized>()
                                      .WithNone<Temp, ParcelPlaceholder>()
                                      .AddAdditionalQuery()
                                      // Existing parcels that got updated
                                      .WithAllRW<Parcel>()
                                      .WithAll<Updated, Initialized>()
                                      .WithNone<Temp, ParcelPlaceholder>()
                                      .Build();

            m_DeletedQuery = SystemAPI.QueryBuilder()
                                      .WithAllRW<Parcel>()
                                      .WithAll<Deleted>()
                                      .WithNone<Temp, ParcelPlaceholder>()
                                      .Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Job to delete parcels marked for deletion
            var deleteParcelJobHandle = new DeleteParcelJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetBufferTypeHandle<ParcelSubBlock>(),
                SystemAPI.GetBufferLookup<Cell>(),
                m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter()
            ).Schedule(m_DeletedQuery, Dependency);

            m_ModificationBarrier2.AddJobHandleForProducer(deleteParcelJobHandle);

            // Job to initialize a parcel after it has been replaced from a placeholder
            var updateParcelJobHandle = new UpdateParcelJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<Parcel>(),
                SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                SystemAPI.GetComponentTypeHandle<Transform>(),
                SystemAPI.GetBufferTypeHandle<ParcelSubBlock>(),
                SystemAPI.GetComponentLookup<ParcelData>(),
                SystemAPI.GetComponentLookup<ZoneBlockData>(),
                m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter()
            ).Schedule(m_UpdatedQuery, Dependency);

            m_ModificationBarrier2.AddJobHandleForProducer(updateParcelJobHandle);

            Dependency = JobHandle.CombineDependencies(deleteParcelJobHandle, updateParcelJobHandle);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeleteParcelJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] private BufferTypeHandle<ParcelSubBlock>         m_SubBlockBufferTypeHandle;
            [ReadOnly] private BufferLookup<Cell>                 m_CellLookup;
            private            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public DeleteParcelJob(EntityTypeHandle entityTypeHandle, BufferTypeHandle<ParcelSubBlock> subBlocBufferTypeHandle, BufferLookup<Cell> cellLookup,
                                   EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle        = entityTypeHandle;
                m_SubBlockBufferTypeHandle = subBlocBufferTypeHandle;
                m_CellLookup              = cellLookup;
                m_CommandBuffer           = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray         = chunk.GetNativeArray(m_EntityTypeHandle);
                var subBlockBufferArray = chunk.GetBufferAccessor(ref m_SubBlockBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var subBlockBuffer = subBlockBufferArray[i];

                    foreach (var subBlock in subBlockBuffer) {
                        var cellBuffer = m_CellLookup[subBlock.m_SubBlock];

                        // Clear cell zoning before deleting
                        // This prevents shenanigans from the vanilla system that will try to re-zone underlying vanilla cells
                        for (var k = 0; k < cellBuffer.Length; k++) {
                            var cell = cellBuffer[k];
                            cell.m_Zone = ZoneType.None;
                            cellBuffer[k] = cell;
                        }

                        // Mark Blocks for deletion
                        m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, subBlock.m_SubBlock);
                    }
                }
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateParcelJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Parcel>        m_ParcelTypeHandle;
            [ReadOnly] private ComponentTypeHandle<PrefabRef>     m_PrefabRefTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Transform>     m_TransformTypeHandle;
            [ReadOnly] private BufferTypeHandle<ParcelSubBlock>   m_ParcelSubBlockBufferTypeHandle;
            [ReadOnly] private ComponentLookup<ParcelData>        m_ParcelDataLookup;
            [ReadOnly] private ComponentLookup<ZoneBlockData>     m_ZoneBlockDataLookup;
            private            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public UpdateParcelJob(EntityTypeHandle                   entityTypeHandle, ComponentTypeHandle<Parcel> parcelTypeHandle,
                                   ComponentTypeHandle<PrefabRef>     prefabRefTypeHandle,
                                   ComponentTypeHandle<Transform>     transformTypeHandle, BufferTypeHandle<ParcelSubBlock> subBlockBufferTypeHandle,
                                   ComponentLookup<ParcelData>        parcelDataLookup,    ComponentLookup<ZoneBlockData>   zoneBlockDataLookup,
                                   EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle         = entityTypeHandle;
                m_ParcelTypeHandle         = parcelTypeHandle;
                m_PrefabRefTypeHandle      = prefabRefTypeHandle;
                m_TransformTypeHandle      = transformTypeHandle;
                m_ParcelSubBlockBufferTypeHandle = subBlockBufferTypeHandle;
                m_ParcelDataLookup         = parcelDataLookup;
                m_ZoneBlockDataLookup      = zoneBlockDataLookup;
                m_CommandBuffer            = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray         = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray         = chunk.GetNativeArray(ref m_ParcelTypeHandle);
                var prefabRefArray      = chunk.GetNativeArray(ref m_PrefabRefTypeHandle);
                var transformArray      = chunk.GetNativeArray(ref m_TransformTypeHandle);
                var subBlockBufferArray = chunk.GetBufferAccessor(ref m_ParcelSubBlockBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var parcelEntity       = entityArray[i];
                    var parcel             = parcelArray[i];
                    var prefabRef          = prefabRefArray[i];
                    var transform          = transformArray[i];
                    var subBlockBuffer     = subBlockBufferArray[i];
                    var parcelData         = m_ParcelDataLookup[prefabRef.m_Prefab];
                    var isUpdatingExisting = subBlockBuffer.Length > 0;
                    Entity blockEntity;

                    if (isUpdatingExisting) {
                        blockEntity = subBlockBuffer[0].m_SubBlock;
                        m_CommandBuffer.SetComponent(index, blockEntity, new PrefabRef(parcelData.m_ZoneBlockPrefab));
                        m_CommandBuffer.SetComponent(index, blockEntity, default(CurvePosition));
                        m_CommandBuffer.SetComponent(index, blockEntity, new BuildOrder { m_Order = 0 });
                        m_CommandBuffer.AddComponent(index, blockEntity, default(Updated));
                        continue;
                    }

                    var zoneBlockData = m_ZoneBlockDataLookup[parcelData.m_ZoneBlockPrefab];
                    blockEntity = m_CommandBuffer.CreateEntity(index, zoneBlockData.m_Archetype);
                    var parcelBlockWidth = math.max(MinBlockWidth, parcelData.m_LotSize.x);
                    var parcelBlockDepth = math.max(MinBlockDepth, parcelData.m_LotSize.y);
                    var blockCenter      = ParcelGeometryUtils.GetBlockCenter(parcelData.m_LotSize);
                    var block = new Block {
                        m_Position  = ParcelUtils.GetWorldPosition(transform, blockCenter),
                        m_Direction = math.mul(transform.m_Rotation, new float3(0f, 0f, 1f)).xz,
                        m_Size      = new int2(parcelBlockWidth, parcelBlockDepth),
                    };

                    m_CommandBuffer.SetComponent(index, blockEntity, new PrefabRef(parcelData.m_ZoneBlockPrefab));
                    m_CommandBuffer.SetComponent(index, blockEntity, block);
                    m_CommandBuffer.SetComponent(index, blockEntity, default(CurvePosition));
                    m_CommandBuffer.SetComponent(index, blockEntity, new BuildOrder { m_Order = 0 });
                    m_CommandBuffer.AddComponent(index, blockEntity, new ParcelOwner(parcelEntity));
                    m_CommandBuffer.AddComponent<Initialized>(index, parcelEntity);
                    var cellBuffer = m_CommandBuffer.SetBuffer<Cell>(index, blockEntity);

                    // Set all cells outside of parcel bounds to occupied
                    for (var row = 0; row < block.m_Size.y; row++)
                    for (var col = 0; col < block.m_Size.x; col++) {
                        var isBlocked = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;
                        cellBuffer.Add(new Cell {
                            m_Zone  = isBlocked ? ZoneType.None : parcel.m_PreZoneType,
                            m_State = isBlocked ? CellFlags.Blocked : CellFlags.Visible,
                        });
                    }
                }
            }
        }
    }
}