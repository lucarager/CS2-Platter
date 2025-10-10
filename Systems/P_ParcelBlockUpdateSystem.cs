// <copyright file="P_ZoningUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class P_ParcelBlockUpdateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelBlockUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                .WithAll<Block>()
                .WithAll<Cell>()
                //.WithAllRW<VacantLot>()
                .WithAll<ParcelOwner>()
                .WithAll<Updated>()
                .WithNone<Temp>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                m_Log.Debug($"OnUpdate() -- {entity}");
                var cellBuffer = EntityManager.GetBuffer<Cell>(entity);
                var containedZones = new Dictionary<ZoneType, int>();

                // Check zone types
                for (var i = 0; i < cellBuffer.Length; i++) {
                    var cell = cellBuffer[i];

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

                    //Also, set the VacantLot min so that nothing spawns that's smaller than the parcel
                    //var vacantLotBuffer = EntityManager.GetBuffer<VacantLot>(entity, false);
                    //if (vacantLotBuffer.Length == 1) {
                    //    var lot = vacantLotBuffer[0];
                    //    lot.m_Area = new int4(lot.m_Area.y, lot.m_Area.y, lot.m_Area.w, lot.m_Area.w);
                    //    vacantLotBuffer[0] = lot;
                    //}
                } else {
                    // Otherwise, set it to a "mix"
                    // todo
                }
            }

            entities.Dispose();
        }
    }
}
