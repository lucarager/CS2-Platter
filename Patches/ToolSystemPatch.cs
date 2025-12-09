// <copyright file="ObjectToolSystemPatch.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ReSharper disable InconsistentNaming
namespace Platter.Patches {
    #region Using Statements

    using System;
    using Game.Prefabs;
    using Game.Tools;
    using HarmonyLib;
    using Systems;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Utils;

    #endregion

    internal class ToolSystemPatch {
        [HarmonyPatch(typeof(ToolBaseSystem))]
        [HarmonyPatch(nameof(ToolBaseSystem.GetActualSnap))]
        [HarmonyPatch(new[] { typeof(Snap), typeof(Snap), typeof(Snap) })]
        private class ToolBaseSystem_GetActualSnap {
            private static void Postfix(ToolBaseSystem __instance, ref Snap __result) {
                var m_PUISystem    = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_UISystem>();
                var m_ToolSystem   = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ToolSystem>();
                var isUsingPlatter = m_ToolSystem.activePrefab is ParcelPlaceholderPrefab;

                if (!isUsingPlatter) {
                    return;
                }

                if (m_PUISystem.ShowContourLines) {
                    __result |= Snap.ContourLines;
                } else {
                    __result &= ~Snap.ContourLines;
                }
            }
        }

        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch("OnUpdate")]
        private class ObjectToolSystem_OnUpdate {
            private static void Postfix(ObjectToolSystem __instance) {
                var m_PUISystem    = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_UISystem>();
                var isUsingPlatter = __instance.GetPrefab() is ParcelPlaceholderPrefab;

                if (isUsingPlatter) {
                    ObjectToolSystemAccessor.SetRequireZones(__instance, m_PUISystem.ShowZones);
                }
            }
        }

        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch("GetAllowRotation")]
        private class ObjectToolSystem_GetAllowRotation {
            private static void Postfix(ObjectToolSystem __instance, ref bool __result) {
                var m_PSnapSystem  = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_SnapSystem>();
                var isSnapped      = m_PSnapSystem.IsSnapped.Value;
                var isInSnapMode   = m_PSnapSystem.CurrentSnapMode is not P_SnapSystem.SnapMode.None;
                var isUsingPlatter = __instance.GetPrefab() is ParcelPlaceholderPrefab;

                if (!isUsingPlatter) {
                    return;
                }

                if (isInSnapMode && isSnapped) {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch(nameof(ObjectToolSystem.StartMoving))]
        private class ObjectToolSystem_StartMoving {
            public static void Postfix(ObjectToolSystem __instance) {
                var m_PPrefabsCreateSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

                if (__instance.prefab is not ParcelPrefab) {
                    return;
                }

                var currentPrefabID = __instance.prefab.GetPrefabID();
                var cacheKey        = ParcelUtils.GetCustomHashCode(currentPrefabID, true);
                if (m_PPrefabsCreateSystem.TryGetCachedPrefab(cacheKey, out var newPrefabEntity) &&
                    m_PPrefabsCreateSystem.TryGetCachedPrefabBase(newPrefabEntity, out var newPrefabBase) &&
                    newPrefabBase is ObjectPrefab objectPrefab) {
                    __instance.prefab = objectPrefab;
                }
            }
        }
    }
}