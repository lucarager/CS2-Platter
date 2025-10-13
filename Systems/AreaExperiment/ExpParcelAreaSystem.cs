// <copyright file="ExpParcelAreaSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Platter.Utils;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ExpParcelAreaSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers

        // Queries

        // Systems & References

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ExpParcelAreaSystem));
            m_Log.Debug($"OnCreate()");
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }
    }
}
