// <copyright file="ParcelBlockRoadReferenceSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelBlockRoadReferenceSystem : GameSystemBase {
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

            m_Log = new PrefixedLogger(nameof(ParcelBlockRoadReferenceSystem));
            m_Log.Debug($"OnCreate()");

            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier5>();

            m_ParcelUpdatedQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            base.RequireForUpdate(m_ParcelUpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate() -- Updating Percel->Block->Road ownership references");

            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();

            NativeArray<Entity> parcelEntities = m_ParcelUpdatedQuery.ToEntityArray(Allocator.Temp);

            GetBufferLookup<SubBlock>(true);

            for (int i = 0; i < parcelEntities.Length; i++) {
                Entity parcelEntity = parcelEntities[i];
                Parcel parcelData = EntityManager.GetComponentData<Parcel>(parcelEntity);

                m_Log.Debug($"OnUpdate() -- Updating references for parcel {parcelEntity}");

                if (!EntityManager.TryGetBuffer<SubBlock>(parcelEntity, false, out DynamicBuffer<SubBlock> subBlockBuffer)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find parcel's {parcelEntity} subblock buffer");
                    return;
                }

                for (int j = 0; j < subBlockBuffer.Length; j++) {
                    SubBlock subBlock = subBlockBuffer[j];
                    Entity blockEntity = subBlock.m_SubBlock;
                    CurvePosition curvePosition = EntityManager.GetComponentData<CurvePosition>(blockEntity);
                    curvePosition.m_CurvePosition = parcelData.m_CurvePosition;
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);

                    if (EntityManager.TryGetComponent<Owner>(blockEntity, out Owner owner)) {
                        // Update logic
                        owner.m_Owner = parcelData.m_RoadEdge;
                        m_CommandBuffer.SetComponent<Owner>(blockEntity, owner);
                    } else {
                        m_CommandBuffer.AddComponent<Owner>(blockEntity, new Owner(parcelData.m_RoadEdge));
                    }
                }
            }
        }
    }
}
