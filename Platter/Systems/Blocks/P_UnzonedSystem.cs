// <copyright file="P_CellUnzonedSystem.cs" company="Luca Rager">
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
    /// Updates parcel data whenever block data updates.
    /// </summary>
    public partial class P_UnzonedSystem : PlatterGameSystemBase {
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
            Dependency = new UpdateBlockCellsJob {
                m_BlockTypeHandle = SystemAPI.GetComponentTypeHandle<Block>(true),
                m_ParcelOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<ParcelOwner>(true),
                m_PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_CellBufferTypeHandle = SystemAPI.GetBufferTypeHandle<Cell>(),
                m_ParcelDataLookup = SystemAPI.GetComponentLookup<ParcelData>(true),
                m_UnzonedZoneType = P_ZoneCacheSystem.UnzonedZoneType,
            }.Schedule(m_Query, Dependency);
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateBlockCellsJob : IJobChunk {
            [ReadOnly] public required ComponentTypeHandle<Block> m_BlockTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<ParcelOwner> m_ParcelOwnerTypeHandle;
            [ReadOnly] public required ComponentLookup<PrefabRef> m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<ParcelData> m_ParcelDataLookup;
            public required BufferTypeHandle<Cell> m_CellBufferTypeHandle;
            [ReadOnly] public ZoneType m_UnzonedZoneType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128 chunkEnabledMask) {
                var blockArray = chunk.GetNativeArray(ref m_BlockTypeHandle);
                var parcelOwnerArray = chunk.GetNativeArray(ref m_ParcelOwnerTypeHandle);
                var cellBufferArray = chunk.GetBufferAccessor(ref m_CellBufferTypeHandle);

                for (var i = 0; i < blockArray.Length; i++) {
                    var block = blockArray[i];
                    var parcelOwner = parcelOwnerArray[i];
                    var cellBuffer = cellBufferArray[i];
                    var prefabRef = m_PrefabRefLookup[parcelOwner.m_Owner];
                    var parcelData = m_ParcelDataLookup[prefabRef.m_Prefab];

                    // BurstLogger.Debug("[P_UnzonedSystem]", $"Block {block} of parcel {parcelOwner.m_Owner} is being updated.");

                    for (var col = 0; col < block.m_Size.x; col++)
                    for (var row = 0; row < block.m_Size.y; row++) {
                        var index = row * block.m_Size.x + col;
                        var cell  = cellBuffer[index];

                        if (col < parcelData.m_LotSize.x && row < parcelData.m_LotSize.y) {
                            if (cell.m_Zone.Equals(ZoneType.None)) {
                                cell.m_Zone       = m_UnzonedZoneType;
                                cell.m_State      = CellFlags.Visible;
                                cellBuffer[index] = cell;
                            }
                        } else {
                            // Set all cells outside of parcel bounds to "blocked" and unzoned
                            cell.m_Zone       = ZoneType.None;
                            cell.m_State      = CellFlags.Blocked;
                            cellBuffer[index] = cell;
                        }
                    }
                }
            }
        }
    }
}