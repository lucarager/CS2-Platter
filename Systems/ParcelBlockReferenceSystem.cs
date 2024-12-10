// <copyright file="ParcelBlockReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Zones;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelBlockReferenceSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelBlockQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(ParcelBlockReferenceSystem));
            m_Log.Debug($"OnCreate()");

            // TODO Only do this for blocks that should belong to a parcel, not edges!
            this.m_ParcelBlockQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new () {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Block>(),
                        ComponentType.ReadOnly<ParcelOwner>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Deleted>()
                    }
                }
            });

            base.RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate() -- Setting Block ownership references");

            NativeArray<Entity> blockEntities = m_ParcelBlockQuery.ToEntityArray(Allocator.Temp);
            BufferLookup<SubBlock> subBlockBufferLookup = GetBufferLookup<SubBlock>(true);

            for (int i = 0; i < blockEntities.Length; i++) {
                Entity blockEntity = blockEntities[i];

                m_Log.Debug($"OnUpdate() -- Setting Block ownership references for entity {blockEntity}");

                if (!EntityManager.TryGetComponent<ParcelOwner>(blockEntity, out ParcelOwner parcelOwner)) {
                    m_Log.Error($"{blockEntity} didn't have parcelOwner component");
                    return;
                }

                if (!subBlockBufferLookup.HasBuffer(parcelOwner.m_Owner)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find owner's {parcelOwner.m_Owner} subblock buffer");
                    return;
                }

                DynamicBuffer<SubBlock> subBlockBuffer = subBlockBufferLookup[parcelOwner.m_Owner];

                if (EntityManager.HasComponent<Created>(blockEntity)) {
                    if (CollectionUtils.TryAddUniqueValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity))) {
                        m_Log.Debug($"OnUpdate() -- Succesfully added {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
                    } else {
                        m_Log.Error($"OnUpdate() -- Unsuccesfully tried adding {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
                    }

                    return;
                }

                CollectionUtils.RemoveValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity));
                m_Log.Debug($"OnUpdate() -- Succesfully deleted {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
            }
        }
    }
}
