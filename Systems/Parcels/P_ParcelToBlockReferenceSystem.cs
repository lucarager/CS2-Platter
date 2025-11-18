// <copyright file="P_ParcelToBlockReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for linking parcels to their blocks.
    /// </summary>
    public partial class P_ParcelToBlockReferenceSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelBlockQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_ParcelToBlockReferenceSystem));
            m_Log.Debug("OnCreate()");

            m_ParcelBlockQuery = SystemAPI.QueryBuilder()
                                          .WithAll<Block, ParcelOwner>()
                                          .WithAny<Created, Deleted>()
                                          .WithNone<Temp>()
                                          .Build();

            RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            Dependency = new UpdateBlockJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<ParcelOwner>(),
                SystemAPI.GetComponentTypeHandle<Created>(),
                SystemAPI.GetBufferLookup<ParcelSubBlock>()
            ).Schedule(m_ParcelBlockQuery, Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateBlockJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<ParcelOwner> m_ParcelOwnerTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Created>     m_CreatedTypeHandle;
            [ReadOnly] private BufferLookup<ParcelSubBlock>     m_ParcelSubBlockLookup;

            public UpdateBlockJob(EntityTypeHandle             entityTypeHandle,  ComponentTypeHandle<ParcelOwner> parcelOwnerTypeHandle,
                                  ComponentTypeHandle<Created> createdTypeHandle, BufferLookup<ParcelSubBlock>     parcelSubBlockLookup) {
                m_EntityTypeHandle      = entityTypeHandle;
                m_ParcelOwnerTypeHandle = parcelOwnerTypeHandle;
                m_CreatedTypeHandle     = createdTypeHandle;
                m_ParcelSubBlockLookup  = parcelSubBlockLookup;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray      = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelOwnerArray = chunk.GetNativeArray(ref m_ParcelOwnerTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var owner  = parcelOwnerArray[i];
                    if (chunk.Has(ref m_CreatedTypeHandle)) {
                        CollectionUtils.TryAddUniqueValue(m_ParcelSubBlockLookup[owner.m_Owner], new ParcelSubBlock(entity));
                        continue;
                    }

                    CollectionUtils.RemoveValue(m_ParcelSubBlockLookup[owner.m_Owner], new ParcelSubBlock(entity));
                }
            }
        }
    }
}