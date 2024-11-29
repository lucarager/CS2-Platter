namespace Platter.Systems {
    using Colossal.Logging;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    public partial class ParcelInitializeSystem : GameSystemBase {
        private ILog m_Log;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_PrefabQuery;
        private string m_LogHeader;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = Mod.Instance.Log;
            m_LogHeader = $"[ParcelInitializeSystem]";

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<PrefabData>(),
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadWrite<ParcelData>()
                    }
                });

            m_Log.Debug($"{m_LogHeader} Loaded system.");

            RequireForUpdate(m_PrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            NativeArray<Entity> entities = m_PrefabQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"{m_LogHeader} Found {entities.Length}");

            for (int i = 0; i < entities.Length; i++) {
                Entity currentEntity = entities[i];

                try {
                    var prefabData = EntityManager.GetComponentData<PrefabData>(currentEntity);
                    m_Log.Debug($"{m_LogHeader} Retrieved prefabData {prefabData.ToString()}");

                    // Get prefab data
                    if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var spacePrefab))
                        return;
                    m_Log.Debug($"{m_LogHeader} Retrieved PrefabBase {spacePrefab.ToString()}");

                    // Get Parcel ComponentBase
                    var parcel = spacePrefab.GetComponent<ParcelSpace>();
                    m_Log.Debug($"{m_LogHeader} Retrieved Parcel ComponentBase {parcel.ToString()} and m_ZoneBlock set to {parcel.m_ZoneBlock}");

                    // Set zone block prefab
                    if (parcel.m_ZoneBlock != null) {
                        var parcelData = default(ParcelData);
                        parcelData.m_ZoneBlockPrefab = m_PrefabSystem.GetEntity(parcel.m_ZoneBlock);
                        EntityManager.SetComponentData(currentEntity, parcelData);
                        m_Log.Debug($"{m_LogHeader} Setting Zone block Prefab on prefabData. {parcelData.m_ZoneBlockPrefab.ToString()}");
                    }
                } catch {
                    m_Log.Debug($"{m_LogHeader} Error");
                }
            }
        }
    }
}
