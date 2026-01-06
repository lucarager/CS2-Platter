// <copyright file="P_LoadZoneResolverSystem.cs" company="Luca Rager">
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
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// After `ResolvePrefabsSystem` runs, zone indexes may have changed.
    /// This system patches prezone on parcels by recounting cell zones.
    /// </summary>
    internal partial class P_LoadZoneResolverSystem : PlatterGameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Block, ParcelOwner>()
                .WithNone<Deleted, Temp>()
                .Build();

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var deserializeJobHandle = new DeserializeJob {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                BlockTypeHandle = SystemAPI.GetComponentTypeHandle<Block>(true),
                ParcelOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                CellBufferTypeHandle = SystemAPI.GetBufferTypeHandle<Cell>(true),
                ParcelDataLookup = SystemAPI.GetComponentLookup<ParcelData>(true),
                ParcelLookup = SystemAPI.GetComponentLookup<Parcel>(),
                UnzonedZoneType = P_ZoneCacheSystem.UnzonedZoneType,
            }.ScheduleParallel(m_Query, Dependency);

            Dependency = deserializeJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct DeserializeJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle EntityType;
            [ReadOnly] public required ComponentTypeHandle<Block> BlockTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> ParcelOwnerTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef> PrefabRefTypeHandle;
            [ReadOnly] public required BufferTypeHandle<Cell> CellBufferTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData> ParcelDataLookup;
            public required ComponentLookup<Parcel> ParcelLookup;
            [ReadOnly] public ZoneType UnzonedZoneType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(EntityType);
                var blockArray = chunk.GetNativeArray(ref BlockTypeHandle);
                var parcelOwnerArray = chunk.GetNativeArray(ref ParcelOwnerTypeHandle);
                var prefabRefArray = chunk.GetNativeArray(ref PrefabRefTypeHandle);
                var cellBufferArray = chunk.GetBufferAccessor(ref CellBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity      = entityArray[i];
                    var block       = blockArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    var prefabRef   = prefabRefArray[i];
                    var cellBuffer  = cellBufferArray[i];
                    var parcel      = ParcelLookup[parcelOwner.m_Owner];
                    var parcelData  = ParcelDataLookup[prefabRef.m_Prefab];
                    // BurstLogger.Debug("[P_LoadZoneResolverSystem]", 
                                      $"Re-classifying Parcel entity {entity}");

                    ParcelUtils.ClassifyParcelZoning(ref parcel, in block, in parcelData, in cellBuffer, UnzonedZoneType);

                    ParcelLookup[parcelOwner.m_Owner] = parcel;
                }
            }
        }
    }
}