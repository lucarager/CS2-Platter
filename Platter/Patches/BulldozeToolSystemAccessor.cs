// <copyright file="BulldozeToolSystemAccessor.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Patches {
    #region Using Statements

    using System;
    using Game.Tools;
    using HarmonyLib;

    #endregion

    /// <summary>
    /// Cached field accessor for BulldozeToolSystem to access private m_ToolRaycastSystem field.
    /// </summary>
    internal static class BulldozeToolSystemAccessor {
        private static readonly Func<BulldozeToolSystem, ToolRaycastSystem> getToolRaycastSystem;

        static BulldozeToolSystemAccessor() {
            getToolRaycastSystem = CreateFieldGetter();
        }

        private static Func<BulldozeToolSystem, ToolRaycastSystem> CreateFieldGetter() {
            var fieldInfo = AccessTools.Field(typeof(BulldozeToolSystem), "m_ToolRaycastSystem");
            if (fieldInfo == null) {
                PlatterMod.Instance.Log.Debug("Field 'm_ToolRaycastSystem' not found on BulldozeToolSystem!");
                return null;
            }

            return instance => (ToolRaycastSystem)fieldInfo.GetValue(instance);
        }

        /// <summary>
        /// Gets the tool raycast system field value.
        /// </summary>
        public static ToolRaycastSystem GetToolRaycastSystem(BulldozeToolSystem instance) =>
            getToolRaycastSystem?.Invoke(instance);
    }
}
