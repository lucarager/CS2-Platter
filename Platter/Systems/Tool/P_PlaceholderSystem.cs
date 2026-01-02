// <copyright file="P_PlaceholderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using static Colossal.AssetPipeline.Diagnostic.Report;

    #endregion

    public partial class P_PlaceholderSystem  : PlatterGameSystemBase {
        private EntityQuery           m_PlacedQuery;
        private EntityQuery           m_TempQuery;
        private ModificationBarrier1  m_ModificationBarrier1;
        private P_UISystem            m_PlatterUISystem;
        private P_PrefabsCreateSystem m_PPrefabsCreateSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PlatterUISystem      = World.GetOrCreateSystemManaged<P_UISystem>();
            m_PPrefabsCreateSystem = World.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

            // Parcels (with ParcelPlaceholder) that have been placed (not Temp)
            m_PlacedQuery = SystemAPI.QueryBuilder()
                                     .WithAllRW<ParcelPlaceholder>()
                                     .WithNone<Temp, Deleted>()
                                     .Build();

            // Parcels (with ParcelPlaceholder) created by a tool (Temp)
            m_TempQuery = SystemAPI.QueryBuilder()
                                   .WithAllRW<ParcelPlaceholder>()
                                   .WithAll<Temp>()
                                   .WithNone<Deleted>()
                                   .Build();

            RequireAnyForUpdate(m_PlacedQuery, m_TempQuery);
        }

        protected override void OnUpdate() {
            // Job to set the prezone data on a temp placeholder parcel
            var updateTempPlaceholderJobHandle = new UpdateTempPlaceholderJob {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_ZoneType = m_PlatterUISystem.PreZoneType,
                m_ParcelTypeHandle = SystemAPI.GetComponentTypeHandle<Parcel>(),
                m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
            }.Schedule(m_TempQuery, Dependency);
            m_ModificationBarrier1.AddJobHandleForProducer(updateTempPlaceholderJobHandle);

            Dependency = JobHandle.CombineDependencies(updateTempPlaceholderJobHandle, Dependency);

            // Job to swap prefabref once placeholder is placed
            var swapPlaceholderForPermnanentJobHandle = new SwapPlaceholderRefJob() {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_AllowSpawn       = PlatterMod.Instance.Settings.AllowSpawn,
                m_PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                m_ParcelDataLookup = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabCache = m_PPrefabsCreateSystem.GetReadOnlyPrefabCache(),
                m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
                toPermanent = true,
            }.Schedule(m_PlacedQuery, Dependency);
            m_ModificationBarrier1.AddJobHandleForProducer(swapPlaceholderForPermnanentJobHandle);

            Dependency = JobHandle.CombineDependencies(swapPlaceholderForPermnanentJobHandle, Dependency);
        }

        private struct UpdateTempPlaceholderJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] public required ZoneType                           m_ZoneType;
            public required            ComponentTypeHandle<Parcel>        m_ParcelTypeHandle;
            public required            EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray = chunk.GetNativeArray(ref m_ParcelTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var parcel = parcelArray[i];
                    parcel.m_PreZoneType = m_ZoneType;
                    parcelArray[i]       = parcel;

                    BurstLogger.Debug($"[UpdateTempPlaceholderJob]", $"Updated parcel {entity}");

                    //m_CommandBuffer.AddComponent<Updated>(index, entity);
                }
            }
        }

        private struct SwapPlaceholderRefJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>      m_PrefabRefTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData>         m_ParcelDataLookup;
            [ReadOnly] public required NativeHashMap<int, Entity>.ReadOnly m_PrefabCache;
            [ReadOnly] public required bool                                m_AllowSpawn;
            public required            EntityCommandBuffer.ParallelWriter  m_CommandBuffer;
            public required            bool                                toPermanent;

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var prefabRefArray = chunk.GetNativeArray(ref m_PrefabRefTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var prefabRef = prefabRefArray[i];

                    BurstLogger.Debug($"[SwapPlaceholderRefJob]", $"Updated parcel {entity}");

                    // Get the parcel data from the placeholder prefab entity to extract dimensions
                    if (!m_ParcelDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        continue;
                    }

                    var parcelData = m_ParcelDataLookup[prefabRef.m_Prefab];
                    var parcelPrefabID = ParcelUtils.GetPrefabID(parcelData.m_LotSize.x, parcelData.m_LotSize.y);
                    var cacheKey = ParcelUtils.GetCustomHashCode(parcelPrefabID, !toPermanent);

                    if (!m_PrefabCache.TryGetValue(cacheKey, out var newPrefabEntity)) {
                        continue;
                    }

                    // Swap PrefabRef
                    prefabRef.m_Prefab = newPrefabEntity;
                    m_CommandBuffer.SetComponent(index, entity, prefabRef);

                    // Set or remove ParcelPlaceholder component
                    if (toPermanent) {
                        m_CommandBuffer.RemoveComponent<ParcelPlaceholder>(index, entity);
                    } else {
                        m_CommandBuffer.AddComponent<ParcelPlaceholder>(index, entity);
                    }

                    if (m_AllowSpawn) {
                        m_CommandBuffer.AddComponent<ParcelSpawnable>(index, entity);
                    }

                    // Update
                    m_CommandBuffer.AddComponent<Updated>(index, entity);
                }
            }
        }

    }
}