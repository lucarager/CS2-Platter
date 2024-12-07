namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    public partial class ParcelBlockRoadReferenceSystem : GameSystemBase {
        private EntityQuery m_ParcelUpdatedQuery;
        private PrefixedLogger m_Log;
        private EntityCommandBuffer m_CommandBuffer;
        private ModificationBarrier5 m_ModificationBarrier;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(ParcelBlockRoadReferenceSystem));
            m_Log.Debug($"OnCreate()");

            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier5>();

            m_ParcelUpdatedQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            base.RequireForUpdate(m_ParcelUpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate() -- Updating Percel->Block->Road ownership references");

            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();

            var parcelEntities = m_ParcelUpdatedQuery.ToEntityArray(Allocator.Temp);
            var subBlockBufferLookup = GetBufferLookup<SubBlock>(true);

            for (int i = 0; i < parcelEntities.Length; i++) {
                var parcelEntity = parcelEntities[i];
                var parcelData = EntityManager.GetComponentData<Parcel>(parcelEntity);

                m_Log.Debug($"OnUpdate() -- Updating references for parcel {parcelEntity}");

                if (!EntityManager.TryGetBuffer<SubBlock>(parcelEntity, false, out var subBlockBuffer)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find parcel's {parcelEntity} subblock buffer");
                    return;
                }

                for (int j = 0; j < subBlockBuffer.Length; j++) {
                    var subBlock = subBlockBuffer[j];
                    var blockEntity = subBlock.m_SubBlock;
                    var curvePosition = EntityManager.GetComponentData<CurvePosition>(blockEntity);
                    curvePosition.m_CurvePosition = parcelData.m_CurvePosition;
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);

                    if (EntityManager.TryGetComponent<Owner>(blockEntity, out var owner)) {
                        // Update logic
                        owner.m_Owner = parcelData.m_RoadEdge;
                        m_CommandBuffer.SetComponent<Owner>(blockEntity, owner);
                    } else {
                        m_CommandBuffer.AddComponent<Owner>(blockEntity, new Owner(parcelData.m_RoadEdge));
                    }
                }
            }
        }
    }
}
