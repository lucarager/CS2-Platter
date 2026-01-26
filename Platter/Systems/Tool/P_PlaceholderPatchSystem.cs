// <copyright file="P_PlaceholderPatchSystem.cs" company="Luca Rager">
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
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using static Colossal.AssetPipeline.Diagnostic.Report;

    #endregion

    public partial class P_PlaceholderPatchSystem  : PlatterGameSystemBase {
        private EntityQuery           m_PlacedQuery;
        private ModificationBarrier1  m_ModificationBarrier1;
        private P_PrefabsCreateSystem m_PPrefabsCreateSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PPrefabsCreateSystem = World.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

            // Parcels (with ParcelPlaceholder) that have been placed (not Temp)
            m_PlacedQuery = SystemAPI.QueryBuilder()
                                     .WithAllRW<ParcelPlaceholder>()
                                     .WithNone<Temp, Deleted>()
                                     .Build();

            RequireForUpdate(m_PlacedQuery);
        }

        protected override void OnUpdate() {
            // Job to swap prefabref once placeholder is placed
            var swapPlaceholderForPermnanentJobHandle = new SwapPlaceholderRefJob() {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_AllowSpawn       = PlatterMod.Instance.Settings.AllowSpawn,
                m_PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                m_ParcelPairCache = m_PPrefabsCreateSystem.GetReadOnlyParcelPairCache(),
                m_CommandBuffer = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
                toPermanent = true,
            }.Schedule(m_PlacedQuery, Dependency);
            m_ModificationBarrier1.AddJobHandleForProducer(swapPlaceholderForPermnanentJobHandle);

            Dependency = JobHandle.CombineDependencies(swapPlaceholderForPermnanentJobHandle, Dependency);
        }


#if USE_BURST
        [BurstCompile]
#endif
        private struct SwapPlaceholderRefJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>      m_PrefabRefTypeHandle;
            [ReadOnly] public required NativeHashMap<Entity, Entity>.ReadOnly m_ParcelPairCache;
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

                    // Use the parcel pair cache to get the paired prefab entity directly
                    if (!m_ParcelPairCache.TryGetValue(prefabRef.m_Prefab, out var pairedPrefabEntity)) {
                        continue;
                    }

                    // Swap PrefabRef
                    prefabRef.m_Prefab = pairedPrefabEntity;
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