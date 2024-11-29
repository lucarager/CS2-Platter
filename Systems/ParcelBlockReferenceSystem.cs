namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Colossal.Logging;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    public partial class ParcelBlockReferenceSystem : GameSystemBase {
        private EntityQuery m_BlockQuery;
        private EntityQuery m_ParcelQuery;
        private ILog m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = Mod.Instance.Log;

            m_BlockQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Block>(),
                ComponentType.ReadOnly<Created>(),
                ComponentType.Exclude<Temp>()
            });
            m_ParcelQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Space>(),
                ComponentType.ReadOnly<Parcel>(),
                ComponentType.Exclude<Temp>()
            });

            m_Log.Info("Loaded ParcelBlockReferenceSystem!");

            base.RequireForUpdate(m_BlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            NativeArray<Entity> blocks = m_BlockQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> parcels = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            //if (chunk.Has<Created>(ref this.m_CreatedType)) {
            //    for (int i = 0; i < blockEntities.Length; i++) {
            //        Entity block = blockEntities[i];
            //        Owner owner = edgeEntities[i];
            //        CollectionUtils.TryAddUniqueValue<SubBlock>(this.m_Blocks[owner.m_Owner], new SubBlock(block));
            //    }
            //    return;
            //}
            //for (int j = 0; j < blockEntities.Length; j++) {
            //    Entity block2 = blockEntities[j];
            //    Owner owner2 = edgeEntities[j];
            //    CollectionUtils.RemoveValue<SubBlock>(this.m_Blocks[owner2.m_Owner], new SubBlock(block2));
            //}
            var subBlockBuffersOfAllEntities = GetBufferLookup<SubBlock>(true);
            var block = blocks[0];
            var parcel = parcels[0];
            m_Log.Info($"ParcelBlockReferenceSystem: Selected a block! {block.ToString()}");
            m_Log.Info($"ParcelBlockReferenceSystem: Selected a parcel! {parcel.ToString()}");

            if (!subBlockBuffersOfAllEntities.HasBuffer(parcel))
                return;

            DynamicBuffer<SubBlock> subBlockBuffer = subBlockBuffersOfAllEntities[parcel];

            CollectionUtils.TryAddUniqueValue<SubBlock>(subBlockBuffer, new SubBlock(block));
            m_Log.Info($"ParcelBlockReferenceSystem: Set Buffer");
        }
    }
}
