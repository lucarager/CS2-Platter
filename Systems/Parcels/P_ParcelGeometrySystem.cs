// <copyright file="P_ParcelGeometrySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Utils;

    /// <summary>
    /// </summary>
    public partial class P_ParcelGeometrySystem : GameSystemBase {
        private PrefixedLogger   m_Log;
        private EntityQuery      m_OverrriddenParcelPrefabQuery;
        private EntityQuery      m_TempParcelQuery;
        private EntityQuery      m_UnchangedParcelPrefabQuery;
        private ToolSystem       m_ToolSystem;
        private ObjectToolSystem m_ObjectToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelGeometrySystem));
            m_Log.Debug("OnCreate()");
            m_ToolSystem       = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_TempParcelQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Parcel, Temp>()
                                         .WithNone<Deleted>()
                                         .Build();
            m_UnchangedParcelPrefabQuery = SystemAPI.QueryBuilder()
                                                    .WithAll<ParcelData>()
                                                    .WithAllRW<ObjectGeometryData>()
                                                    .WithNone<ParcelGeoOverridden>()
                                                    .Build();
            m_OverrriddenParcelPrefabQuery = SystemAPI.QueryBuilder()
                                                      .WithAll<ParcelData, ParcelGeoOverridden>()
                                                      .WithAllRW<ObjectGeometryData>()
                                                      .Build();

            RequireAnyForUpdate(
                m_TempParcelQuery,
                m_OverrriddenParcelPrefabQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            var overriddenPrefabsExist = !m_OverrriddenParcelPrefabQuery.IsEmpty;

            if (CurrentlyUsingParcelsInObjectTool()) {
                // We are using the tool => make Parcels "markers"
                foreach (var entity in m_UnchangedParcelPrefabQuery.ToEntityArray(Allocator.Temp)) {
                    var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(entity);
                    oGeoData.m_Flags = P_ParcelInitializeSystem.PlaceholderGeometryFlags;
                    EntityManager.AddComponent<ParcelGeoOverridden>(entity);
                    EntityManager.SetComponentData(
                        entity,
                        oGeoData);
                }
            } else if (overriddenPrefabsExist) {
                // We are not using the tool, and some overridden prefabs exist => make Parcels normal
                foreach (var entity in m_OverrriddenParcelPrefabQuery.ToEntityArray(Allocator.Temp)) {
                    var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(entity);
                    oGeoData.m_Flags = P_ParcelInitializeSystem.PermGeometryFlags;
                    EntityManager.RemoveComponent<ParcelGeoOverridden>(entity);
                    EntityManager.SetComponentData(
                        entity,
                        oGeoData);
                }
            }
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPlaceholderPrefab;
        }
    }
}