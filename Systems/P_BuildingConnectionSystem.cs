// <copyright file="P_BuildingConnectSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for connecting buildings to their parcels.
    /// </summary>
    public partial class P_BuildingConnectionSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingConnectionSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Building>()
                .WithAll<ConnectedParcel>()
                .WithAny<Updated>()
                .WithAny<BatchesUpdated>()
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
