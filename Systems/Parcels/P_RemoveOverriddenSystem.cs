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
    /// System responsible for patching parcels and removing the overridable flag.
    /// </summary>
    public partial class P_RemoveOverriddenSystem : GameSystemBase {
        private PrefixedLogger m_Log;
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_RemoveOverriddenSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Parcel, Initialized, Overridden>()
                               .WithNone<Deleted, Temp>()
                               .Build();

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            EntityManager.RemoveComponent<Overridden>(m_Query);
        }
    }
}
