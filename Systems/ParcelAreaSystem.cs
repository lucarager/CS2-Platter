// <copyright file="ParcelAreaSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelAreaSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private ModificationBarrier4 m_ModificationBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        // Queries
        private EntityQuery m_ParcelQuery;
        private EntityQuery m_ZoneQuery;

        // Systems & References
        private PrefabSystem m_PrefabSystem;
        private ZoneSystem m_ZoneSystem;
        private PlatterUISystem m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Retriefve Systems
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneSystem = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_PlatterUISystem = World.GetOrCreateSystemManaged<PlatterUISystem>();

            // Queries
            m_ParcelQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>()
                    },
                });

            // Update Cycle
            RequireForUpdate(m_ParcelQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            var entities = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"OnUpdate() -- Found {entities.Length}");

            foreach (var parcelEntity in entities) {
                // DELETE state
                if (EntityManager.HasComponent<Deleted>(parcelEntity)) {
                    m_Log.Debug($"OnUpdate() -- [DELETE] Deleting parcel {parcelEntity}");
                    return;
                }

                // UPDATE State
                m_Log.Debug($"OnUpdate() -- Running UPDATE logic");

                // Retrieve components
                if (!EntityManager.TryGetComponent<Parcel>(parcelEntity, out var parcel) ||
                    !EntityManager.TryGetComponent<PrefabRef>(parcelEntity, out var prefabRef) ||
                    !m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase) ||
                    !EntityManager.TryGetComponent<ParcelData>(prefabRef, out var parcelData) ||
                    !EntityManager.TryGetComponent<ParcelComposition>(parcelEntity, out var parcelComposition) ||
                    !EntityManager.TryGetComponent<Transform>(parcelEntity, out var transform)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find all required components");
                    return;
                }

                var parcelPrefab = prefabBase.GetComponent<ParcelPrefab>();
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

                //EntityManager.GetBuffer<>
            }
        }
    }
}
