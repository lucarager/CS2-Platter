// <copyright file="P_ParcelPlaceholderMigrationSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// Migrates parcels that still reference placeholder prefabs to use permanent prefabs.
    /// This handles legacy save data where parcels were saved with placeholder prefab references.
    /// </summary>
    internal partial class P_ParcelPlaceholderMigrationSystem : PlatterGameSystemBase {
        private EntityQuery           m_Query;
        private PrefixedLogger        m_Log;
        private P_PrefabsCreateSystem m_PrefabsCreateSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_ParcelPlaceholderMigrationSystem));
            m_Log.Debug("OnCreate()");

            m_PrefabsCreateSystem = World.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

            // Query for parcels whose prefab has ParcelPlaceholderData (indicating it's a placeholder prefab)
            m_Query = GetEntityQuery(
                ComponentType.ReadOnly<Parcel>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var migrationCount = new NativeReference<int>(0, Allocator.TempJob);
            var ecb            = new EntityCommandBuffer(Allocator.TempJob);

            var migrateJobHandle = new MigrateParcelPlaceholdersJob {
                EntityType                  = SystemAPI.GetEntityTypeHandle(),
                PrefabRefType               = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                ParcelDataLookup            = SystemAPI.GetComponentLookup<ParcelData>(true),
                ParcelPlaceholderDataLookup = SystemAPI.GetComponentLookup<ParcelPlaceholderData>(true),
                PrefabCache                 = m_PrefabsCreateSystem.GetReadOnlyPrefabCache(),
                Ecb                         = ecb.AsParallelWriter(),
                MigrationCount              = migrationCount,
            }.Schedule(m_Query, Dependency);

            migrateJobHandle.Complete();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            if (migrationCount.Value > 0) {
                m_Log.Info($"Migrated {migrationCount.Value} parcels from placeholder to permanent prefabs.");
            }

            migrationCount.Dispose();

            Dependency = migrateJobHandle;
        }

        private struct MigrateParcelPlaceholdersJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                       EntityType;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>         PrefabRefType;
            [ReadOnly] public required ComponentLookup<ParcelData>            ParcelDataLookup;
            [ReadOnly] public required ComponentLookup<ParcelPlaceholderData> ParcelPlaceholderDataLookup;
            [ReadOnly] public required NativeHashMap<int, Entity>.ReadOnly    PrefabCache;
            public required            EntityCommandBuffer.ParallelWriter     Ecb;
            public required            NativeReference<int>                   MigrationCount;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray    = chunk.GetNativeArray(EntityType);
                var prefabRefArray = chunk.GetNativeArray(ref PrefabRefType);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity    = entityArray[i];
                    var prefabRef = prefabRefArray[i];

                    // Check if this prefab is a placeholder prefab
                    if (!ParcelPlaceholderDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        continue;
                    }

                    // Get the parcel data from the placeholder prefab to extract dimensions
                    if (!ParcelDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        continue;
                    }

                    var parcelData     = ParcelDataLookup[prefabRef.m_Prefab];
                    var parcelPrefabID = ParcelUtils.GetPrefabID(parcelData.m_LotSize.x, parcelData.m_LotSize.y);
                    var cacheKey       = ParcelUtils.GetCustomHashCode(parcelPrefabID, false); // false = permanent prefab

                    if (!PrefabCache.TryGetValue(cacheKey, out var newPrefabEntity)) {
                        // Warning: permanent prefab not found in cache - leave as-is
                        continue;
                    }

                    // Swap PrefabRef to permanent prefab
                    var newPrefabRef = new PrefabRef { m_Prefab = newPrefabEntity };
                    Ecb.SetComponent(unfilteredChunkIndex, entity, newPrefabRef);

                    // Increment migration count (note: not thread-safe, but gives approximate count)
                    MigrationCount.Value++;
                }
            }
        }
    }
}
