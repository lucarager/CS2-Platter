// <copyright file="P_ParcelCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for setting data when a parcel entity is created (likely by a tool).
    /// </summary>
    public partial class P_ParcelCreateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private ModificationBarrier1 m_ModificationBarrier1;
        private EntityCommandBuffer m_CommandBuffer;

        // Queries
        private EntityQuery m_ParcelCreatedQuery;

        // Systems & References
        private P_UISystem m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelCreateSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PlatterUISystem = World.GetOrCreateSystemManaged<P_UISystem>();

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
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer();
            var entities = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);
            var currentDefaultPreZone = m_PlatterUISystem.PreZoneType;

            foreach (var parcelEntity in entities) {
                // Retrieve components
                if (!EntityManager.TryGetComponent<Parcel>(parcelEntity, out var parcel)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find all required components");
                    return;
                }

                // Update prezoned type
                parcel.m_PreZoneType = currentDefaultPreZone;
                m_CommandBuffer.SetComponent<Parcel>(parcelEntity, parcel);
            }

            entities.Dispose();
        }
    }
}
