namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.Logging;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class ParcelBlockSpawnSystem : GameSystemBase {
        private ILog m_Log;
        private RandomSeed m_Random;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityQuery m_ParcelCreatedQuery;
        private ModificationBarrier4 m_ModificationBarrier;
        private PrefabSystem m_PrefabSystem;
        private string m_LogHeader;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = Mod.Instance.Log;
            m_LogHeader = $"[ParcelBlockSpawnSystem]";
            m_Random = RandomSeed.Next();
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_ParcelCreatedQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Space>(),
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

            m_Log.Debug($"{m_LogHeader} Loaded system.");

            RequireForUpdate(m_ParcelCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            NativeArray<Entity> entities = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);
            NativeParallelHashMap<Block, Entity> oldBlockBuffer = new NativeParallelHashMap<Block, Entity>(32, Allocator.Temp);

            m_Log.Debug($"{m_LogHeader} Found {entities.Length}");

            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];
                if (!EntityManager.TryGetBuffer<SubBlock>(entity, false, out var subBlockBuffer))
                    return;

                // DELETE state
                if (EntityManager.HasComponent<Deleted>(entity)) {
                    m_Log.Debug($"{m_LogHeader} [DELETE] Deleting this parcel");

                    // Mark Blocks for deletion
                    for (int j = 0; j < subBlockBuffer.Length; j++) {
                        Entity subBlock = subBlockBuffer[j].m_SubBlock;
                        this.m_CommandBuffer.AddComponent<Deleted>(subBlock, default(Deleted));
                    }

                    return;
                }

                // UPDATE State

                // Retrieve components
                if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef)
                    || !m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var spacePrefab)
                    || !EntityManager.TryGetComponent<ParcelData>(prefabRef, out var parcelData)
                    || !EntityManager.TryGetComponent<PlotData>(prefabRef, out var plotData)
                    || !EntityManager.TryGetComponent<ParcelComposition>(entity, out var parcelComposition)
                    || !EntityManager.TryGetComponent<Geometry>(entity, out var geometry))
                    return;


                // Retrieve old blocks
                foreach (var subBlock in subBlockBuffer) {
                    var subBlockEntity = subBlock.m_SubBlock;
                    var oldBlock = EntityManager.GetComponentData<Block>(subBlockEntity);
                    oldBlockBuffer.TryAdd(oldBlock, subBlockEntity);
                }

                m_Log.Debug($"{m_LogHeader} [UPDATE] Found all required components");

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
                block.m_Position = geometry.m_CenterPosition;
                block.m_Direction = plotData.m_ForwardDirection;
                block.m_Size = plotData.m_PlotSize;
                buildOder.m_Order = 0;

                // Set Data
                m_Log.Debug($"{m_LogHeader} Creating Block of size {block.m_Size.x}*{block.m_Size.y} in position: {block.m_Position.ToString()}");

                if (oldBlockBuffer.TryGetValue(block, out var foundOldBlock)) {
                    m_Log.Debug($"{m_LogHeader} Updating the old block...");
                    oldBlockBuffer.Remove(block);
                    m_CommandBuffer.SetComponent<PrefabRef>(foundOldBlock, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<CurvePosition>(foundOldBlock, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(foundOldBlock, buildOder);
                    m_CommandBuffer.AddComponent<Updated>(foundOldBlock, default(Updated));

                } else {
                    m_Log.Debug($"{m_LogHeader} Creating a new block...");

                    var blockEntity = m_CommandBuffer.CreateEntity(zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent<PrefabRef>(blockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(blockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(blockEntity, buildOder);
                    DynamicBuffer<Cell> cellBuffer = m_CommandBuffer.SetBuffer<Cell>(blockEntity);
                    var cellCount = block.m_Size.x * block.m_Size.y;
                    for (int l = 0; l < cellCount; l++) {
                        cellBuffer.Add(default(Cell));
                    }
                }

                m_Log.Debug($"{m_LogHeader} Done.");
            }
        }
    }
}
