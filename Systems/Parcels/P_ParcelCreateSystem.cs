// <copyright file="P_ParcelCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Components;
    using Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for setting data when a parcel entity is created (likely by a tool).
    /// </summary>
    public partial class P_ParcelCreateSystem : GameSystemBase {
        private PrefixedLogger m_Log;
        private EntityQuery    m_ParcelCreatedQuery;
        private P_UISystem     m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelCreateSystem));
            m_Log.Debug("OnCreate()");

            // Retrieve Systems
            m_PlatterUISystem      = World.GetOrCreateSystemManaged<P_UISystem>();

            // Queries
            m_ParcelCreatedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Parcel>()
                                            .WithAny<Created, Temp>()
                                            .WithNone<Updated, Deleted>()
                                            .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelCreatedQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            var entities              = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);
            var currentDefaultPreZone = m_PlatterUISystem.PreZoneType;

            foreach (var parcelEntity in entities) {
                // Retrieve components
                var parcel = EntityManager.GetComponentData<Parcel>(
                    parcelEntity
                );
                parcel.m_PreZoneType = currentDefaultPreZone;
                EntityManager.SetComponentData(
                    parcelEntity,
                    parcel);
            }
        }
    }
}