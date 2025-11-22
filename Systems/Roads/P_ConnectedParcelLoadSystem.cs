// <copyright file="P_ConnectedParcelLoadSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game.Buildings;
    using Game.Net;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// System responsible for adding the ConnectedParcel buffer to roads that don't have it on load.
    /// </summary>
    public partial class P_ConnectedParcelLoadSystem : PlatterGameSystemBase {
        private EntityQuery m_RoadQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_RoadQuery = SystemAPI.QueryBuilder()
                                   .WithAllRW<Edge>()
                                   .WithAll<ConnectedBuilding>()
                                   .WithNone<ConnectedParcel>()
                                   .Build();

            // Update Cycle
            RequireForUpdate(m_RoadQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { EntityManager.AddComponent<ConnectedParcel>(m_RoadQuery); }
    }
}