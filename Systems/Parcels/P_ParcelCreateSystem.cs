// <copyright file="P_ParcelCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Components;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine.Rendering;
    using Utils;

    /// <summary>
    /// System responsible for setting data when a parcel entity is created (likely by a tool).
    /// </summary>
    public partial class P_ParcelCreateSystem : GameSystemBase {
        private EntityQuery          m_ParcelCreatedQuery;
        private P_UISystem           m_PlatterUISystem;
        private PrefixedLogger       m_Log;
        private ModificationBarrier1 m_ModificationBarrier1;
        private EntityCommandBuffer  m_CommandBuffer;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelCreateSystem));
            m_Log.Debug("OnCreate()");

            // Retrieve Systems
            m_PlatterUISystem      = World.GetOrCreateSystemManaged<P_UISystem>();
            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            // Queries
            m_ParcelCreatedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Parcel>()
                                            .WithNone<Initialized>()
                                            .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer();

            foreach (var entity in m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp)) {
                var parcel = EntityManager.GetComponentData<Parcel>(entity);
                parcel.m_PreZoneType = m_PlatterUISystem.PreZoneType;
                m_CommandBuffer.SetComponent(entity, parcel);
                m_CommandBuffer.AddComponent<Initialized>(entity);
                m_CommandBuffer.AddComponent<Updated>(entity);
            }
        }
    }
}