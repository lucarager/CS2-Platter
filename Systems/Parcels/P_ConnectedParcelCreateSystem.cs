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
    /// System responsible for adding the ConnectedParcel buffer to roads.
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
                .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabCreatedQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            EntityManager.AddComponent<ConnectedParcel>(m_PrefabCreatedQuery);
        }
    }
}
