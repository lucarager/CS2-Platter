// <copyright file="P_ParcelBlockUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Linq;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

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
                .WithAll<Block>()
                .WithAll<Cell>()
                .WithAll<ParcelOwner>()
                .WithAll<Updated>()
                .WithNone<Temp>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                m_Log.Debug($"OnUpdate() -- {entity}");
                var cellBuffer = EntityManager.GetBuffer<Cell>(entity);
                var containedZones = new Dictionary<ZoneType, int>();

                // Check zone types
                foreach (var cell in cellBuffer) {
                    if (containedZones.TryGetValue(cell.m_Zone, out var current)) {
                        containedZones[cell.m_Zone] = current + 1;
                    } else {
                        containedZones[cell.m_Zone] = 1;
                    }
                }

                if (containedZones.Count == 1) {
                    // If we only have one zone, set the Parcel to that
                    var parcelOwner = EntityManager.GetComponentData<ParcelOwner>(entity);
                    var parcel = EntityManager.GetComponentData<Parcel>(parcelOwner.m_Owner);
                    parcel.m_PreZoneType = containedZones.Keys.ToList()[0];
                    EntityManager.SetComponentData<Parcel>(parcelOwner.m_Owner, parcel);
                } else {
                    // Otherwise, set it to a "mix"
                    // todo
                }
            }

            entities.Dispose();
        }
    }
}
