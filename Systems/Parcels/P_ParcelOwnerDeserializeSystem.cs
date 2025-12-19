// <copyright file="P_ParcelOwnerDeserializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System that deserializes Parcel blocks.
    /// It adds the Owner component back based on the ParcelOwner and Parcel data.
    /// See <see cref="P_ParcelOwnerSerializeSystem"/> for the other half of the process.
    /// 
    /// We could keep this in the deserialized state, but some systems might freak out when deserializing a parcel 
    /// with an Owner component (usually, just road's blocks have that in vanilla).
    /// </summary>
    internal partial class P_ParcelOwnerDeserializeSystem : PlatterGameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Block, ParcelOwner>()
                .WithNone<Deleted, Owner, Temp>()
                .Build();

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var serializeJobHandle = new SerializeJob {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ParcelOwnerType = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                ParcelLookup = SystemAPI.GetComponentLookup<Parcel>(true),
                CommandBuffer = ecb.AsParallelWriter(),
            }.ScheduleParallel(m_Query, Dependency);

            serializeJobHandle.Complete();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            Dependency = serializeJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct SerializeJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle EntityType;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> ParcelOwnerType;
            [ReadOnly] public required ComponentLookup<Parcel> ParcelLookup;
            public required EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(EntityType);
                var parcelOwnerArray = chunk.GetNativeArray(ref ParcelOwnerType);

                for (var i = 0; i < entityArray.Length; i++) {
                    var blockEntity = entityArray[i];
                    var parcelOwner = parcelOwnerArray[i];

                    if (ParcelLookup.TryGetComponent(parcelOwner.m_Owner, out var parcel) && parcel.m_RoadEdge != Entity.Null) {
                        CommandBuffer.AddComponent(unfilteredChunkIndex, blockEntity, new Owner {
                            m_Owner = parcel.m_RoadEdge,
                        });
                    }
                }
            }
        }
    }
}