// <copyright file="P_PlaceholderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using Game.Prefabs; // Added to reference SpawnableBuildingData
using Platter.Components; // Added to reference ParcelData
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst.Intrinsics;
    using Unity.Mathematics;
    using UnityEngine.Rendering;

    public partial class P_PlaceholderSystem : PlatterGameSystemBase {
        private EntityQuery          m_PlacedQuery;
        private EntityQuery          m_TempQuery;
        private PrefabSystem         m_PrefabSystem;
        private ToolSystem           m_ToolSystem;
        private ModificationBarrier1 m_ModificationBarrier1;
        private P_UISystem           m_PlatterUISystem;
        private EntityCommandBuffer  m_CommandBuffer;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem         = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem           = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PlatterUISystem      = World.GetOrCreateSystemManaged<P_UISystem>();
             
            m_ToolSystem.EventPrefabChanged += OnPrefabChanged;

            m_PlacedQuery = SystemAPI.QueryBuilder()
                               .WithAllRW<ParcelPlaceholder>()
                               .WithNone<Temp, Deleted>()
                               .Build();

            m_TempQuery = SystemAPI.QueryBuilder()
                                     .WithAllRW<ParcelPlaceholder>()
                                     .WithAll<Temp>()
                                     .Build();
        }

        protected override void OnUpdate() {
            // Job to set the prezone data on a temp placeholder parcel
            var updateTempPlaceholderJobHandle = new UpdateTempPlaceholderJob(
                SystemAPI.GetEntityTypeHandle(),
                m_PlatterUISystem.PreZoneType,
                PlatterMod.Instance.Settings.AllowSpawn,
                SystemAPI.GetComponentTypeHandle<Parcel>(),
                m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter()
            ).Schedule(m_TempQuery, Dependency);

            m_ModificationBarrier1.AddJobHandleForProducer(updateTempPlaceholderJobHandle);

            // Job to swap out a permanent placeholder with the real parcel one it is placed
            // Todo make job
            m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer();
            foreach (var entity in m_PlacedQuery.ToEntityArray(Allocator.Temp)) {
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                // Todo we should have a cache for our parcel prefabs, for simplicity
                var currentPrefab = m_PrefabSystem.GetPrefab<ParcelPlaceholderPrefab>(prefabRef);
                m_PrefabSystem.TryGetPrefab(
                    new PrefabID("ParcelPrefab", currentPrefab.GetPrefabID().GetName()),
                    out var newPrefab);
                prefabRef.m_Prefab = m_PrefabSystem.GetEntity(newPrefab);
                m_CommandBuffer.SetComponent(entity, prefabRef);
                m_CommandBuffer.RemoveComponent<ParcelPlaceholder>(entity);
                m_CommandBuffer.AddComponent<Updated>(entity);
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

        private struct UpdateTempPlaceholderJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] private ZoneType                           m_ZoneType;
            [ReadOnly] private bool                               m_AllowSpawn;
            private            ComponentTypeHandle<Parcel>        m_ParcelTypeHandle;
            private            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public UpdateTempPlaceholderJob(EntityTypeHandle entityTypeHandle, ZoneType zoneType, bool allowSpawn, ComponentTypeHandle<Parcel> parcelTypeHandle, EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle = entityTypeHandle;
                m_ZoneType = zoneType;
                m_AllowSpawn = allowSpawn;
                m_ParcelTypeHandle = parcelTypeHandle;
                m_CommandBuffer = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray = chunk.GetNativeArray(ref m_ParcelTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var parcel = parcelArray[i];
                    parcel.m_PreZoneType = m_ZoneType;
                    parcelArray[i]       = parcel;

                    if (m_AllowSpawn) {
                        m_CommandBuffer.AddComponent<ParcelSpawnable>(unfilteredChunkIndex, entity);
                    }

                    //m_CommandBuffer.AddComponent<Updated>(unfilteredChunkIndex, entity);
                }
            }
        }

        private struct UpdatePermanentPlaceholderJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                   m_EntityTypeHandle;
            private            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                }

            }
        }
    }
}