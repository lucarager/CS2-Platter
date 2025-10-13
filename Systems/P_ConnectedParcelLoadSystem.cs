// <copyright file="P_ConnectedParcelLoadSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Net;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for adding the ConnectedParcel buffer to roads and buildings that don't have it on load.
    /// </summary>
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
                if (entityArray.Length == 0) {
                    continue;
                }

                foreach (var entity in entityArray) {
                    if (!EntityManager.HasComponent<ConnectedBuilding>(entity)) {
                        continue;
                    }

                    m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to road entity");
                    EntityManager.AddBuffer<ConnectedParcel>(entity);
                }
            }
        }
    }
}
