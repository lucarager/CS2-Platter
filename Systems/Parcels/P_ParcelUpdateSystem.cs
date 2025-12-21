// <copyright file="P_ParcelUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for updating Parcel and Cell data when a parcel is placed, moved, or deleted.
    /// </summary>
    public partial class P_ParcelUpdateSystem : PlatterGameSystemBase {
        // Hardcoded block size minimums
        // This is needed because the game requires
        //   - blocks to be at least 2 wide to not be automatically set to occupied (even though LOTS can be 1 wide, like for townhomes)
        //   - blocks to be 6 deep so that the rendering / culling system does not get confused and not render cells on certain smaller parcel sizes
        public const int                  MinBlockDepth = 6;
        public const int                  MinBlockWidth = 2;
        private      EntityQuery          m_DeletedQuery;
        private      EntityQuery          m_UpdatedQuery;
        private      ModificationBarrier2 m_ModificationBarrier2;
        private      PrefixedLogger       m_Log;

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
            var deleteParcelJobHandle = new DeleteParcelJob
            {
                m_EntityTypeHandle         = SystemAPI.GetEntityTypeHandle(),
                m_SubBlockBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ParcelSubBlock>(),
                m_CellLookup               = SystemAPI.GetBufferLookup<Cell>(),
                m_CommandBuffer            = m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter(),
            }.Schedule(m_DeletedQuery, Dependency);

            m_ModificationBarrier2.AddJobHandleForProducer(deleteParcelJobHandle);

            // Job to initialize a parcel after it has been replaced from a placeholder
            var updateParcelJobHandle = new UpdateParcelJob
            {
                m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                m_ParcelTypeHandle               = SystemAPI.GetComponentTypeHandle<Parcel>(),
                m_PrefabRefTypeHandle            = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                m_TransformTypeHandle            = SystemAPI.GetComponentTypeHandle<Transform>(),
                m_ParcelSubBlockBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ParcelSubBlock>(),
                m_ParcelDataLookup               = SystemAPI.GetComponentLookup<ParcelData>(),
                m_ZoneBlockDataLookup            = SystemAPI.GetComponentLookup<ZoneBlockData>(),
                m_CommandBuffer                  = m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter(),
            }.Schedule(m_UpdatedQuery, Dependency);

            m_ModificationBarrier2.AddJobHandleForProducer(updateParcelJobHandle);

            Dependency = JobHandle.CombineDependencies(deleteParcelJobHandle, updateParcelJobHandle);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeleteParcelJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ParcelSubBlock>   m_SubBlockBufferTypeHandle;
            [ReadOnly] public required BufferLookup<Cell>                 m_CellLookup;
            public required            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

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
                            cell.m_Zone   = ZoneType.None;
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
            [ReadOnly] public required EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Parcel>        m_ParcelTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>     m_PrefabRefTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Transform>     m_TransformTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ParcelSubBlock>   m_ParcelSubBlockBufferTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData>        m_ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<ZoneBlockData>     m_ZoneBlockDataLookup;
            public required            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray         = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray         = chunk.GetNativeArray(ref m_ParcelTypeHandle);
                var prefabRefArray      = chunk.GetNativeArray(ref m_PrefabRefTypeHandle);
                var transformArray      = chunk.GetNativeArray(ref m_TransformTypeHandle);
                var subBlockBufferArray = chunk.GetBufferAccessor(ref m_ParcelSubBlockBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var    parcelEntity       = entityArray[i];
                    var    parcel             = parcelArray[i];
                    var    prefabRef          = prefabRefArray[i];
                    var    transform          = transformArray[i];
                    var    subBlockBuffer     = subBlockBufferArray[i];
                    var    parcelData         = m_ParcelDataLookup[prefabRef.m_Prefab];
                    var    isUpdatingExisting = subBlockBuffer.Length > 0;
                    var    blockCenter      = ParcelGeometryUtils.GetBlockCenter(parcelData.m_LotSize);
                    var    parcelBlockWidth = math.max(MinBlockWidth, parcelData.m_LotSize.x);
                    var    parcelBlockDepth = math.max(MinBlockDepth, parcelData.m_LotSize.y);
                    var block = new Block {
                        m_Position  = ParcelUtils.GetWorldPosition(transform, blockCenter),
                        m_Direction = math.mul(transform.m_Rotation, new float3(0f, 0f, 1f)).xz,
                        m_Size      = new int2(parcelBlockWidth, parcelBlockDepth),
                    };
                    var    zoneBlockData = m_ZoneBlockDataLookup[parcelData.m_ZoneBlockPrefab];
                    Entity blockEntity;

                    if (isUpdatingExisting) {
                        blockEntity = subBlockBuffer[0].m_SubBlock;
                        m_CommandBuffer.SetComponent(index, blockEntity, new PrefabRef(parcelData.m_ZoneBlockPrefab));
                        m_CommandBuffer.SetComponent(index, blockEntity, default(CurvePosition));
                        m_CommandBuffer.SetComponent(index, blockEntity, block);
                        m_CommandBuffer.SetComponent(index, blockEntity, new BuildOrder { m_Order = 0 });
                        m_CommandBuffer.SetComponent(
                            index,
                            blockEntity,
                            new ValidArea { m_Area = new int4(0, parcelData.m_LotSize.x, 0, parcelData.m_LotSize.y) });
                        m_CommandBuffer.AddComponent(index, blockEntity, default(Updated));
                        continue;
                    }

                    blockEntity = m_CommandBuffer.CreateEntity(index, zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent(index, blockEntity, new PrefabRef(parcelData.m_ZoneBlockPrefab));
                    m_CommandBuffer.SetComponent(index, blockEntity, block);
                    m_CommandBuffer.SetComponent(index, blockEntity, default(CurvePosition));
                    m_CommandBuffer.SetComponent(index, blockEntity, new BuildOrder { m_Order = 0 });
                    m_CommandBuffer.SetComponent(
                        index,
                        blockEntity,
                        new ValidArea { m_Area = new int4(0, parcelData.m_LotSize.x, 0, parcelData.m_LotSize.y) });
                    m_CommandBuffer.AddComponent(index, blockEntity, new ParcelOwner(parcelEntity));
                    m_CommandBuffer.AddComponent<Initialized>(index, parcelEntity);
                    var cellBuffer = m_CommandBuffer.SetBuffer<Cell>(index, blockEntity);


                    // Set all cells beyond the depth or width limit to blocked.
                    // This is due to a graphical limitation of the vanilla game that needs the depth to be 6 to fully render cells
                    // as well as the fact that a block has a minimum width of 2 to be valid.

                    // "Visible" flag is usually added by the game's cell overlap systems
                    // By adding it manually, we ensure that the game takes our custom blocks as a priority (given the 0 build order)
                    // otherwise it always prioritizes visible cells first, which would be the ones already existing on roads
                    // This may break in the future should the logic in CellOverlapJobs.cs change

                    // Set all cells outside of parcel bounds to occupied
                    for (var row = 0; row < block.m_Size.y; row++)
                    for (var col = 0; col < block.m_Size.x; col++) {
                        var isBlocked = col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y;
                        cellBuffer.Add(
                            new Cell
                            {
                                m_Zone  = isBlocked ? ZoneType.None : parcel.m_PreZoneType,
                                m_State = isBlocked ? CellFlags.Blocked : CellFlags.Visible,
                            });
                    }
                }
            }
        }
    }
}