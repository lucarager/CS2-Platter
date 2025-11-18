// <copyright file="P_ParcelBlockUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using static UnityEngine.Rendering.DebugUI;

    /// <summary>
    /// Updates parcel data whenever block data updates.
    /// </summary>
    public partial class P_BlockUpdateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BlockUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Block, Cell, ParcelOwner, Updated>()
                               .WithNone<Deleted, Temp>()
                               .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            foreach (var entity in m_Query.ToEntityArray(Allocator.Temp)) {
                var parcelOwner    = EntityManager.GetComponentData<ParcelOwner>(entity);
                var block          = EntityManager.GetComponentData<Block>(entity);
                var cellBuffer     = EntityManager.GetBuffer<Cell>(entity);
                var parcel         = EntityManager.GetComponentData<Parcel>(parcelOwner.m_Owner);
                var prefabRef      = EntityManager.GetComponentData<PrefabRef>(parcelOwner.m_Owner);
                var parcelData     = EntityManager.GetComponentData<ParcelData>(prefabRef.m_Prefab);
                var containedZones = new Dictionary<ZoneType, int>();

                for (var col = 0; col < block.m_Size.x; col++) {
                    for (var row = 0; row < block.m_Size.y; row++) {
                        var index = (row * block.m_Size.x) + col;
                        var cell  = cellBuffer[index];

                        // Set all cells outside of parcel bounds to occupied
                        // todo move this out to its own system
                        if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                            cell.m_State      = CellFlags.Blocked;
                            cellBuffer[index] = cell;
                        } else {
                            // Count cell zones if inside parcel
                            if (containedZones.TryGetValue(cell.m_Zone, out var current)) {
                                containedZones[cell.m_Zone] = current + 1;
                            } else {
                                containedZones[cell.m_Zone] = 1;
                            }
                        }
                    }
                }

                if (containedZones.Count == 1) {
                    // If we only have one zone, set the Parcel to that
                    // Temporary fix, we should find the root cause for this
                    if (parcelOwner.m_Owner == Entity.Null) {
                        return;
                    }
                    parcel.m_PreZoneType = containedZones.Keys.ToList()[0];
                    EntityManager.SetComponentData<Parcel>(parcelOwner.m_Owner, parcel);
                } else {
                    // Otherwise, set it to a "mix"
                    parcel.m_ZoneFlags = ParcelZoneFlags.Mixed;
                }
            }
        }
    }
}
