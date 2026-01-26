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
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using static Colossal.AssetPipeline.Diagnostic.Report;

    #endregion

    public partial class P_PlaceholderSystem  : PlatterGameSystemBase {
        private EntityQuery           m_TempQuery;
        private P_UISystem            m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_PlatterUISystem      = World.GetOrCreateSystemManaged<P_UISystem>();

            // Parcels (with ParcelPlaceholder) created by a tool (Temp)
            m_TempQuery = SystemAPI.QueryBuilder()
                                   .WithAll<ParcelPlaceholder, Temp>()
                                   .WithNone<Deleted>()
                                   .Build();

            RequireForUpdate(m_TempQuery);
        }

        protected override void OnUpdate() {
            // Job to set the prezone data on a temp placeholder parcel
            var updateTempPlaceholderJobHandle = new UpdateTempPlaceholderJob {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_ZoneType = m_PlatterUISystem.PreZoneType,
                m_ParcelTypeHandle = SystemAPI.GetComponentTypeHandle<Parcel>(),
            }.Schedule(m_TempQuery, Dependency);

            Dependency = JobHandle.CombineDependencies(updateTempPlaceholderJobHandle, Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateTempPlaceholderJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle            m_EntityTypeHandle;
            [ReadOnly] public required ZoneType                    m_ZoneType;
            public required            ComponentTypeHandle<Parcel> m_ParcelTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var parcelArray = chunk.GetNativeArray(ref m_ParcelTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var parcel = parcelArray[i];

                    if (parcel.m_PreZoneType.Equals(m_ZoneType)) {
                        continue;
                    }

                    parcel.m_PreZoneType = m_ZoneType;
                    parcelArray[i]       = parcel;
                }
            }
        }
    }
}