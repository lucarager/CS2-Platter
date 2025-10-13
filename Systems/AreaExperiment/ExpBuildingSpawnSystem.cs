// <copyright file="ExpBuildingSpawnSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ExpBuildingSpawnSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers

        // Queries
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ExpBuildingSpawnSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Building>()
                               .WithAny<Updated, BatchesUpdated>()
                               .WithNone<Temp>()
                               .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                m_Log.Debug($"Building was updated {entity}");
            }

            entities.Dispose();
        }
    }
}
