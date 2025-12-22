// <copyright file="P_RemoveOverriddenSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for patching parcels and removing the overridable flag.
    /// </summary>
    public partial class P_RemoveOverriddenSystem : PlatterGameSystemBase {
        private EntityQuery    m_Query;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_RemoveOverriddenSystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_Query = SystemAPI.QueryBuilder()
                               .WithAll<Parcel, Initialized, Overridden>()
                               .WithNone<Deleted, Temp>()
                               .Build();

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() { EntityManager.RemoveComponent<Overridden>(m_Query); }
    }
}