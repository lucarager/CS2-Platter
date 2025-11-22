// <copyright file="P_ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for initializing ParcelPrefab data.
    /// This runs after ObjectInitializeSystem and manually sets things like geometry data.
    /// </summary>
    public partial class P_ParcelInitializeSystem : GameSystemBase {
        private const GeometryFlags PermGeometryFlags        = GeometryFlags.Overridable | GeometryFlags.Brushable | GeometryFlags.LowCollisionPriority;
        private const GeometryFlags PlaceholderGeometryFlags = GeometryFlags.WalkThrough | GeometryFlags.Brushable;
        private       EntityQuery   m_ParcelPlaceholderPrefabQuery;

        private EntityQuery    m_ParcelPrefabQuery;
        private PrefabSystem   m_PrefabSystem;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelInitializeSystem));
            m_Log.Debug("OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_ParcelPrefabQuery = SystemAPI.QueryBuilder()
                                           .WithAll<PrefabData, Created>()
                                           .WithAllRW<ParcelData>()
                                           .WithAllRW<ObjectGeometryData>()
                                           .WithAllRW<PlaceableObjectData>()
                                           .WithNone<ParcelPlaceholderData>()
                                           .Build();

            m_ParcelPlaceholderPrefabQuery = SystemAPI.QueryBuilder()
                                                      .WithAll<PrefabData, Created, ParcelPlaceholderData>()
                                                      .WithAllRW<ParcelData>()
                                                      .WithAllRW<ObjectGeometryData>()
                                                      .WithAllRW<PlaceableObjectData>()
                                                      .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelPrefabQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            foreach (var prefabEntity in m_ParcelPrefabQuery.ToEntityArray(Allocator.Temp)) {
                InitializePrefab(prefabEntity);
            }

            foreach (var prefabEntity in m_ParcelPlaceholderPrefabQuery.ToEntityArray(Allocator.Temp)) {
                InitializePrefab(prefabEntity, true);
            }

            EntityManager.AddComponent<Updated>(m_ParcelPrefabQuery);
        }

        private void InitializePrefab(Entity prefabEntity, bool placeholder = false) {
            var prefabData = EntityManager.GetComponentData<PrefabData>(prefabEntity);

            // Get prefab data
            if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefabBase)) {
                return;
            }

            // Get Parcel ComponentBase
            int    lotWidth;
            int    lotDepth;
            Entity zoneBlockPrefab;

            if (placeholder) {
                var parcelPrefab = prefabBase.GetComponent<ParcelPlaceholderPrefab>();
                lotWidth        = parcelPrefab.m_LotWidth;
                lotDepth        = parcelPrefab.m_LotDepth;
                zoneBlockPrefab = m_PrefabSystem.GetEntity(parcelPrefab.m_ZoneBlock);
            } else {
                var parcelPrefab = prefabBase.GetComponent<ParcelPrefab>();
                lotWidth        = parcelPrefab.m_LotWidth;
                lotDepth        = parcelPrefab.m_LotDepth;
                zoneBlockPrefab = m_PrefabSystem.GetEntity(parcelPrefab.m_ZoneBlock);
            }

            // Parceldata
            var parcelData = EntityManager.GetComponentData<ParcelData>(prefabEntity);
            parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
            parcelData.m_LotSize         = new int2(lotWidth, lotDepth);
            EntityManager.SetComponentData(prefabEntity, parcelData);

            // Some dimensions.
            var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

            // Geometry data
            var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(prefabEntity);
            oGeoData.m_MinLod  = 1;
            oGeoData.m_Size    = parcelGeo.Size;
            oGeoData.m_Pivot   = parcelGeo.Pivot;
            oGeoData.m_LegSize = new float3(0f, 0f, 0f);
            oGeoData.m_Bounds  = parcelGeo.Bounds;
            oGeoData.m_Layers  = MeshLayer.Default;
            oGeoData.m_Flags   = placeholder ? PlaceholderGeometryFlags : PermGeometryFlags;
            EntityManager.SetComponentData(prefabEntity, oGeoData);

            // Geometry data
            EntityManager.SetComponentData(
                prefabEntity,
                new PlaceableObjectData {
                    m_Flags = PlacementFlags
                        .OwnerSide, // Ownerside added to make sure EDT doesn't pick up parcels. Temporary fix until we can patch
                    // EDT or Platter accordingly
                    m_PlacementOffset  = new float3(0, 0, 0),
                    m_ConstructionCost = 0,
                    m_XPReward         = 0,
                });

            m_Log.Debug($"OnUpdate() -- Initialized Parcel {parcelData.m_LotSize.x}x{parcelData.m_LotSize.y}. placeholder = {placeholder}");
        }
    }
}