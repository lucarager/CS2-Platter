// <copyright file="ParcelAreaSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ExpBuildingSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private bool done = false;

        // Queries
        private EntityQuery m_Query;

        // Systems & References
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ExpBuildingSystem));
            m_Log.Debug($"OnCreate()");

            // Retriefve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Queries
            m_Query = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<UnderConstruction>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                var constructionData = EntityManager.GetComponentData<UnderConstruction>(entity);

                if (constructionData.m_Progress != 0) {
                    return;
                }

                if (done) {
                    return;
                }

                done = true;

                m_Log.Debug($"OnUpdate() -- Processing entity {entity} out of {entities.Length}");

                if (!EntityManager.TryGetBuffer<Game.Areas.SubArea>(entity, false, out var subAreaBuffer)) {
                    return;
                }

                m_Log.Debug($"OnUpdate() -- Processing entity {entity} with buffer {subAreaBuffer} {subAreaBuffer.Length}");

                for (var i = 0; i < subAreaBuffer.Length; i++) {
                    var subArea = subAreaBuffer[i];
                    if (EntityManager.TryGetComponent<PrefabRef>(subArea.m_Area, out var prefabRef) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var subAreaPrefab)) {
                        m_Log.Debug($"OnUpdate() -- Building has the following Area: {subAreaPrefab.name}");

                        if (subAreaPrefab.name.StartsWith("Sand") && EntityManager.TryGetBuffer<Node>(subArea.m_Area, false, out var nodeBuffer)) {
                            for (var j = 0; j < nodeBuffer.Length; j++) {
                                var node = nodeBuffer[j];
                                m_Log.Debug($"OnUpdate() -- Processing node {j} {node.m_Position.x}");
                                node.m_Position.x += 3;
                                nodeBuffer[j] = node;
                            }
                        }
                    }
                }
            }
        }
    }
}
