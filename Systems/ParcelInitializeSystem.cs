// <copyright file="ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
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

            // Update Cycle
            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            NativeArray<Entity> entities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"OnUpdate() -- Found {entities.Length} prefabs to initialize.");

            for (int i = 0; i < entities.Length; i++) {
                Entity currentEntity = entities[i];

                // @todo
                // Looking at game code, I think its cleaner to get PrefabRef, which then can get prefab data directly
                // with m_prefab
                var prefabData = EntityManager.GetComponentData<PrefabData>(currentEntity);

                // Get prefab data
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out PrefabBase prefabBase)) {
                    return;
                }

                // Get Parcel ComponentBase
                ParcelPrefab parcelPrefabRef = prefabBase.GetComponent<ParcelPrefab>();

                if (!m_PrefabSystem.TryGetEntity(parcelPrefabRef.m_ZoneBlock, out Entity zoneBlockPrefab)) {
                    return;
                }

                // Parceldata
                ParcelData parcelData = EntityManager.GetComponentData<ParcelData>(currentEntity);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                parcelData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                EntityManager.SetComponentData<ParcelData>(currentEntity, parcelData);

                // Building data
                // var buildingData = EntityManager.GetComponentData<BuildingData>(currentEntity);
                // buildingData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                // buildingData.m_Flags |= BuildingFlags.RequireRoad;
                // EntityManager.SetComponentData<BuildingData>(currentEntity, buildingData);

                // Geometry data
                ObjectGeometryData oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(currentEntity);
                float width = parcelData.m_LotSize.x * PrefabLoadSystem.CellSize;
                float depth = parcelData.m_LotSize.y * PrefabLoadSystem.CellSize;
                float height = 1f;
                oGeoData.m_MinLod = 100;
                oGeoData.m_Size = new float3(width, height, depth);
                oGeoData.m_Pivot = new float3(0f, height, 0f);
                oGeoData.m_LegSize = new float3(0f, 0f, 0f);
                oGeoData.m_Bounds = new Colossal.Mathematics.Bounds3(
                    new float3(-width / 2, -height / 2, -depth / 2),
                    new float3(width / 2, height / 2, depth / 2)
                );
                oGeoData.m_Layers = MeshLayer.First;
                oGeoData.m_Flags &= ~Game.Objects.GeometryFlags.Overridable;
                oGeoData.m_Flags |= Game.Objects.GeometryFlags.Physical;
                EntityManager.SetComponentData<ObjectGeometryData>(currentEntity, oGeoData);

                // Placeable data
                PlaceableObjectData placeableData = EntityManager.GetComponentData<PlaceableObjectData>(currentEntity);
                placeableData.m_Flags |= Game.Objects.PlacementFlags.RoadEdge | Game.Objects.PlacementFlags.OnGround;
                placeableData.m_PlacementOffset = new float3(0, 0, 100f);
                EntityManager.SetComponentData<PlaceableObjectData>(currentEntity, placeableData);

                // Terraform Data
                // BuildingTerraformData terraformData = EntityManager.GetComponentData<BuildingTerraformData>(currentEntity);
                // terraformData.m_FlatX0 = new float3(0f);
                // terraformData.m_FlatZ0 = new float3(0f);
                // terraformData.m_FlatX1 = new float3(0f);
                // terraformData.m_FlatZ1 = new float3(0f);
                // terraformData.m_Smooth = new float4(-40f, -32f, 40f, 32f);
                // terraformData.m_HeightOffset = 0f;
                // terraformData.m_DontRaise = false;
                // terraformData.m_DontLower = false;
                // EntityManager.SetComponentData<BuildingTerraformData>(currentEntity, terraformData);
                // m_Log.Debug($"OnUpdate() -- Set {currentEntity}'s BuildingTerraformData");
                m_Log.Debug($"OnUpdate() -- Finished initializing {parcelPrefabRef} on entity {currentEntity.Index}");
            }
        }
    }
}
