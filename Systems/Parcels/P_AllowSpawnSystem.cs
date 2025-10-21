// <copyright file="P_ParcelSpawnSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for adding/removing the AllowSpawn component.
    /// </summary>
    public partial class P_AllowSpawnSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_EnabledQuery;
        private EntityQuery m_DisabledQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_AllowSpawnSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_EnabledQuery = SystemAPI.QueryBuilder()
                                      .WithAll<Parcel, ParcelSpawnable>()
                                      .WithNone<Deleted, Temp>()
                                      .Build();
            m_DisabledQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Parcel>()
                                       .WithNone<ParcelSpawnable, Deleted, Temp>()
                                       .Build();
            
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        // Todo convert to job for perf
        public void UpdateSpawning(bool allowSpawn = true) {
            m_Log.Debug($"UpdateSpawning(allowSpawn={allowSpawn})");

            var query = allowSpawn ? m_DisabledQuery : m_EnabledQuery;
            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                if (allowSpawn) {
                    EntityManager.AddComponent<ParcelSpawnable>(entity);
                } else {
                    EntityManager.RemoveComponent<ParcelSpawnable>(entity);
                }

                EntityManager.AddComponent<Updated>(entity);
            }

            entities.Dispose();
        }
    }
}
