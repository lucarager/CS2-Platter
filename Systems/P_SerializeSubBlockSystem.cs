// <copyright file="P_SerializeSubBlockSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Logging;
    using Game;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    internal partial class P_SerializeParcelSubBlockSystem : GameSystemBase {
        private EntityQuery m_Query;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(P_SerializeParcelSubBlockSystem));
            m_Log.Debug($"OnCreate()");

            m_Query = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Block>(),
                ComponentType.ReadOnly<ParcelOwner>(),
            });
            base.RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate()");

            var deserializeJobHandle = new DeserializeJob(
                entityType: SystemAPI.GetEntityTypeHandle(),
                parcelOwnerType: SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                subBlocksBufferLookup: SystemAPI.GetBufferLookup<ParcelSubBlock>(false)
            ).Schedule(m_Query, base.Dependency);

            base.Dependency = deserializeJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeserializeJob : IJobChunk {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;
            [ReadOnly]
            public ComponentTypeHandle<ParcelOwner> m_ParcelOwnerType;
            public BufferLookup<ParcelSubBlock> m_SubBlocksBufferLookup;

            public DeserializeJob(EntityTypeHandle entityType, ComponentTypeHandle<ParcelOwner> parcelOwnerType, BufferLookup<ParcelSubBlock> subBlocksBufferLookup) {
                m_EntityType = entityType;
                m_ParcelOwnerType = parcelOwnerType;
                m_SubBlocksBufferLookup = subBlocksBufferLookup;
            }

            public void Execute(in ArchetypeChunk chunk) {
                var entityArray = chunk.GetNativeArray(m_EntityType);
                var parcelOwnerArray = chunk.GetNativeArray<ParcelOwner>(ref m_ParcelOwnerType);
                for (var i = 0; i < parcelOwnerArray.Length; i++) {
                    var blockEntity = entityArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    m_SubBlocksBufferLookup[parcelOwner.m_Owner].Add(new ParcelSubBlock(blockEntity));
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                Execute(in chunk);
            }
        }
    }
}
