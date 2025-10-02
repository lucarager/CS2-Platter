// <copyright file="VanillaRoadInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    public partial class P_VanillaRoadInitializeSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_RoadPrefabCreatedQuery;
        private EntityQuery m_RoadPrefabQuery;

        // Systems & References
        private Game.Prefabs.PrefabSystem m_PrefabSystem;

        // Type handles
        private EntityTypeHandle m_EntityTypeHandle;
        private ComponentTypeHandle<PrefabData> m_ComponentTypeHandle;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_VanillaRoadInitializeSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();

            // Queries
            m_RoadPrefabCreatedQuery = GetEntityQuery(new EntityQueryDesc {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Created>(),
                },
                None = new ComponentType[] {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                },
            });

            m_RoadPrefabQuery = GetEntityQuery(new EntityQueryDesc {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<PrefabData>(),
                    ComponentType.ReadOnly<RoadData>(),
                },
                None = new ComponentType[] {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                },
            });

            // Type handles
            m_EntityTypeHandle = GetEntityTypeHandle();
            m_ComponentTypeHandle = GetComponentTypeHandle<PrefabData>();

            // Update Cycle
            RequireForUpdate(m_RoadPrefabCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var logMethodPrefix = $"OnUpdate() --";

            var chunkArray = m_RoadPrefabCreatedQuery.ToArchetypeChunkArray(Allocator.TempJob);
            foreach (var archetypeChunk in chunkArray) {
                var entityArray = archetypeChunk.GetNativeArray(m_EntityTypeHandle);
                if (entityArray.Length != 0) {
                    for (var i = 0; i < entityArray.Length; i++) {
                        var entity = entityArray[i];
                        m_Log.Debug($"{logMethodPrefix} Created a road entity {entity}");

                        if (!EntityManager.HasBuffer<ConnectedParcel>(entity)) {
                            m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to road entity");
                            EntityManager.AddBuffer<ConnectedParcel>(entity);
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        //protected override void OnGamePreload(Purpose purpose, GameMode mode) {
        //    base.OnGamePreload(purpose, mode);
        //    var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

        //    var chunkArray = m_RoadPrefabQuery.ToArchetypeChunkArray(Allocator.TempJob);
        //    foreach (var archetypeChunk in chunkArray) {
        //        var entityArray = archetypeChunk.GetNativeArray(m_EntityTypeHandle);
        //        var prefabDataArray = archetypeChunk.GetNativeArray<PrefabData>(ref m_ComponentTypeHandle);
        //        if (entityArray.Length != 0) {
        //            for (var i = 0; i < entityArray.Length; i++) {
        //                var entity = entityArray[i];
        //                var prefab = m_PrefabSystem.GetPrefab<RoadPrefab>(prefabDataArray[i]);

        //                if (prefab.m_ZoneBlock != null && !EntityManager.HasBuffer<ConnectedParcel>(entity)) {
        //                    m_Log.Debug($"{logMethodPrefix} Added ConnectedParcel buffer to road entity of prefab {prefab.name}");
        //                    EntityManager.AddBuffer<ConnectedParcel>(entity);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}

// if (this.m_ZoneBlock != null) {
//    components.Add(ComponentType.ReadWrite<SubBlock>());
//    components.Add(ComponentType.ReadWrite<ConnectedBuilding>());
//    components.Add(ComponentType.ReadWrite<ServiceCoverage>());
//    components.Add(ComponentType.ReadWrite<ResourceAvailability>());
//    components.Add(ComponentType.ReadWrite<Density>());
//    return;
// }