// <copyright file="PlatterGameSystemBase.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Game;
    using Utils;

    #endregion

    public abstract partial class PlatterGameSystemBase : GameSystemBase {
        internal PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(GetType().Name);
            m_Log.Debug("OnCreate()");
        }
    }
}