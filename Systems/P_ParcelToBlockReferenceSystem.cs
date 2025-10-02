// <copyright file="P_ParcelToBlockReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
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
            m_Log.Debug($"OnCreate()");

            m_ParcelBlockQuery = GetEntityQuery(new EntityQueryDesc[]
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

            RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var blockEntities = m_ParcelBlockQuery.ToEntityArray(Allocator.Temp);
            var subBlockBufferLookup = SystemAPI.GetBufferLookup<SubBlock>();

            for (var i = 0; i < blockEntities.Length; i++) {
                var blockEntity = blockEntities[i];

                m_Log.Debug($"OnUpdate() -- Setting Block ownership references for entity {blockEntity}");

                if (!EntityManager.TryGetComponent<ParcelOwner>(blockEntity, out var parcelOwner)) {
                    m_Log.Error($"{blockEntity} didn't have parcelOwner component");
                    return;
                }

                if (!subBlockBufferLookup.TryGetBuffer(parcelOwner.m_Owner, out var subBlockBuffer)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find owner's {parcelOwner.m_Owner} subblock buffer");
                    return;
                }

                if (EntityManager.HasComponent<Created>(blockEntity)) {
                    m_Log.Debug($"OnUpdate() -- Block was CREATED. Adding block reference to parcel.");

                    if (!CollectionUtils.TryAddUniqueValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity))) {
                        m_Log.Error($"OnUpdate() -- Unsuccesfully tried adding {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
                    }

                    return;
                }

                // Todo is this needed? The parcel will be destroyed, too.
                m_Log.Debug($"OnUpdate() -- Block was DELETED. Removing block from parcel buffer");

                CollectionUtils.RemoveValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity));
            }
        }
    }
}
