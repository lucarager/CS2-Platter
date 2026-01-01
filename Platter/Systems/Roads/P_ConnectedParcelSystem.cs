// <copyright file="P_ConnectedParcelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for adding the ConnectedParcel buffer to roads.
    /// </summary>
    public partial class P_ConnectedParcelSystem : PlatterGameSystemBase {
        private EntityQuery    m_PrefabCreatedQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Queries
            m_PrefabCreatedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Edge, Created, Game.Zones.SubBlock, ConnectedBuilding>() // Only roads that allow connections will have ConnectedBuilding component
                                            .WithNone<ConnectedParcel, Temp, Deleted>()
                                            .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { EntityManager.AddComponent<ConnectedParcel>(m_PrefabCreatedQuery); }
    }
}