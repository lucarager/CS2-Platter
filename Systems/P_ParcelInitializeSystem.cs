// <copyright file="P_ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class P_ParcelInitializeSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_PrefabQuery;

        // Systems & References
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelInitializeSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_PrefabQuery = SystemAPI.QueryBuilder()
                .WithAllRW<ParcelData>()
                .WithAllRW<ObjectGeometryData>()
                .WithAllRW<PlaceableObjectData>()
                .WithAll<PrefabData>()
                .WithAll<Created>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"OnUpdate() -- Found {entities.Length} prefabs to initialize.");

            for (var i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                // @todo
                // Looking at game code, I think its cleaner to get PrefabRef, which then can get prefab data directly
                // with m_prefab
                var prefabData = EntityManager.GetComponentData<PrefabData>(entity);

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
                var parcelData = EntityManager.GetComponentData<ParcelData>(entity);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                parcelData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                EntityManager.SetComponentData<ParcelData>(entity, parcelData);

                // Some dimensions.
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

                // Geometry data
                var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(entity);
                oGeoData.m_MinLod = 100;
                oGeoData.m_Size = parcelGeo.Size;
                oGeoData.m_Pivot = parcelGeo.Pivot;
                oGeoData.m_LegSize = new float3(2f, 2f, 2f);
                oGeoData.m_Bounds = parcelGeo.Bounds;
                oGeoData.m_Layers = MeshLayer.First;
                oGeoData.m_Flags &= ~GeometryFlags.Overridable;
                oGeoData.m_Flags |= GeometryFlags.WalkThrough | GeometryFlags.ExclusiveGround;
                EntityManager.SetComponentData<ObjectGeometryData>(entity, oGeoData);

                // Geometry data
                EntityManager.SetComponentData(entity, new PlaceableObjectData() {
                    m_Flags = Game.Objects.PlacementFlags.OwnerSide, // Ownerside added to make sure EDT doesn't pick up parcels. Temporary fix until we can patch EDT or Platter accordingly
                    m_PlacementOffset = new float3(0, 0, 0),
                    m_ConstructionCost = 0,
                    m_XPReward = 0,
                });

                m_Log.Debug($"OnUpdate() -- Initialized Parcel {parcelData.m_LotSize.x}x{parcelData.m_LotSize.y}.");
            }

            EntityManager.AddComponent<Updated>(m_PrefabQuery);

            entities.Dispose();
        }
    }
}
