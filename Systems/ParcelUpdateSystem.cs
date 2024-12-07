namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Platter.Utils;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class ParcelUpdateSystem : GameSystemBase {
        private PrefixedLogger m_Log;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityQuery m_ParcelCreatedQuery;
        private ModificationBarrier4 m_ModificationBarrier;
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Retriefve Systems
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Queries
            m_ParcelCreatedQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            // Update Cycle
            RequireForUpdate(m_ParcelCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            var entities = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);
            var oldBlocks = new Dictionary<Block, Entity>(32);

            m_Log.Debug($"OnUpdate() -- Found {entities.Length}");

            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                if (!EntityManager.TryGetBuffer<SubBlock>(entity, false, out var subBlockBuffer))
                    return;

                // DELETE state
                if (EntityManager.HasComponent<Deleted>(entity)) {
                    m_Log.Debug($"OnUpdate() -- [DELETE] Deleting this parcelPrefab");

                    // Mark Blocks for deletion
                    for (int j = 0; j < subBlockBuffer.Length; j++) {
                        Entity subBlock = subBlockBuffer[j].m_SubBlock;
                        this.m_CommandBuffer.AddComponent<Deleted>(subBlock, default(Deleted));
                    }

                    return;
                }

                // UPDATE State
                m_Log.Debug($"OnUpdate() -- Running UPDATE logic");

                // Retrieve components
                if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef) ||
                    !m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var lotPrefabBase) ||
                    !EntityManager.TryGetComponent<ParcelData>(prefabRef, out var parcelData) ||
                    !EntityManager.TryGetComponent<ParcelComposition>(entity, out var parcelComposition) ||
                    !EntityManager.TryGetComponent<Transform>(entity, out var transform)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find all required components");
                    return;
                }

                var parcelPrefab = lotPrefabBase.GetComponent<ParcelPrefab>();


                // Store Zoneblock
                parcelComposition.m_ZoneBlockPrefab = parcelData.m_ZoneBlockPrefab;
                m_CommandBuffer.SetComponent<ParcelComposition>(entity, parcelComposition);

                // Retrive zone data
                var blockPrefab = parcelComposition.m_ZoneBlockPrefab;
                if (!EntityManager.TryGetComponent<ZoneBlockData>(blockPrefab, out var zoneBlockData))
                    return;

                // Zone block position & Data
                var curvePosition = default(CurvePosition);
                var block = default(Block);
                var buildOder = default(BuildOrder);
                curvePosition.m_CurvePosition = new float2(1f, 0f);
                block.m_Position = transform.m_Position;
                var forwardVector = math.mul(transform.m_Rotation, new float3(0, 0, 1)).xz;
                block.m_Direction = forwardVector;
                block.m_Size = new int2(parcelPrefab.m_LotWidth, parcelPrefab.m_LotDepth);
                buildOder.m_Order = 0;

                // Set Data
                m_Log.Debug($"OnUpdate() -- Creating Block of size {block.m_Size.x}*{block.m_Size.y} in position: {block.m_Position.ToString()}");

                // For now, we know there's only going to be one block per component
                if (subBlockBuffer.Length > 0) {
                    m_Log.Debug($"OnUpdate() -- Updating the old block...");
                    var subBlockEntity = subBlockBuffer[0].m_SubBlock;
                    m_CommandBuffer.SetComponent<PrefabRef>(subBlockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(subBlockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(subBlockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(subBlockEntity, buildOder);
                    m_CommandBuffer.AddComponent<Updated>(subBlockEntity, default(Updated));
                } else {
                    m_Log.Debug($"OnUpdate() -- Creating a new block...");
                    var blockEntity = m_CommandBuffer.CreateEntity(zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent<PrefabRef>(blockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(blockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(blockEntity, buildOder);
                    m_CommandBuffer.AddComponent<ParcelOwner>(blockEntity, new ParcelOwner(entity));
                    DynamicBuffer<Cell> cellBuffer = m_CommandBuffer.SetBuffer<Cell>(blockEntity);
                    var cellCount = block.m_Size.x * block.m_Size.y;
                    for (int l = 0; l < cellCount; l++) {
                        cellBuffer.Add(default(Cell));
                    }
                }
            }
        }
    }
}
