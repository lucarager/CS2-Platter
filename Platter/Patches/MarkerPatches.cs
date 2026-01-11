// <copyright file="MarkerPatches.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ReSharper disable InconsistentNaming

namespace Platter.Patches {
    #region Using Statements

    using System;
    using Colossal.Entities;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using HarmonyLib;
    using Platter.Components;
    using Unity.Entities;
    using Utils;
    using Game.Common;

    #endregion

    /// <summary>
    /// Patches for tool systems to allow parcels (which have Marker geometry flag) to be targeted.
    /// The patches enable marker raycasting and filter results to only allow parcels through.
    /// Should markers be already enabled, no filtering is done, this should ensure compatibility with other mods or base game changes.
    /// </summary>
    internal class MarkerPatches {
        private static readonly PrefixedLogger s_Log = new PrefixedLogger(nameof(MarkerPatches));
        
        [ThreadStatic]
        private static bool m_BulldozeAddedMarkersFlag;
        
        [ThreadStatic]
        private static bool m_DefaultToolAddedMarkersFlag;

        #region BulldozeToolSystem Patches

        /// <summary>
        /// Patch BulldozeToolSystem.InitializeRaycast to enable marker raycasting for parcels.
        /// Tracks whether we added the Markers flag vs the base game adding it.
        /// </summary>
        [HarmonyPatch(typeof(BulldozeToolSystem))]
        [HarmonyPatch("InitializeRaycast")]
        private class BulldozeToolSystem_InitializeRaycast {
            private static void Postfix(BulldozeToolSystem __instance) {
                var toolRaycastSystem = BulldozeToolSystemAccessor.GetToolRaycastSystem(__instance);
                if (toolRaycastSystem == null) {
                    return;
                }

                var hadMarkers = (toolRaycastSystem.raycastFlags & RaycastFlags.Markers) != 0;

                if (!hadMarkers) {
                    toolRaycastSystem.raycastFlags |= RaycastFlags.Markers;
                    m_BulldozeAddedMarkersFlag = true;
                } else {
                    m_BulldozeAddedMarkersFlag = false;
                }
            }
        }

        /// <summary>
        /// Patch BulldozeToolSystem.GetRaycastResult to filter markers when we added the flag.
        /// Only allows parcels through; blocks other markers in game mode.
        /// </summary>
        [HarmonyPatch(typeof(BulldozeToolSystem))]
        [HarmonyPatch("GetRaycastResult")]
        [HarmonyPatch(new Type[] { typeof(ControlPoint) }, new ArgumentType[] { ArgumentType.Ref })]
        private class BulldozeToolSystem_GetRaycastResult1 {
            private static void Postfix(
                BulldozeToolSystem __instance,
                ref bool __result,
                ref ControlPoint controlPoint) {
                FilterMarkerRaycastResult(__instance, ref __result, ref controlPoint, m_BulldozeAddedMarkersFlag);
            }
        }

        /// <summary>
        /// Patch BulldozeToolSystem.GetRaycastResult (overload)
        /// </summary>
        [HarmonyPatch(typeof(BulldozeToolSystem))]
        [HarmonyPatch("GetRaycastResult")]
        [HarmonyPatch(new Type[] { typeof(ControlPoint), typeof(bool) }, new ArgumentType[] { ArgumentType.Ref, ArgumentType.Ref })]
        private class BulldozeToolSystem_GetRaycastResult2 {
            private static void Postfix(
                BulldozeToolSystem __instance,
                ref bool __result,
                ref ControlPoint controlPoint,
                ref bool forceUpdate) {
                FilterMarkerRaycastResult(__instance, ref __result, ref controlPoint, m_BulldozeAddedMarkersFlag);
            }
        }

        #endregion

        #region DefaultToolSystem Patches

        /// <summary>
        /// Patch DefaultToolSystem.InitializeRaycast to enable marker raycasting for parcels.
        /// Tracks whether we added the Markers flag vs the base game adding it.
        /// </summary>
        [HarmonyPatch(typeof(DefaultToolSystem))]
        [HarmonyPatch("InitializeRaycast")]
        private class DefaultToolSystem_InitializeRaycast {
            private static void Postfix(DefaultToolSystem __instance) {
                var toolRaycastSystem = DefaultToolSystemAccessor.GetToolRaycastSystem(__instance);
                if (toolRaycastSystem == null) {
                    return;
                }

                var hadMarkers = (toolRaycastSystem.raycastFlags & RaycastFlags.Markers) != 0;

                if (!hadMarkers) {
                    toolRaycastSystem.raycastFlags |= RaycastFlags.Markers;
                    m_DefaultToolAddedMarkersFlag = true;
                } else {
                    m_DefaultToolAddedMarkersFlag = false;
                }
            }
        }

        /// <summary>
        /// Patch DefaultToolSystem.GetRaycastResult (overload)
        /// </summary>
        [HarmonyPatch(typeof(ToolBaseSystem))]
        [HarmonyPatch("GetRaycastResult")]
        [HarmonyPatch(new Type[] { typeof(Entity), typeof(RaycastHit) }, new ArgumentType[] { ArgumentType.Ref, ArgumentType.Ref })]
        private class ToolBaseSystem_GetRaycastResult1 {
            private static void Postfix(
                ToolBaseSystem __instance,
                ref bool __result,
                ref Entity entity,
                ref RaycastHit hit) {
                if (__instance is not DefaultToolSystem) {
                    return;
                }
                FilterMarkerRaycastResult(__instance, ref __result, ref entity, m_DefaultToolAddedMarkersFlag);
            }
        }

        /// <summary>
        /// Patch ToolBaseSystem.GetRaycastResult (overload)
        /// </summary>
        [HarmonyPatch(typeof(ToolBaseSystem))]
        [HarmonyPatch("GetRaycastResult")]
        [HarmonyPatch(new Type[] { typeof(Entity), typeof(RaycastHit), typeof(bool) }, new ArgumentType[] { ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Ref })]
        private class ToolBaseSystem_GetRaycastResult2 {
            private static void Postfix(
                ToolBaseSystem __instance,
                ref bool __result,
                ref Entity entity,
                ref RaycastHit hit,
                ref bool forceUpdate) {
                if (__instance is not DefaultToolSystem) {
                    return;
                }
                FilterMarkerRaycastResult(__instance, ref __result, ref entity, m_DefaultToolAddedMarkersFlag);
            }
        }

        #endregion

        /// <summary>
        /// Common filtering logic for raycast results.
        /// Blocks non-parcel markers when we added the Markers flag.
        /// </summary>
        private static void FilterMarkerRaycastResult(
            ToolBaseSystem instance,
            ref bool result,
            ref ControlPoint controlPoint,
            bool weAddedMarkersFlag) {
            // If no result, nothing to filter.
            if (!result) {
                return;
            }

            // If we didn't add the Markers flag, don't filter anything.
            if (!weAddedMarkersFlag) {
                return;
            }

            var entityManager = instance.EntityManager;
            var entity = controlPoint.m_OriginalEntity;
            var isParcel = entityManager.HasComponent<Parcel>(entity);

            // It's a parcel; allow it.
            if (isParcel) {
                return;
            }

            // Not a parcel; check if it's a marker and block it if so.
            if (entityManager.TryGetComponent(entity, out PrefabRef prefabRef) &&
                entityManager.TryGetComponent(prefabRef.m_Prefab, out ObjectGeometryData geometryData) &&
                (geometryData.m_Flags & GeometryFlags.Marker) != 0) {
                result = false;
                controlPoint = default;
            }
        }


        /// <summary>
        /// Common filtering logic for raycast results.
        /// Blocks non-parcel markers when we added the Markers flag.
        /// </summary>
        private static void FilterMarkerRaycastResult(
            ToolBaseSystem instance,
            ref bool result,
            ref Entity entity,
            bool weAddedMarkersFlag) {
            // If no result, nothing to filter.
            if (!result) {
                return;
            }

            // If we didn't add the Markers flag, don't filter anything.
            if (!weAddedMarkersFlag) {
                return;
            }

            var entityManager = instance.EntityManager;
            var isParcel = entityManager.HasComponent<Parcel>(entity);

            // It's a parcel; allow it.
            if (isParcel) {
                return;
            }

            // Not a parcel; check if it's a marker and block it if so.
            if (entityManager.TryGetComponent(entity, out PrefabRef prefabRef) &&
                entityManager.TryGetComponent(prefabRef.m_Prefab, out ObjectGeometryData geometryData) &&
                (geometryData.m_Flags & GeometryFlags.Marker) != 0) {
                result = false;
                entity = Entity.Null;
            }
        }
    }
}
