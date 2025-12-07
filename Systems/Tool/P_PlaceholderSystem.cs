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

    public partial class P_PlaceholderSystem : PlatterGameSystemBase {
        private EntityQuery           m_PlacedQuery;
        private EntityQuery           m_TempQuery;
        private EntityQuery           m_TempNotPlaceholderQuery;
        private ModificationBarrier1  m_ModificationBarrier1;
        private P_UISystem            m_PlatterUISystem;
        private PrefabSystem          m_PrefabSystem;
        private ToolSystem            m_ToolSystem;
        private P_PrefabsCreateSystem m_PPrefabsCreateSystem;
        private ObjectToolSystem      m_ObjectToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem                  =  World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem                    =  World.GetOrCreateSystemManaged<ToolSystem>();
            m_ModificationBarrier1          =  World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PlatterUISystem               =  World.GetOrCreateSystemManaged<P_UISystem>();
            m_PPrefabsCreateSystem          =  World.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

            // Register a callback for prefab changes
            m_ToolSystem.EventPrefabChanged += OnPrefabChanged;
            m_ToolSystem.EventToolChanged   += OnToolChanged;


            // Parcels (with ParcelPlaceholder) that have been placed (not Temp)
            m_PlacedQuery = SystemAPI.QueryBuilder()
                                     .WithAllRW<ParcelPlaceholder>()
                                     .WithNone<Temp, Deleted>()
                                     .Build();

            // Parcels (with ParcelPlaceholder) created by a tool (Temp)
            m_TempQuery = SystemAPI.QueryBuilder()
                                   .WithAllRW<ParcelPlaceholder>()
                                   .WithAll<Temp>()
                                   .Build();

            // Parcels (without ParcelPlaceholder) created by something like the relocate tool (with Temp)
            // Currently ununsed
            m_TempNotPlaceholderQuery = SystemAPI.QueryBuilder()
                                                 .WithAll<Parcel, Temp>()
                                                 .WithNone<ParcelPlaceholder>()
                                                 .Build();
        }

        protected override void OnUpdate() {
            if (m_PlacedQuery.IsEmpty &&
                m_TempQuery.IsEmpty   &&
                m_TempNotPlaceholderQuery.IsEmpty) {
                return;
            }

            // Job to set the prezone data on a temp placeholder parcel
            var updateTempPlaceholderJobHandle = new UpdateTempPlaceholderJob {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_ZoneType = m_PlatterUISystem.PreZoneType,
                m_AllowSpawn = PlatterMod.Instance.Settings.AllowSpawn,
                m_ParcelTypeHandle = SystemAPI.GetComponentTypeHandle<Parcel>(),
                m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
            }.Schedule(m_TempQuery, Dependency);
            m_ModificationBarrier1.AddJobHandleForProducer(updateTempPlaceholderJobHandle);

            Dependency = JobHandle.CombineDependencies(updateTempPlaceholderJobHandle, updateTempPlaceholderJobHandle);

            // Job to swap prefabref once placeholder is placed
            var swapPlaceholderForPermnanentJobHandle = new SwapPlaceholderRefJob() {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                m_ParcelDataLookup = SystemAPI.GetComponentLookup<ParcelData>(),
                m_PrefabCache = m_PPrefabsCreateSystem.GetReadOnlyPrefabCache(),
                m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
                toPermanent = true,
            }.Schedule(m_PlacedQuery, Dependency);
            m_ModificationBarrier1.AddJobHandleForProducer(swapPlaceholderForPermnanentJobHandle);

            Dependency = JobHandle.CombineDependencies(updateTempPlaceholderJobHandle, swapPlaceholderForPermnanentJobHandle);
        }

        private void OnToolChanged(ToolBaseSystem tool) {
            if (tool is ObjectToolSystem
                {
                    prefab: ParcelPrefab,
                } objectTool) {
                OnPrefabChanged(objectTool.prefab);
            }
        }

        private void OnPrefabChanged(PrefabBase currentPrefab) {
            if (m_ToolSystem.activeTool is not ObjectToolSystem objectTool ||
                objectTool.prefab == null                            ||
                objectTool.prefab is not ParcelPrefab) {
                return;
            }

            m_Log.Debug($"OnPrefabChanged(currentPrefab = {currentPrefab}");

            var currentPrefabID = currentPrefab.GetPrefabID();
            var cacheKey        = ParcelUtils.GetCustomHashCode(currentPrefabID, true);

            if (m_PPrefabsCreateSystem.TryGetCachedPrefab(cacheKey, out var newPrefabEntity) && 
                m_PPrefabsCreateSystem.TryGetCachedPrefabBase(newPrefabEntity, out var newPrefabBase)) {
                objectTool.TrySetPrefab(newPrefabBase);
            }
        }

        private struct UpdateTempPlaceholderJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                   m_EntityTypeHandle;
            [ReadOnly] public required ZoneType                           m_ZoneType;
            [ReadOnly] public required bool                               m_AllowSpawn;
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

                    if (m_AllowSpawn) {
                        m_CommandBuffer.AddComponent<ParcelSpawnable>(index, entity);
                    }

                    m_CommandBuffer.AddComponent<Updated>(index, entity);
                }
            }
        }

        private struct SwapPlaceholderRefJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>      m_PrefabRefTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData>         m_ParcelDataLookup;
            [ReadOnly] public required NativeHashMap<int, Entity>.ReadOnly m_PrefabCache;
            public required            EntityCommandBuffer.ParallelWriter  m_CommandBuffer;
            public required            bool                                toPermanent;

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var prefabRefArray = chunk.GetNativeArray(ref m_PrefabRefTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var prefabRef = prefabRefArray[i];

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

                    // Update
                    m_CommandBuffer.AddComponent<Updated>(index, entity);
                }
            }
        }

    }
}