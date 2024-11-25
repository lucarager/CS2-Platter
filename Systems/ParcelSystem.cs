

namespace Platter {
    using Colossal.Entities;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine.Scripting;
    using Platter.Systems;
    using Unity.Collections;
    using Platter.Prefabs;
    using static Game.UI.NameSystem;
    using Game.Zones;

    public partial class ParcelSystem : GameSystemBase {
        private ILog m_Log;
        private RandomSeed m_Random;
        private EntityArchetype m_DefinitionArchetype;
        private EndFrameBarrier m_EndFrameBarrier;
        private EntityQuery m_BuildingQuery;
        private bool executed = false;
        private EndFrameBarrier m_Barrier;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityCommandBuffer m_BlockCommandBuffer;
        private Entity m_CachedBuildingEntity;
        private Entity m_CachedEdgeEntity;
        private EntityQuery m_ParcelCreatedQuery;
        private EntityQuery m_UpdatedEdgesQuery;
        private ModificationBarrier1 m_ModificationBarrier;
        private PrefabSystem m_PrefabSystem;

        [Preserve]
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = Mod.Instance.Log;
            m_Random = RandomSeed.Next();
            m_Barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_PrefabSystem = base.World.GetOrCreateSystemManaged<PrefabSystem>();

            m_ParcelCreatedQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Area>(),
                        ComponentType.ReadOnly<Space>(),
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                    }
                }
            });

            m_Log.Info("Loaded ParcelSystem!");

            base.RequireForUpdate(m_ParcelCreatedQuery);
        }

        [Preserve]
        protected override void OnUpdate() {
            this.m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            NativeArray<Entity> entities = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];
                if (!EntityManager.TryGetComponent(entity, out Space space)) {
                    return;
                }
                PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var name = this.m_PrefabSystem.GetPrefab<PrefabBase>(prefabRef).name;

                if (name == PrefabManager.prefabName) {
                    m_Log.Debug($"ParcelSystem: Checking {entity.ToString()}");
                    if (entity == null) {
                        return;
                    }

                    if (EntityManager.HasComponent<Parcel>(entity)) {
                        m_Log.Debug($"ParcelSystem: {entity.ToString()} has component Parcel");
                    }
                    if (EntityManager.HasComponent<ParcelData>(entity)) {
                        m_Log.Debug($"ParcelSystem: {entity.ToString()} has component ParcelData");
                    }
                    if (EntityManager.HasComponent<SubBlock>(entity)) {
                        m_Log.Debug($"ParcelSystem: {entity.ToString()} has component SubBlock");
                    }

                    //this.m_CommandBuffer.AddComponent<ParcelData>(entity);
                    //ParcelData parcelData = new ();
                    //parcelData.m_Position.x = 725.2779f;
                    //parcelData.m_Position.y = 879.4377f;
                    //parcelData.m_Position.z = 250.3719f;
                    //parcelData.m_Size.xy = 2;
                    //this.m_CommandBuffer.SetComponent<ParcelData>(entity, parcelData);
                }
            }
        }
    }
}
