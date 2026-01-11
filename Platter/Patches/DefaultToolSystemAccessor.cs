// <copyright file="DefaultToolSystemAccessor.cs" company="Luca Rager">
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
    /// Cached field accessor for DefaultToolSystem to access private m_ToolRaycastSystem field.
    /// </summary>
    internal static class DefaultToolSystemAccessor {
        private static readonly Func<DefaultToolSystem, ToolRaycastSystem> getToolRaycastSystem;

        static DefaultToolSystemAccessor() {
            getToolRaycastSystem = CreateFieldGetter();
        }

        private static Func<DefaultToolSystem, ToolRaycastSystem> CreateFieldGetter() {
            var fieldInfo = AccessTools.Field(typeof(DefaultToolSystem), "m_ToolRaycastSystem");
            if (fieldInfo == null) {
                PlatterMod.Instance.Log.Debug("Field 'm_ToolRaycastSystem' not found on DefaultToolSystem!");
                return null;
            }

            return instance => (ToolRaycastSystem)fieldInfo.GetValue(instance);
        }

        /// <summary>
        /// Gets the tool raycast system field value.
        /// </summary>
        public static ToolRaycastSystem GetToolRaycastSystem(DefaultToolSystem instance) =>
            getToolRaycastSystem?.Invoke(instance);
    }
}
