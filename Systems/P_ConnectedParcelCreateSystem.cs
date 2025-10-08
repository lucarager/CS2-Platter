// <copyright file="P_ConnectedParcelCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    public partial class P_ConnectedParcelCreateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_RoadPrefabCreatedQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ConnectedParcelCreateSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_RoadPrefabCreatedQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge>()
                .WithAll<Created>()
                .WithAll<ConnectedBuilding>() // Only roads that allow connections will have this component
                .WithNone<ConnectedParcel>()
                .WithNone<Temp>()
                .WithNone<Deleted>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_RoadPrefabCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var logMethodPrefix = $"OnUpdate() --";
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();

            var chunkArray = m_RoadPrefabCreatedQuery.ToArchetypeChunkArray(Allocator.TempJob);
            foreach (var archetypeChunk in chunkArray) {
                var entityArray = archetypeChunk.GetNativeArray(entityTypeHandle);
                if (entityArray.Length != 0) {
                    for (var i = 0; i < entityArray.Length; i++) {
                        var entity = entityArray[i];
                        m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to road entity");
                        EntityManager.AddBuffer<ConnectedParcel>(entity);
                    }
                }
            }
        }
    }
}
