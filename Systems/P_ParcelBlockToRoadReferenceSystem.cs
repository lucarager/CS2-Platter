// <copyright file="P_ParcelBlockToRoadReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for adding the "Owner" component to a block when a parcel and road get connected.
    /// This is what marks a block as a valid spawn location to the vanilla ZoneSpawnSystem.
    /// </summary>
    public partial class P_ParcelBlockToRoadReferenceSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private ModificationBarrier5 m_ModificationBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        // Queries
        private EntityQuery m_ParcelUpdatedQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_ParcelBlockToRoadReferenceSystem));
            m_Log.Debug($"OnCreate()");

            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier5>();

            m_ParcelUpdatedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Parcel>()
                                            .WithAny<Updated>()
                                            .WithNone<Temp>()
                                            .Build();

            RequireForUpdate(m_ParcelUpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate() -- Updating Percel->Block->Road ownership references");

            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();

            var entities = m_ParcelUpdatedQuery.ToEntityArray(Allocator.Temp);

            foreach (var parcelEntity in entities) {
                var parcel        = EntityManager.GetComponentData<Parcel>(parcelEntity);
                var allowSpawning = EntityManager.HasComponent<ParcelSpawnable>(parcelEntity);

                if (!EntityManager.TryGetBuffer<ParcelSubBlock>(parcelEntity, false, out var subBlockBuffer)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find parcel's {parcelEntity} subblock buffer");
                    return;
                }

                foreach (var subBlock in subBlockBuffer) {
                    var blockEntity   = subBlock.m_SubBlock;
                    var curvePosition = EntityManager.GetComponentData<CurvePosition>(blockEntity);

                    curvePosition.m_CurvePosition = parcel.m_CurvePosition;
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);

                    m_Log.Debug($"OnUpdate() -- Parcel {parcelEntity} -> Block {blockEntity}: Updating Block's Owner ({parcel.m_RoadEdge})");

                    if (parcel.m_RoadEdge != Entity.Null) {
                        m_CommandBuffer.AddComponent<Updated>(parcel.m_RoadEdge, default);
                    }

                    if (parcel.m_RoadEdge == Entity.Null || !allowSpawning) {
                        // We need to make sure that the block actually NEVER has a null owner
                        // Otherwise the game can crash when systems try to retrieve the Edge.
                        // Tsk tsk paradox for not considering an edge case that is entirely a modders thing and doesn't happen in vanilla ;)

                        // Also note that this is how we prevent a parcel from spawning buildings - as long as no Edge is set as owner,
                        // the spawn system won't pick up this block.
                        m_CommandBuffer.RemoveComponent<Owner>(blockEntity);
                    } else {
                        if (EntityManager.TryGetComponent<Owner>(blockEntity, out var owner)) {
                            owner.m_Owner = parcel.m_RoadEdge;
                            m_CommandBuffer.SetComponent<Owner>(blockEntity, owner);
                        } else {
                            m_CommandBuffer.AddComponent<Owner>(blockEntity, new Owner(parcel.m_RoadEdge));
                        }
                    }
                }
            }

            entities.Dispose();
        }
    }
}
