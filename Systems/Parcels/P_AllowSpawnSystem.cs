// <copyright file="P_AllowSpawnSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for adding/removing the AllowSpawn component.
    /// </summary>
    public partial class P_AllowSpawnSystem : PlatterGameSystemBase {
        private EntityQuery m_NotSpawnableQuery;

        // Queries
        private EntityQuery m_SpawnableQuery;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_AllowSpawnSystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_SpawnableQuery = SystemAPI.QueryBuilder()
                                        .WithAll<Parcel, ParcelSpawnable>()
                                        .WithNone<Deleted>()
                                        .Build();
            m_NotSpawnableQuery = SystemAPI.QueryBuilder()
                                           .WithAll<Parcel>()
                                           .WithNone<ParcelSpawnable, Deleted>()
                                           .Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { }

        public void UpdateSpawning(bool allowSpawn = true) {
            m_Log.Debug($"UpdateSpawning(allowSpawn={allowSpawn})");

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var query         = allowSpawn ? m_NotSpawnableQuery : m_SpawnableQuery;
            var entities      = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                if (allowSpawn) {
                    commandBuffer.AddComponent<ParcelSpawnable>(entity);
                } else {
                    commandBuffer.RemoveComponent<ParcelSpawnable>(entity);
                }
                commandBuffer.AddComponent<Updated>(entity);
            }

            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();
        }
    }
}