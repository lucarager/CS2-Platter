// <copyright file="ObjectToolSystemFieldAccessor.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Patches {
    #region Using Statements

    using System;
    using System.Reflection;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using HarmonyLib;
    using Unity.Collections;

    #endregion

    /// <summary>
    /// Cached field accessors for ObjectToolSystem to avoid repeated reflection lookups in hot paths.
    /// </summary>
    internal static class ObjectToolSystemFieldAccessor {
        private static readonly Func<ObjectToolSystem, Game.Zones.SearchSystem> getZoneSearchSystem;
        private static readonly Func<ObjectToolSystem, Game.Net.SearchSystem> getNetSearchSystem;
        private static readonly Func<ObjectToolSystem, TerrainSystem> getTerrainSystem;
        private static readonly Func<ObjectToolSystem, WaterSystem> getWaterSystem;
        private static readonly Func<ObjectToolSystem, NativeList<ControlPoint>> getControlPoints;
        private static readonly Func<ObjectToolSystem, PrefabBase> getPrefab;

        static ObjectToolSystemFieldAccessor() {
            getZoneSearchSystem = CreateFieldGetter<Game.Zones.SearchSystem>("m_ZoneSearchSystem");
            getNetSearchSystem = CreateFieldGetter<Game.Net.SearchSystem>("m_NetSearchSystem");
            getTerrainSystem = CreateFieldGetter<TerrainSystem>("m_TerrainSystem");
            getWaterSystem = CreateFieldGetter<WaterSystem>("m_WaterSystem");
            getControlPoints = CreateFieldGetter<NativeList<ControlPoint>>("m_ControlPoints");
            getPrefab = CreateFieldGetter<PrefabBase>("m_Prefab");
        }

        private static Func<ObjectToolSystem, T> CreateFieldGetter<T>(string fieldName) {
            var fieldInfo = AccessTools.Field(typeof(ObjectToolSystem), fieldName);
            if (fieldInfo == null) {
                PlatterMod.Instance.Log.Debug($"Field '{fieldName}' not found on ObjectToolSystem!");
                return null;
            }

            return instance => (T)fieldInfo.GetValue(instance);
        }

        /// <summary>
        /// Gets the cached zone search system field value.
        /// </summary>
        public static Game.Zones.SearchSystem GetZoneSearchSystem(ObjectToolSystem instance) =>
            getZoneSearchSystem?.Invoke(instance);

        /// <summary>
        /// Gets the cached net search system field value.
        /// </summary>
        public static Game.Net.SearchSystem GetNetSearchSystem(ObjectToolSystem instance) =>
            getNetSearchSystem?.Invoke(instance);

        /// <summary>
        /// Gets the cached terrain system field value.
        /// </summary>
        public static TerrainSystem GetTerrainSystem(ObjectToolSystem instance) =>
            getTerrainSystem?.Invoke(instance);

        /// <summary>
        /// Gets the cached water system field value.
        /// </summary>
        public static WaterSystem GetWaterSystem(ObjectToolSystem instance) =>
            getWaterSystem?.Invoke(instance);

        /// <summary>
        /// Gets the cached control points field value.
        /// </summary>
        public static NativeList<ControlPoint> GetControlPoints(ObjectToolSystem instance) =>
            getControlPoints?.Invoke(instance) ?? default;

        /// <summary>
        /// Gets the cached prefab field value.
        /// </summary>
        public static PrefabBase GetPrefab(ObjectToolSystem instance) =>
            getPrefab?.Invoke(instance);
    }
}
