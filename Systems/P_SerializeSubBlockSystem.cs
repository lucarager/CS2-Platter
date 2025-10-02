// <copyright file="P_SerializeSubBlockSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Zones;
    using Platter.Components;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    internal partial class P_SerializeSubBlockSystem : GameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Query = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Block>(),
                ComponentType.ReadOnly<ParcelOwner>()
            });
            base.RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var subBlockJob = default(SubBlockJob);
            subBlockJob.m_EntityType = SystemAPI.GetEntityTypeHandle();
            subBlockJob.m_ParcelOwnerType = SystemAPI.GetComponentTypeHandle<ParcelOwner>();
            subBlockJob.m_SubBlocksBufferLookup = SystemAPI.GetBufferLookup<SubBlock>();
            base.Dependency = subBlockJob.Schedule(m_Query, base.Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct SubBlockJob : IJobChunk {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<ParcelOwner> m_ParcelOwnerType;
            public BufferLookup<SubBlock> m_SubBlocksBufferLookup;

            public void Execute(in ArchetypeChunk chunk) {
                var entityArray = chunk.GetNativeArray(m_EntityType);
                var parcelOwnerArray = chunk.GetNativeArray<ParcelOwner>(ref m_ParcelOwnerType);
                for (var i = 0; i < parcelOwnerArray.Length; i++) {
                    var blockEntity = entityArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    m_SubBlocksBufferLookup[parcelOwner.m_Owner].Add(new SubBlock(blockEntity));
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                Execute(in chunk);
            }
        }
    }
}
