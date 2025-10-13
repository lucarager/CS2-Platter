// <copyright file="P_ConnectedParcelCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for adding the ConnectedParcel buffer to roads and buildings.
    /// </summary>
    public partial class P_ConnectedParcelCreateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_PrefabCreatedQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ConnectedParcelCreateSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_PrefabCreatedQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge, Created, ConnectedBuilding>() // Only roads that allow connections will have ConnectedBuilding component
                .WithNone<ConnectedParcel, Temp, Deleted>()
                .AddAdditionalQuery()
                .WithAll<Building, UnderConstruction>()
                .WithNone<ConnectedParcel, Temp>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var logMethodPrefix = $"OnUpdate() --";
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();

            var chunkArray = m_PrefabCreatedQuery.ToArchetypeChunkArray(Allocator.TempJob);

            foreach (var archetypeChunk in chunkArray) {
                var entityArray = archetypeChunk.GetNativeArray(entityTypeHandle);
                if (entityArray.Length == 0) {
                    continue;
                }

                foreach (var entity in entityArray) {
                    m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to entity");
                    EntityManager.AddBuffer<ConnectedParcel>(entity);
                }
            }
        }
    }
}
