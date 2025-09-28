// <copyright file="ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.Serialization.Entities;
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
    public partial class ParcelInitializeSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_PrefabQuery;
        private EntityQuery m_LateInitializeQuery;

        // Systems & References
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelInitializeSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Queries
            m_PrefabQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<PrefabData>(),
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadWrite<ParcelData>()
                    }
                });

            // Adding BuildingData in the same frame that the prefab entity is created causing a conflict with vanilla BuildingInitializeSystem.
            // This query allows us to initialize this portion later than the vanilla buildings.
            m_LateInitializeQuery = SystemAPI.QueryBuilder()
                .WithAll<PrefabData, ParcelData>()
                .WithNone<Game.Prefabs.BuildingData>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            if (m_LateInitializeQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            NativeArray<Entity> prefabEntities = m_LateInitializeQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < prefabEntities.Length; i++)
            {
                Entity currentPrefabEntity = prefabEntities[i];
                if (EntityManager.TryGetComponent(currentPrefabEntity, out ParcelData parcelData))
                {
                    Game.Prefabs.BuildingData buildingData = new Game.Prefabs.BuildingData()
                    {
                        m_Flags = BuildingFlags.RequireRoad,
                        m_LotSize = parcelData.m_LotSize,
                    };
                    EntityManager.AddComponent<Game.Prefabs.BuildingData>(currentPrefabEntity);
                    EntityManager.SetComponentData(currentPrefabEntity, buildingData);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"OnUpdate() -- Found {entities.Length} prefabs to initialize.");

            for (var i = 0; i < entities.Length; i++) {
                var currentEntity = entities[i];

                // @todo
                // Looking at game code, I think its cleaner to get PrefabRef, which then can get prefab data directly
                // with m_prefab
                var prefabData = EntityManager.GetComponentData<PrefabData>(currentEntity);

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
                var parcelData = EntityManager.GetComponentData<ParcelData>(currentEntity);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                parcelData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                EntityManager.SetComponentData<ParcelData>(currentEntity, parcelData);

                // Some dimensions.
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

                // Geometry data
                var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(currentEntity);
                oGeoData.m_MinLod = 100;
                oGeoData.m_Size = parcelGeo.Size;
                oGeoData.m_Pivot = parcelGeo.Pivot;
                oGeoData.m_LegSize = new float3(0f, 0f, 0f);
                oGeoData.m_Bounds = parcelGeo.Bounds;
                oGeoData.m_Layers = MeshLayer.First;
                oGeoData.m_Flags &= ~GeometryFlags.Overridable;
                oGeoData.m_Flags |= GeometryFlags.Physical | GeometryFlags.WalkThrough;
                EntityManager.SetComponentData<ObjectGeometryData>(currentEntity, oGeoData);

                // Placeable data
                var placeableData = EntityManager.GetComponentData<PlaceableObjectData>(currentEntity);
                placeableData.m_Flags |= Game.Objects.PlacementFlags.RoadSide | Game.Objects.PlacementFlags.SubNetSnap | Game.Objects.PlacementFlags.OnGround;
                placeableData.m_PlacementOffset = new float3(100f, 0, 100f);
                EntityManager.SetComponentData<PlaceableObjectData>(currentEntity, placeableData);

                //// Terraform Data
                // var terraformData = EntityManager.GetComponentData<BuildingTerraformData>(currentEntity);
                // terraformData.m_FlatX0 = new float3(0f);
                // terraformData.m_FlatZ0 = new float3(0f);
                // terraformData.m_FlatX1 = new float3(0f);
                // terraformData.m_FlatZ1 = new float3(0f);
                // terraformData.m_Smooth = new float4(-40f, -32f, 40f, 32f);
                // terraformData.m_HeightOffset = 0f;
                // terraformData.m_DontRaise = false;
                // terraformData.m_DontLower = false;
                // EntityManager.SetComponentData<BuildingTerraformData>(currentEntity, terraformData);

                // Finished
                m_Log.Debug($"OnUpdate() -- Finished initializing {parcelPrefabRef} on entity {currentEntity.Index}");
            }
        }
    }
}
