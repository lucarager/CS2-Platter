// <copyright file="P_PlaceholderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Game.Prefabs; // Added to reference SpawnableBuildingData
using Platter.Components; // Added to reference ParcelData

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Unity.Mathematics;

    public partial class P_PlaceholderSystem : PlatterGameSystemBase {
        private EntityQuery  m_Query;
        private PrefabSystem m_PrefabSystem;
        private ToolSystem   m_ToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem                =  World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem                  =  World.GetOrCreateSystemManaged<ToolSystem>();
            m_ToolSystem.EventPrefabChanged += OnPrefabChanged;

            m_Query = SystemAPI.QueryBuilder()
                               .WithAllRW<ParcelPlaceholder>()
                               .WithNone<Temp, Deleted>()
                               .Build();

            RequireForUpdate(m_Query);
        }

        protected override void OnUpdate() {
            foreach (var entity in m_Query.ToEntityArray(Allocator.Temp)) {
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                // Todo we should have a cache for our parcel prefabs, for simplicity
                var currentPrefab = m_PrefabSystem.GetPrefab<ParcelPlaceholderPrefab>(prefabRef);
                m_PrefabSystem.TryGetPrefab(
                    new PrefabID("ParcelPrefab", currentPrefab.GetPrefabID().GetName()),
                    out var newPrefab);
                prefabRef.m_Prefab = m_PrefabSystem.GetEntity(newPrefab);
                EntityManager.SetComponentData(entity, prefabRef);
                EntityManager.RemoveComponent<ParcelPlaceholder>(entity);
                EntityManager.AddComponent<Updated>(entity);
            }
        }

        private void OnPrefabChanged(PrefabBase currentPrefab) {
            if (m_ToolSystem.activePrefab == null || m_ToolSystem.activePrefab is not ParcelPrefab) {
                return;
            }

            m_PrefabSystem.TryGetPrefab(
                new PrefabID("ParcelPlaceholderPrefab", currentPrefab.GetPrefabID().GetName()),
                out var newPrefab);
            m_ToolSystem.activeTool.TrySetPrefab(newPrefab);
        }
    }
}