namespace Platter.Systems {
    using Colossal.Json;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class ParcelInitializeSystem : GameSystemBase {
        private PrefixedLogger m_Log;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_PrefabQuery;

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
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefabBase))
                    return;

                // Get Parcel ComponentBase
                var parcelPrefabRef = prefabBase.GetComponent<ParcelPrefab>();

                if (!m_PrefabSystem.TryGetEntity(parcelPrefabRef.m_ZoneBlock, out var zoneBlockPrefab))
                    return;

                // Parceldata
                var parcelData = EntityManager.GetComponentData<ParcelData>(currentEntity);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                parcelData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                EntityManager.SetComponentData<ParcelData>(currentEntity, parcelData);
                m_Log.Debug($"OnUpdate() -- Set {currentEntity}'s ParcelData");

                //// Building data
                //var buildingData = EntityManager.GetComponentData<BuildingData>(currentEntity);
                //buildingData.m_LotSize = new int2(parcelPrefabRef.m_LotWidth, parcelPrefabRef.m_LotDepth);
                //buildingData.m_Flags |= BuildingFlags.RequireRoad;
                //EntityManager.SetComponentData<BuildingData>(currentEntity, buildingData);

                // Geometry data
                var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(currentEntity);
                var width = (parcelData.m_LotSize.x * PrefabLoadSystem.m_CellSize) + 1f;
                var depth = (parcelData.m_LotSize.y * PrefabLoadSystem.m_CellSize) + 1f;
                var height = PrefabLoadSystem.m_CellSize;
                oGeoData.m_MinLod = 100;
                oGeoData.m_Size = new float3(width, height, depth);
                oGeoData.m_Pivot = new float3(0f, height, depth / 2);
                oGeoData.m_LegSize = new float3(0f, 0f, 0f);
                oGeoData.m_Bounds = new Colossal.Mathematics.Bounds3(
                    new float3(-width / 2, -height / 2, -depth / 2),
                    new float3(width / 2, height / 2, depth / 2)
                );
                oGeoData.m_Layers = MeshLayer.First;
                oGeoData.m_Flags &= ~Game.Objects.GeometryFlags.Overridable;
                oGeoData.m_Flags |= Game.Objects.GeometryFlags.Standing; // | Game.Objects.GeometryFlags.Circular | Game.Objects.GeometryFlags.CircularLeg;
                EntityManager.SetComponentData<ObjectGeometryData>(currentEntity, oGeoData);
                m_Log.Debug($"OnUpdate() -- Set {currentEntity}'s ObjectGeometryData {oGeoData.m_Size.ToJSONString()}");

                // Placeable data
                var placeableData = EntityManager.GetComponentData<PlaceableObjectData>(currentEntity);
                placeableData.m_Flags |= Game.Objects.PlacementFlags.RoadSide;
                placeableData.m_PlacementOffset = new float3(0, 0, depth / 2);
                EntityManager.SetComponentData<PlaceableObjectData>(currentEntity, placeableData);

                // Terraform Data
                var terraformData = EntityManager.GetComponentData<BuildingTerraformData>(currentEntity);
                terraformData.m_FlatX0 = new float3(0f);
                terraformData.m_FlatZ0 = new float3(0f);
                terraformData.m_FlatX1 = new float3(0f);
                terraformData.m_FlatZ1 = new float3(0f);
                terraformData.m_Smooth = new float4(-40f, -32f, 40f, 32f);
                terraformData.m_HeightOffset = 0f;
                terraformData.m_DontRaise = false;
                terraformData.m_DontLower = false;
                EntityManager.SetComponentData<BuildingTerraformData>(currentEntity, terraformData);
                m_Log.Debug($"OnUpdate() -- Set {currentEntity}'s BuildingTerraformData");

                m_Log.Debug($"OnUpdate() -- Finished initializing {currentEntity}");
            }
        }
    }
}
