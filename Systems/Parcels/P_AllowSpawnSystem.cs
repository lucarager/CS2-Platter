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
        private EntityQuery m_SpawnableQuery;
        private EntityQuery m_NotSpawnableQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_AllowSpawnSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_SpawnableQuery = SystemAPI.QueryBuilder()
                                      .WithAll<Parcel, Initialized, ParcelSpawnable>()
                                      .WithNone<Deleted, Temp>()
                                      .Build();
            m_NotSpawnableQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Parcel, Initialized>()
                                       .WithNone<ParcelSpawnable, Deleted, Temp>()
                                       .Build();
            
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        public void UpdateSpawning(bool allowSpawn = true) {
            m_Log.Debug($"UpdateSpawning(allowSpawn={allowSpawn})");
            if (allowSpawn) {
                EntityManager.AddComponent<ParcelSpawnable>(m_NotSpawnableQuery);
                EntityManager.AddComponent<Updated>(m_NotSpawnableQuery);
            } else {
                EntityManager.RemoveComponent<ParcelSpawnable>(m_SpawnableQuery);
                EntityManager.AddComponent<Updated>(m_SpawnableQuery);
            }
        }
    }
}
