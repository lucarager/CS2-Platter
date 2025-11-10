// <copyright file="P_ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Components;
    using Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// System responsible for initializing ParcelPrefab data.
    /// This runs after ObjectInitializeSystem and manually sets things like geometry data.
    /// </summary>
    public partial class P_ParcelInitializeSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        public const GeometryFlags PermGeometryFlags = GeometryFlags.Overridable | GeometryFlags.Brushable | GeometryFlags.LowCollisionPriority;
        public const GeometryFlags TempGeometryFlags = GeometryFlags.WalkThrough;

        // Queries
        private EntityQuery m_PrefabQuery;

        // Systems & References
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelInitializeSystem));
            m_Log.Debug("OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_PrefabQuery = SystemAPI.QueryBuilder()
                                     .WithAll<PrefabData, Created>()
                                     .WithAllRW<ParcelData>()
                                     .WithAllRW<ObjectGeometryData>()
                                     .WithAllRW<PlaceableObjectData>()
                                     .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            var prefabEntities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"OnUpdate() -- Found {prefabEntities.Length} prefabs to initialize.");

            foreach (var prefabEntity in prefabEntities) {
                var prefabData = EntityManager.GetComponentData<PrefabData>(prefabEntity);

                // Get prefab data
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefabBase)) {
                    return;
                }

                // Get Parcel ComponentBase
                var parcelPrefabRef = prefabBase.GetComponent<ParcelPrefab>();

                if (!m_PrefabSystem.TryGetEntity(parcelPrefabRef.m_ZoneBlock, out var zoneBlockPrefab)) {
                    return;
                }

                // Parceldata
                var parcelData = EntityManager.GetComponentData<ParcelData>(prefabEntity);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                parcelData.m_LotSize         = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
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
                oGeoData.m_Flags   = PermGeometryFlags;
                EntityManager.SetComponentData(prefabEntity, oGeoData);

                // Geometry data
                EntityManager.SetComponentData(
                    prefabEntity, new PlaceableObjectData {
                        m_Flags = PlacementFlags
                        .OwnerSide, // Ownerside added to make sure EDT doesn't pick up parcels. Temporary fix until we can patch
                        // EDT or Platter accordingly
                        m_PlacementOffset  = new float3(0, 0, 0),
                        m_ConstructionCost = 0,
                        m_XPReward         = 0,
                    });

                m_Log.Debug($"OnUpdate() -- Initialized Parcel {parcelData.m_LotSize.x}x{parcelData.m_LotSize.y}.");
            }

            EntityManager.AddComponent<Updated>(m_PrefabQuery);

            prefabEntities.Dispose();
        }
    }
}