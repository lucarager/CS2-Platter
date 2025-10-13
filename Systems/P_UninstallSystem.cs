// <copyright file="P_UninstallSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for safely uninstalling Platter.
    /// </summary>
    internal partial class P_UninstallSystem : GameSystemBase {
        private PrefixedLogger m_Log;
        private EntityQuery m_ParcelQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(P_UninstallSystem));
            m_Log.Debug($"OnCreate()");
            m_ParcelQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Parcel>(),
            });
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        public void UninstallPlatter() {
            m_Log.Debug($"UninstallPlatter()");

            RemoveAllParcels();
        }

        public void RemoveAllParcels() {
            m_Log.Debug($"RemoveAllParcels()");
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

            m_Log.Debug($"RemoveAllParcels() -- Done.");
        }
    }
}
