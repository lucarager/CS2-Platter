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

    internal partial class P_ParcelSubBlockDeserializeSystem : GameSystemBase {
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
                m_EntityType            = SystemAPI.GetEntityTypeHandle(),
                m_ParcelOwnerType       = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                m_SubBlocksBufferLookup = SystemAPI.GetBufferLookup<ParcelSubBlock>(),
            }.Schedule(m_Query, Dependency);

            Dependency = deserializeJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeserializeJob : IJobChunk {
            [ReadOnly]
            public required EntityTypeHandle m_EntityType;

            [ReadOnly]
            public required ComponentTypeHandle<ParcelOwner> m_ParcelOwnerType;

            public required BufferLookup<ParcelSubBlock> m_SubBlocksBufferLookup;

            public void Execute(in ArchetypeChunk chunk) {
                var entityArray      = chunk.GetNativeArray(m_EntityType);
                var parcelOwnerArray = chunk.GetNativeArray(ref m_ParcelOwnerType);
                for (var i = 0; i < parcelOwnerArray.Length; i++) {
                    var blockEntity = entityArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    m_SubBlocksBufferLookup[parcelOwner.m_Owner].Add(new ParcelSubBlock(blockEntity));
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) { Execute(in chunk); }
        }
    }
}