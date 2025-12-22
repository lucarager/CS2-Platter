// <copyright file="P_UninstallSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Entities;
    using Components;
    using Game;
    using Game.Common;
    using Game.Notifications;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for safely uninstalling Platter.
    /// </summary>
    internal partial class P_UninstallSystem : PlatterGameSystemBase {
        private EntityQuery    m_OrphanedIconQuery;
        private EntityQuery    m_ParcelQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ParcelQuery = SystemAPI.QueryBuilder().WithAll<Parcel>().Build();
            m_OrphanedIconQuery = SystemAPI.QueryBuilder().WithAll<Icon, Owner>().Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { }

        public void RemoveIcons() {
            m_Log.Debug("RemoveIcons()");
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var entities      = m_OrphanedIconQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                if (EntityManager.TryGetComponent<Owner>(entity, out var owner) &&
                    owner.m_Owner == Entity.Null) {
                    commandBuffer.AddComponent<Deleted>(entity);
                }
            }

            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();
        }

        public void UninstallPlatter() {
            m_Log.Debug("UninstallPlatter()");

            RemoveAllParcels();
        }

        public void RemoveAllParcels() {
            m_Log.Debug("RemoveAllParcels()");
            var parcelEntities = m_ParcelQuery.ToEntityArray(Allocator.Temp);

            m_Log.Debug($"RemoveAllParcels() -- Removing {parcelEntities.Length} parcels...");

            foreach (var entity in parcelEntities) {
                var subBlockBuffer = EntityManager.GetBuffer<ParcelSubBlock>(entity);

                // Remove blocks
                foreach (var subBlock in subBlockBuffer) {
                    EntityManager.AddComponent<Deleted>(subBlock.m_SubBlock);
                }

                // Enqueue the parcel itself for deletion
                EntityManager.AddComponent<Deleted>(entity);
            }

            m_Log.Debug("RemoveAllParcels() -- Done.");
        }
    }
}