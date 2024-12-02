namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Zones;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    public partial class ParcelBlockReferenceSystem : GameSystemBase {
        private EntityQuery m_ParcelBlockQuery;
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(ParcelBlockReferenceSystem));

            // TODO Only do this for blocks that should belong to a parcel, not edges!
            this.m_ParcelBlockQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Block>(),
                        ComponentType.ReadOnly<ParcelOwner>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Deleted>()
                    }
                }
            });

            m_Log.Debug($"Loaded System.");

            base.RequireForUpdate(m_ParcelBlockQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"Setting Block ownership references");
            var blockEntities = m_ParcelBlockQuery.ToEntityArray(Allocator.Temp);
            var subBlockBufferLookup = GetBufferLookup<SubBlock>(true);

            for (int i = 0; i < blockEntities.Length; i++) {
                var blockEntity = blockEntities[i];

                m_Log.Debug($"Setting Block ownership references for entity {blockEntity.ToString()}");

                if (!EntityManager.TryGetComponent<ParcelOwner>(blockEntity, out var parcelOwner)) {
                    m_Log.Error($"{blockEntity} didn't have parcelOwner component");
                    return;
                }

                if (subBlockBufferLookup.HasBuffer(parcelOwner.m_Owner)) {
                    m_Log.Error($"Couldn't find owner's {parcelOwner.m_Owner} subblock buffer");
                    return;
                }

                var subBlockBuffer = subBlockBufferLookup[parcelOwner.m_Owner];

                if (EntityManager.HasComponent<Created>(blockEntity)) {
                    if (CollectionUtils.TryAddUniqueValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity))) {
                        m_Log.Debug($"Succesfully added {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
                    } else {
                        m_Log.Error($"Unsuccesfully tried adding {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
                    }
                    return;
                }

                CollectionUtils.RemoveValue<SubBlock>(subBlockBuffer, new SubBlock(blockEntity));
                m_Log.Debug($"Succesfully deleted {blockEntity} to {parcelOwner.m_Owner}'s {subBlockBuffer} buffer");
            }
        }
    }
}
