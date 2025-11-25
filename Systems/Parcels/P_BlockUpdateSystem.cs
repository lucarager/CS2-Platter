// <copyright file="P_BlockUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using Components;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// Updates parcel data whenever block data updates.
    /// </summary>
    public partial class P_BlockUpdateSystem : GameSystemBase {
        // Queries
        private EntityQuery m_Query;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BlockUpdateSystem));
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
        // Todo convert to job for perf
        protected override void OnUpdate() {
            foreach (var entity in m_Query.ToEntityArray(Allocator.Temp)) {
                var parcelOwner    = EntityManager.GetComponentData<ParcelOwner>(entity);
                var block          = EntityManager.GetComponentData<Block>(entity);
                var cellBuffer     = EntityManager.GetBuffer<Cell>(entity);
                var prefabRef      = EntityManager.GetComponentData<PrefabRef>(parcelOwner.m_Owner);
                var parcelData     = EntityManager.GetComponentData<ParcelData>(prefabRef.m_Prefab);

                for (var col = 0; col < block.m_Size.x; col++)
                for (var row = 0; row < block.m_Size.y; row++) {
                    var index = row * block.m_Size.x + col;
                    var cell  = cellBuffer[index];

                    // Set all cells outside of parcel bounds to occupied
                    if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                        cell.m_State      = CellFlags.Blocked;
                        cellBuffer[index] = cell;
                    }
                }

            }
        }
    }
}