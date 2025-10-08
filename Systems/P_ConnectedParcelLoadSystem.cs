// <copyright file="P_ConnectedParcelLoadSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Buildings;
    using Game.Net;
    using Game.Serialization;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    public partial class P_ConnectedParcelLoadSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_RoadQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ConnectedParcelLoadSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_RoadQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Edge>()
                .WithAll<ConnectedBuilding>()
                .WithNone<ConnectedParcel>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_RoadQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var logMethodPrefix = $"OnUpdate() --";
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
            m_Log.Debug($"{logMethodPrefix} Start");

            var chunkArray = m_RoadQuery.ToArchetypeChunkArray(Allocator.TempJob);
            foreach (var archetypeChunk in chunkArray) {
                var entityArray = archetypeChunk.GetNativeArray(entityTypeHandle);
                if (entityArray.Length != 0) {
                    for (var i = 0; i < entityArray.Length; i++) {
                        var entity = entityArray[i];
                        if (EntityManager.HasComponent<ConnectedBuilding>(entity)) {
                            m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to road entity");
                            EntityManager.AddBuffer<ConnectedParcel>(entity);
                        }
                    }
                }
            }
        }
    }
}
