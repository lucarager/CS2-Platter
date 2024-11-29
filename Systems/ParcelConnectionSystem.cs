namespace Platter.Systems {
    using Colossal.Logging;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    public partial class ParcelConnectionSystem : GameSystemBase {
        private ILog m_Log;
        private EntityQuery m_ParcelQuery;
        private EntityQuery m_EdgeQuery;
        private ModificationBarrier4B m_ModificationBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = Mod.Instance.Log;
            m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier4B>();

            m_ParcelQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Space>(),
                ComponentType.ReadOnly<Parcel>(),
                ComponentType.Exclude<Temp>()
            });

            m_EdgeQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<SubBlock>(),
                ComponentType.ReadOnly<Updated>(),
                ComponentType.Exclude<Temp>()
            });

            m_Log.Info("Loaded ParcelConnectionSystem!");

            base.RequireForUpdate(m_ParcelQuery);
            base.RequireForUpdate(m_EdgeQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();

            NativeArray<Entity> parcels = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> edges = m_EdgeQuery.ToEntityArray(Allocator.Temp);

            if (parcels.Length != 1 || edges.Length != 1) {
                return;
            }

            var parcelEntity = parcels[0];
            m_Log.Info($"ParcelConnectionSystem: Selected a parcelEntity! {parcelEntity.ToString()}");
            var edgeEntity = edges[0];
            m_Log.Info($"ParcelConnectionSystem: Selected a edgeEntity! {edgeEntity.ToString()}");
            var buffer = EntityManager.GetBuffer<SubBlock>(parcelEntity);
            m_Log.Info($"ParcelConnectionSystem: Selected a buffer! {buffer.ToString()}");
            this.m_CommandBuffer.AddComponent<Owner>(buffer[0].m_SubBlock, new Owner {
                m_Owner = edgeEntity
            });
            m_Log.Info($"ParcelConnectionSystem: Set Buffer");
        }
    }
}
