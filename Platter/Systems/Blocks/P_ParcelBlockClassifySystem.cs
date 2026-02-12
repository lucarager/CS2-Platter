// <copyright file="P_ParcelBlockClassifySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Linq;
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
    /// Updates parcel data whenever block data updates.
    /// </summary>
    public partial class P_ParcelBlockClassifySystem : PlatterGameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Block, Cell, ParcelOwner, Updated>()
                               .WithNone<Deleted, Temp>()
                               .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            Dependency = new ClassifyParcelJob {
                m_EntityTypeHandle      = SystemAPI.GetEntityTypeHandle(),
                m_BlockTypeHandle       = SystemAPI.GetComponentTypeHandle<Block>(true),
                m_ParcelOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                m_PrefabRefTypeHandle   = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                m_CellBufferTypeHandle  = SystemAPI.GetBufferTypeHandle<Cell>(true),
                m_ParcelDataLookup      = SystemAPI.GetComponentLookup<ParcelData>(true),
                m_ParcelLookup          = SystemAPI.GetComponentLookup<Parcel>(),
                m_UnzonedZoneType       = P_ZoneCacheSystem.UnzonedZoneType,
            }.Schedule(m_Query, Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct ClassifyParcelJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Block>       m_BlockTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> m_ParcelOwnerTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>   m_PrefabRefTypeHandle;
            [ReadOnly] public required BufferTypeHandle<Cell>           m_CellBufferTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData>      m_ParcelDataLookup;
            public required            ComponentLookup<Parcel>          m_ParcelLookup;
            [ReadOnly] public ZoneType m_UnzonedZoneType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray      = chunk.GetNativeArray(m_EntityTypeHandle);
                var blockArray       = chunk.GetNativeArray(ref m_BlockTypeHandle);
                var parcelOwnerArray = chunk.GetNativeArray(ref m_ParcelOwnerTypeHandle);
                var prefabRefArray   = chunk.GetNativeArray(ref m_PrefabRefTypeHandle);
                var cellBufferArray  = chunk.GetBufferAccessor(ref m_CellBufferTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var block       = blockArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    var prefabRef   = prefabRefArray[i];
                    var cellBuffer  = cellBufferArray[i];
                    var parcel      = m_ParcelLookup[parcelOwner.m_Owner];
                    var parcelData  = m_ParcelDataLookup[prefabRef.m_Prefab];

                    ParcelUtils.ClassifyParcelZoning(ref parcel, in block, in parcelData, in cellBuffer, m_UnzonedZoneType);

                    m_ParcelLookup[parcelOwner.m_Owner] = parcel;
                }
            }
        }
    }
}