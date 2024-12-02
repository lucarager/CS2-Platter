namespace Platter.Systems {
    using Colossal.Json;
    using Colossal.Logging;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
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
            m_Log = new PrefixedLogger(nameof(ParcelInitializeSystem));

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<PrefabData>(),
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadWrite<ParcelData>()
                    }
                });

            m_Log.Debug($"Loaded system.");

            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            NativeArray<Entity> entities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"Found {entities.Length}");

            for (int i = 0; i < entities.Length; i++) {
                Entity currentEntity = entities[i];
                var prefabData = EntityManager.GetComponentData<PrefabData>(currentEntity);
                m_Log.Debug($"Retrieved prefabData {prefabData.ToString()}");

                // Get prefab data
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefabBase))
                    return;


                m_Log.Debug($"Retrieved PrefabBase {prefabBase.ToString()}");

                // Get Parcel ComponentBase
                var parcel = prefabBase.GetComponent<ParcelPrefab>();

                if (!m_PrefabSystem.TryGetEntity(parcel.m_ZoneBlock, out var zoneBlockPrefab))
                    return;

                m_Log.Debug($"parcel {parcel.m_LotDepth}");

                // Parceldata
                var parcelData = EntityManager.GetComponentData<ParcelData>(currentEntity);
                parcelData.m_LotSize = new int2(parcel.m_LotWidth, parcel.m_LotDepth);
                parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
                EntityManager.SetComponentData<ParcelData>(currentEntity, parcelData);

                // Geometry data
                var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(currentEntity);
                var width = parcelData.m_LotSize.x * ParcelPrefab.m_CellSize;
                var depth = parcelData.m_LotSize.y * ParcelPrefab.m_CellSize;
                var height = ParcelPrefab.m_CellSize; 
                oGeoData.m_MinLod = 100;
                oGeoData.m_Size = new float3(width, height, depth);
                oGeoData.m_Pivot = new float3(0f, height, 0f);
                oGeoData.m_LegSize = new float3(0f, 0f, 0f);
                oGeoData.m_Bounds = new Colossal.Mathematics.Bounds3(
                    new float3(-width / 2, -height / 2, -depth / 2),
                    new float3(width / 2, height / 2, depth / 2)
                );
                oGeoData.m_Layers = MeshLayer.First;
                oGeoData.m_Flags |= Game.Objects.GeometryFlags.Standing;// | Game.Objects.GeometryFlags.Circular | Game.Objects.GeometryFlags.CircularLeg;
                m_Log.Debug($"Setting ObjectGeometryData {oGeoData.m_Size.ToJSONString()}");
                EntityManager.SetComponentData<ObjectGeometryData>(currentEntity, oGeoData);
            }
        }
    }
}
