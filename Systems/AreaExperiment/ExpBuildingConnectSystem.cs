// <copyright file="ParcelAreaSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ExpBuildingConnectSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers

        // Queries
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ExpBuildingConnectSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems

            // Queries
            m_Query = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Building>(),
                        ComponentType.ReadOnly<UnderConstruction>(),
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<BatchesUpdated>()
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<ConnectedParcel>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                m_Log.Debug($"Building was updated {entity}");
            }
        }
    }
}
