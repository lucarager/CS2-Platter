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
    public partial class P_ParcelBlockClassifySystem : GameSystemBase {
        // Queries
        private EntityQuery m_Query;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelBlockClassifySystem));
            m_Log.Debug("OnCreate()");

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

                    // Initialize state tracking
                    var isZoningUniform    = true;
                    var cachedZone         = ZoneType.None;
                    var roadFlagCountLeft  = 0;
                    var roadFlagCountRight = 0;
                    var roadFlagCountBack  = 0;

                    // Count Cells and Flags within parcel bounds
                    for (var col = 0; col < block.m_Size.x; col++) {
                        for (var row = 0; row < block.m_Size.y; row++) {
                            var index = row * block.m_Size.x + col;
                            var cell  = cellBuffer[index];

                            // Skip cells outside parcel bounds
                            if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                                continue;
                            }

                            // Track zoning uniformity - cache first zone, compare subsequent ones
                            if (cachedZone.m_Index == ZoneType.None.m_Index) {
                                cachedZone = cell.m_Zone;
                            } else if (cell.m_Zone.m_Index != cachedZone.m_Index) {
                                isZoningUniform = false;
                            }

                            // Count road flags: RoadLeft, RoadRight, RoadBack
                            var cellState = cell.m_State;
                            if ((cellState & CellFlags.RoadLeft) != 0) {
                                roadFlagCountLeft++;
                            }

                            if ((cellState & CellFlags.RoadRight) != 0) {
                                roadFlagCountRight++;
                            }

                            if ((cellState & CellFlags.RoadBack) != 0) {
                                roadFlagCountBack++;
                            }
                        }
                    }

                    // Clear the state flags and rebuild
                    parcel.m_State = ParcelStateFlags.None;

                    // Classify zoning
                    parcel.m_PreZoneType = cachedZone;
                    if (isZoningUniform) {
                        parcel.m_State |= ParcelStateFlags.ZoningUniform;
                    } else {
                        parcel.m_State |= ParcelStateFlags.ZoningMixed;
                    }

                    // Add road flags if 2+ cells have them
                    if (roadFlagCountLeft >= 2) {
                        parcel.m_State |= ParcelStateFlags.RoadLeft;
                    }

                    if (roadFlagCountRight >= 2) {
                        parcel.m_State |= ParcelStateFlags.RoadRight;
                    }

                    if (roadFlagCountBack >= 2) {
                        parcel.m_State |= ParcelStateFlags.RoadBack;
                    }

                    // Update the parcel with new state
                    m_ParcelLookup[parcelOwner.m_Owner] = parcel;
                }
            }
        }
    }
}