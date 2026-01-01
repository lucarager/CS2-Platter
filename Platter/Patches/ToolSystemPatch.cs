// <copyright file="ObjectToolSystemPatch.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ReSharper disable InconsistentNaming

using Platter.Extensions;

namespace Platter.Patches {
    #region Using Statements

    using System;
    using System.Security.Cryptography;
    using Colossal.Entities;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using HarmonyLib;
    using Platter.Components;
    using Systems;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using static Platter.Systems.P_SnapSystem;

    #endregion

    internal class ToolSystemPatch {

        /// <summary>
        /// Patch to modify the snap behavior when using platter.
        /// </summary>
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

        /// <summary>
        /// Patch to toggle the zone overlay when using platter.
        /// </summary>
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

        /// <summary>
        /// Patch to prevent rotation when snapped in snap mode using platter.
        /// </summary>
        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch("GetAllowRotation")]
        private class ObjectToolSystem_GetAllowRotation {
            private static bool Prefix(ObjectToolSystem __instance, ref bool __result) {
                if (!ShouldDisableRotation(__instance)) {
                    return true; // Run original method
                }

                __result = false;
                return false; // Skip original method
            }

            private static bool ShouldDisableRotation(ObjectToolSystem instance) {
                var m_PSnapSystem     = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_SnapSystem>();
                var isSnapped         = m_PSnapSystem.IsSnapped.Value;
                var isSnapping        = m_PSnapSystem.CurrentSnapMode != 0;
                var isNotUsingPlatter = instance.prefab is not ParcelPlaceholderPrefab;

                if (isNotUsingPlatter) {
                    return false;
                }

                return isSnapping && isSnapped;
            }
        }

        /// <summary>
        /// Patch to inject custom snap job when using platter.
        /// </summary>
        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch("SnapControlPoint")]
        private class ObjectToolSystem_SnapControlPoint {
            private static bool Prefix(ObjectToolSystem __instance, JobHandle inputDeps, ref JobHandle __result) {
                var  pSnapSystem        = __instance.World.GetOrCreateSystemManaged<P_SnapSystem>();
                var  parcelSearchSystem = __instance.World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();
                var  prefabSystem       = __instance.World.GetOrCreateSystemManaged<PrefabSystem>();
                var  zoneSearchSystem   = (Game.Zones.SearchSystem)__instance.GetMemberValue("m_ZoneSearchSystem");
                var  netSearchSystem    = (Game.Net.SearchSystem)__instance.GetMemberValue("m_NetSearchSystem");
                var  terrainSystem      = (TerrainSystem)__instance.GetMemberValue("m_TerrainSystem");
                var  waterSystem        = (WaterSystem)__instance.GetMemberValue("m_WaterSystem");
                var  controlPoints      = (NativeList<ControlPoint>)__instance.GetMemberValue("m_ControlPoints");
                var  prefab             = (PrefabBase)__instance.GetMemberValue("m_Prefab");

                if (__instance.prefab is not ParcelPlaceholderPrefab) {
                    return true; 
                }
                
                // Schedule custom job
                var customSnapJobHandle = new P_SnapSystem.AdhocParcelSnapJob {
                    m_ZoneTree                     = zoneSearchSystem.GetSearchTree(true, out var zoneTreeJobHandle),
                    m_NetTree                      = netSearchSystem.GetNetSearchTree(true, out var netTreeJobHandle),
                    m_ParcelTree                   = parcelSearchSystem.GetStaticSearchTree(true, out var parcelTreeJobHandle),
                    m_SnapMode                     = pSnapSystem.CurrentSnapMode,
                    m_ControlPoints                = controlPoints,
                    m_PrefabEntity                 = prefabSystem.GetEntity(prefab),
                    m_BlockComponentLookup         = __instance.GetComponentLookup<Game.Zones.Block>(true),
                    m_ParcelDataComponentLookup    = __instance.GetComponentLookup<ParcelData>(true),
                    m_ParcelOwnerComponentLookup   = __instance.GetComponentLookup<ParcelOwner>(true),
                    m_TransformComponentLookup     = __instance.GetComponentLookup<Game.Objects.Transform>(true),
                    m_ParcelComponentLookup        = __instance.GetComponentLookup<Parcel>(true),
                    m_NodeLookup                   = __instance.GetComponentLookup<Node>(true),
                    m_EdgeLookup                   = __instance.GetComponentLookup<Edge>(true),
                    m_CurveLookup                  = __instance.GetComponentLookup<Curve>(true),
                    m_CompositionLookup            = __instance.GetComponentLookup<Composition>(true),
                    m_PrefabRefLookup              = __instance.GetComponentLookup<PrefabRef>(true),
                    m_NetDataLookup                = __instance.GetComponentLookup<NetData>(true),
                    m_NetGeometryDataLookup        = __instance.GetComponentLookup<NetGeometryData>(true),
                    m_NetCompositionDataLookup     = __instance.GetComponentLookup<NetCompositionData>(true),
                    m_EdgeGeoLookup                = __instance.GetComponentLookup<EdgeGeometry>(true),
                    m_StartNodeGeoLookup           = __instance.GetComponentLookup<StartNodeGeometry>(true),
                    m_EndNodeGeoLookup             = __instance.GetComponentLookup<EndNodeGeometry>(true),
                    m_ConnectedEdgeLookup          = __instance.GetBufferLookup<ConnectedEdge>(),
                    m_TerrainHeightData            = terrainSystem.GetHeightData(),
                    m_WaterSurfaceData             = waterSystem.GetSurfaceData(out var waterSurfaceJobHandle),
                    m_SnapSetback                  = pSnapSystem.CurrentSnapSetback,
                    m_EntityTypeHandle             = __instance.GetEntityTypeHandle(),
                    m_ConnectedParcelLookup        = __instance.GetBufferLookup<ConnectedParcel>(true),
                    m_SubBlockLookup               = __instance.GetBufferLookup<Game.Zones.SubBlock>(true),
                    m_IsSnapped                    = pSnapSystem.IsSnapped,
                }.Schedule(JobUtils.CombineDependencies(inputDeps, zoneTreeJobHandle, netTreeJobHandle, parcelTreeJobHandle, waterSurfaceJobHandle));

                zoneSearchSystem.AddSearchTreeReader(customSnapJobHandle);
                netSearchSystem.AddNetSearchTreeReader(customSnapJobHandle);
                zoneSearchSystem.AddSearchTreeReader(customSnapJobHandle);
                waterSystem.AddSurfaceReader(customSnapJobHandle);

                __result = customSnapJobHandle;
                return false; // Skip original method completely
            }
        }

        /// <summary>
        /// Patch to replace the ParcelPrefab with the Placeholder prefab when using move tool.
        /// </summary>
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

        /// <summary>
        /// Patch to replace the ParcelPrefab with the Placeholder prefab when object tool TrySetPrefab is called.
        /// This currently only happens with FindIt! - vanilla game code does not call TrySetPrefab with ParcelPrefab.
        /// </summary>
        [HarmonyPatch(typeof(ObjectToolSystem))]
        [HarmonyPatch(nameof(ObjectToolSystem.TrySetPrefab))]
        [HarmonyPatch(new[] { typeof(PrefabBase) })]
        private class ObjectToolSystem_TrySetPrefab {
            public static bool Prefix(ObjectToolSystem __instance, ref PrefabBase prefab) {
                var m_PPrefabsCreateSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_PrefabsCreateSystem>();

                if (prefab is not ParcelPrefab) {
                    return true; // Run original method
                }

                var currentPrefabID = prefab.GetPrefabID();
                var cacheKey        = ParcelUtils.GetCustomHashCode(currentPrefabID, true);
                if (m_PPrefabsCreateSystem.TryGetCachedPrefab(cacheKey, out var newPrefabEntity) &&
                    m_PPrefabsCreateSystem.TryGetCachedPrefabBase(newPrefabEntity, out var newPrefabBase) &&
                    newPrefabBase is ObjectPrefab objectPrefab) {
                    prefab = objectPrefab;
                }

                return true; // Run original method with modified prefab
            }
        }
    }
}