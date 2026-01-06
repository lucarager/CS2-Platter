// <copyright file="P_ParcelToBlockReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Collections;
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
    /// System responsible for linking parcels to their blocks.
    /// </summary>
    public partial class P_ParcelToBlockReferenceSystem : PlatterGameSystemBase {
        private EntityQuery m_ParcelBlockQuery;
        
        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ParcelBlockQuery = SystemAPI.QueryBuilder()
                                          .WithAll<Block, ParcelOwner>()
                                          .WithAny<Created, Deleted>()
                                          .WithNone<Temp>()
                                          .Build();

            RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            Dependency = new UpdateBlockJob {
                m_EntityTypeHandle      = SystemAPI.GetEntityTypeHandle(),
                m_ParcelOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<ParcelOwner>(),
                m_CreatedTypeHandle     = SystemAPI.GetComponentTypeHandle<Created>(),
                m_ParcelSubBlockLookup  = SystemAPI.GetBufferLookup<ParcelSubBlock>(),
            }.Schedule(m_ParcelBlockQuery, Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateBlockJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> m_ParcelOwnerTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Created>     m_CreatedTypeHandle;
            [ReadOnly] public required BufferLookup<ParcelSubBlock>     m_ParcelSubBlockLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray      = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelOwnerArray = chunk.GetNativeArray(ref m_ParcelOwnerTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var owner  = parcelOwnerArray[i];

                    if (chunk.Has(ref m_CreatedTypeHandle)) {
                        // BurstLogger.Debug("[P_ParcelToBlockReferenceSystem]", $"Added reference: Parcel {owner.m_Owner} -> Block {entity}");
                        CollectionUtils.TryAddUniqueValue(m_ParcelSubBlockLookup[owner.m_Owner], new ParcelSubBlock(entity));
                        continue;
                    }

                    // BurstLogger.Debug("[P_ParcelToBlockReferenceSystem]", $"Removed reference: Parcel {owner.m_Owner} -> Block {entity}");
                    CollectionUtils.RemoveValue(m_ParcelSubBlockLookup[owner.m_Owner], new ParcelSubBlock(entity));
                }
            }
        }
    }
}