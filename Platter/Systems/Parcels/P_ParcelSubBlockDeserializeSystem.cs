// <copyright file="P_ParcelSubBlockDeserializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    internal partial class P_ParcelSubBlockDeserializeSystem : PlatterGameSystemBase {
        private EntityQuery    m_Query;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_ParcelSubBlockDeserializeSystem));
            m_Log.Debug("OnCreate()");

            m_Query = GetEntityQuery(
                ComponentType.ReadOnly<Block>(),
                ComponentType.ReadOnly<ParcelOwner>());

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            var deserializeJobHandle = new DeserializeJob {
                EntityType            = SystemAPI.GetEntityTypeHandle(),
                ParcelOwnerType       = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                SubBlocksBufferLookup = SystemAPI.GetBufferLookup<ParcelSubBlock>(),
            }.Schedule(m_Query, Dependency);

            Dependency = deserializeJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeserializeJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                 EntityType;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> ParcelOwnerType;
            public required            BufferLookup<ParcelSubBlock>     SubBlocksBufferLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray      = chunk.GetNativeArray(EntityType);
                var parcelOwnerArray = chunk.GetNativeArray(ref ParcelOwnerType);
                for (var i = 0; i < parcelOwnerArray.Length; i++) {
                    var blockEntity = entityArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    SubBlocksBufferLookup[parcelOwner.m_Owner].Add(new ParcelSubBlock(blockEntity));
                }
            }
        }
    }
}