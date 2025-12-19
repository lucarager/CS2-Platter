// <copyright file="P_ParcelSerializeSystem.cs" company="Luca Rager">
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
    /// System that serializes Parcel blocks.
    /// It removes the Owner component from Parcel blocks.
    /// See <see cref="P_ParcelOwnerDeserializeSystem"/> for the other half of the process.
    /// 
    /// We could keep this in the deserialized state, but some systems might freak out when deserializing a parcel 
    /// with an Owner component (usually, just road's blocks have that in vanilla).
    /// </summary>
    internal partial class P_ParcelOwnerSerializeSystem : PlatterGameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Block, ParcelOwner, Owner>()
                .WithNone<Deleted, Temp>()
                .Build();

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var serializeJobHandle = new SerializeJob {
                EntityType = SystemAPI.GetEntityTypeHandle(),
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
            public required EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(EntityType);

                for (var i = 0; i < entityArray.Length; i++) {
                    var blockEntity = entityArray[i];

                    CommandBuffer.RemoveComponent<Owner>(unfilteredChunkIndex, blockEntity);
                }
            }
        }
    }
}