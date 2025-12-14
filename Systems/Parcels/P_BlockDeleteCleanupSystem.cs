// <copyright file="P_PatchDeletedParcelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for linking parcels to their blocks.
    /// </summary>
    public partial class P_BlockDeleteCleanupSystem : GameSystemBase {
        // Queries
        private EntityQuery m_ParcelBlockQuery;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_BlockDeleteCleanupSystem));
            m_Log.Debug("OnCreate()");

            m_ParcelBlockQuery = SystemAPI.QueryBuilder()
                                          .WithAll<Block, Owner, ParcelOwner, Deleted>()
                                          .WithNone<Temp>()
                                          .Build();
            
            RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Deletes the Owner component from parcels whose block has been deleted.
            // This patches an issue with mods that remove the SubBlock buffer from roads.
            EntityManager.RemoveComponent<Owner>(m_ParcelBlockQuery);
        }
    }
}