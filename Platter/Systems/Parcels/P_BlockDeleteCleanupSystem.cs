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
    /// System responsible for cleaning up parcel blocks when their associated blocks are deleted.
    /// </summary>
    public partial class P_BlockDeleteCleanupSystem : PlatterGameSystemBase {
        private EntityQuery m_ParcelBlockQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

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