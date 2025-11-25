// <copyright file="ObjectToolSystemAccessor.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Patches {
    #region Using Statements

    using System;
    using Game.Tools;
    using HarmonyLib;

    #endregion

    public static class ObjectToolSystemAccessor {
        private static readonly Action<ObjectToolSystem, bool> setRequireZonesDelegate;

        static ObjectToolSystemAccessor() {
            var propertyInfo = AccessTools.Property(typeof(ObjectToolSystem), "requireZones");
            var setterMethod = propertyInfo?.GetSetMethod(true);

            if (setterMethod != null) {
                setRequireZonesDelegate = (Action<ObjectToolSystem, bool>)Delegate.CreateDelegate(
                    typeof(Action<ObjectToolSystem, bool>),
                    null, 
                    setterMethod
                );
            } else {
                PlatterMod.Instance.Log.Debug("Protected setter for requireZones not found!");
            }
        }

        // Public method to use the cached delegate
        public static void SetRequireZones(ObjectToolSystem instance, bool value) { setRequireZonesDelegate?.Invoke(instance, value); }
    }
}