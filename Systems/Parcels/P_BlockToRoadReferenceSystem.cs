// <copyright file="P_BlockToRoadReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Citizens;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for adding the "Owner" component to a block when a parcel and road get connected.
    /// This is what marks a block as a valid spawn location to the vanilla ZoneSpawnSystem.
    /// </summary>
    public partial class P_BlockToRoadReferenceSystem : GameSystemBase {
        private EntityQuery m_ParcelUpdatedQuery;
        private ModificationBarrier5 m_ModificationBarrier5;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_BlockToRoadReferenceSystem));
            m_Log.Debug("OnCreate()");

            m_ModificationBarrier5 = World.GetOrCreateSystemManaged<ModificationBarrier5>();

            m_ParcelUpdatedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Parcel, Initialized>()
                                            .WithAny<Updated>()
                                            .WithNone<Temp, Deleted>()
                                            .Build();

            RequireForUpdate(m_ParcelUpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate() -- Updating Parcel->Block->Road ownership references");

            var updateBlockOwnerJobHandle = new UpdateBlockOwnerJob
            {
                m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                m_ParcelTypeHandle               = SystemAPI.GetComponentTypeHandle<Parcel>(true),
                m_ParcelSubBlockBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ParcelSubBlock>(true),
                m_ParcelSpawnableLookup          = SystemAPI.GetComponentLookup<ParcelSpawnable>(true),
                m_CurvePositionLookup            = SystemAPI.GetComponentLookup<CurvePosition>(),
                m_OwnerLookup                    = SystemAPI.GetComponentLookup<Owner>(),
                m_CommandBuffer                  = m_ModificationBarrier5.CreateCommandBuffer().AsParallelWriter(),
            }.Schedule(m_ParcelUpdatedQuery, Dependency);

            m_ModificationBarrier5.AddJobHandleForProducer(updateBlockOwnerJobHandle);

            Dependency = updateBlockOwnerJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateBlockOwnerJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Parcel>        m_ParcelTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ParcelSubBlock>   m_ParcelSubBlockBufferTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelSpawnable>   m_ParcelSpawnableLookup;
            public required            ComponentLookup<CurvePosition>     m_CurvePositionLookup;
            public required            ComponentLookup<Owner>             m_OwnerLookup;
            public required            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray         = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray         = chunk.GetNativeArray(ref m_ParcelTypeHandle);
                var subBlockBufferArray = chunk.GetBufferAccessor(ref m_ParcelSubBlockBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var parcelEntity   = entityArray[i];
                    var parcel         = parcelArray[i];
                    var subBlockBuffer = subBlockBufferArray[i];
                    var allowSpawning  = m_ParcelSpawnableLookup.HasComponent(parcelEntity);

                    foreach (var subBlock in subBlockBuffer) {
                        var blockEntity = subBlock.m_SubBlock;

                        // Update the block's curve position
                        if (m_CurvePositionLookup.TryGetComponent(blockEntity, out var curvePosition) &&
                            (
                                !Mathf.Approximately(curvePosition.m_CurvePosition.x, parcel.m_CurvePosition) ||
                                !Mathf.Approximately(curvePosition.m_CurvePosition.y, parcel.m_CurvePosition)
                            )) {
                            curvePosition.m_CurvePosition      = parcel.m_CurvePosition;
                            m_CurvePositionLookup[blockEntity] = curvePosition;
                        }

                        // Mark the road edge as updated
                        //if (parcel.m_RoadEdge != Entity.Null) {
                        //    m_CommandBuffer.AddComponent<Updated>(unfilteredChunkIndex, parcel.m_RoadEdge);
                        //}

                        if (parcel.m_RoadEdge == Entity.Null || !allowSpawning) {
                            // We need to make sure that the block actually NEVER has a null owner
                            // Otherwise the game can crash when systems try to retrieve the Edge.
                            // Tsk tsk paradox for not considering an edge case that is entirely a modders thing and doesn't happen in vanilla ;)

                            // Also note that this is how we prevent a parcel from spawning buildings - as long as no Edge is set as owner,
                            // the spawn system won't pick up this block.
                            m_CommandBuffer.RemoveComponent<Owner>(unfilteredChunkIndex, blockEntity);
                        } else {
                            if (m_OwnerLookup.TryGetComponent(blockEntity, out var owner)) {
                                owner.m_Owner              = parcel.m_RoadEdge;
                                m_OwnerLookup[blockEntity] = owner;
                            } else {
                                m_CommandBuffer.AddComponent(unfilteredChunkIndex, blockEntity, new Owner(parcel.m_RoadEdge));
                            }
                        }
                    }
                }
            }
        }
    }
}