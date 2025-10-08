// <copyright file="P_SerializeConnectedParcelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Logging;
    using Game;
    using Game.Buildings;
    using Game.Net;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    internal partial class P_SerializeConnectedParcelSystem : GameSystemBase {
        private EntityQuery m_Query;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(P_SerializeConnectedParcelSystem));
            m_Log.Debug($"OnCreate()");
            m_Query = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Parcel>(),
            });
            base.RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate()");

            var deserializeJob = default(DeserializeJob);
            deserializeJob.m_EntityType = SystemAPI.GetEntityTypeHandle();
            deserializeJob.m_ParcelType = SystemAPI.GetComponentTypeHandle<Parcel>();
            deserializeJob.m_ConnectedParcelsBufferLookup = SystemAPI.GetBufferLookup<ConnectedParcel>();
            base.Dependency = deserializeJob.Schedule(m_Query, base.Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeserializeJob : IJobChunk {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;
            [ReadOnly]
            public ComponentTypeHandle<Parcel> m_ParcelType;
            public BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            public void Execute(in ArchetypeChunk chunk) {
                var entityArray = chunk.GetNativeArray(m_EntityType);
                var parcelArray = chunk.GetNativeArray<Parcel>(ref m_ParcelType);
                for (var i = 0; i < parcelArray.Length; i++) {
                    var parcelEntity = entityArray[i];
                    var parcel = parcelArray[i];
                    if (parcel.m_RoadEdge != Entity.Null) {
                        m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge].Add(new ConnectedParcel(parcelEntity));
                    }
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                Execute(in chunk);
            }
        }
    }
}
