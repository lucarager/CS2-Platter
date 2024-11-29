namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Game.UI;
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine.Scripting;
    using Block = Game.Zones.Block;

    public partial class TestSystem : UISystemBase {
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
        private EntityQuery m_LotQuery;
        private EntityQuery m_UpdatedEdgesQuery;
        private ModificationBarrier4 m_ModificationBarrier;
        private AreaBufferSystem m_AreaBufferSystem;

        /// <inheritdoc/>
        [Preserve]
        protected override void OnCreate() {
            base.OnCreate();
            this.m_Log = Mod.Instance.Log;
            this.m_Random = RandomSeed.Next();
            this.m_Barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            this.m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier4>();
            this.m_AreaBufferSystem = base.World.GetOrCreateSystemManaged<AreaBufferSystem>();

            // Queries
            this.m_BuildingQuery = base.GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<SpawnableBuildingData>(),
                ComponentType.ReadOnly<BuildingSpawnGroupData>(),
                ComponentType.ReadOnly<PrefabData>(),
            });
            this.m_DefinitionArchetype = base.EntityManager.CreateArchetype(new ComponentType[] {
                ComponentType.ReadWrite<CreationDefinition>(),
                ComponentType.ReadWrite<ObjectDefinition>(),
                ComponentType.ReadWrite<Updated>(),
                ComponentType.ReadWrite<Deleted>(),
            });
            this.m_LotQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Zones.Block>(),
                        ComponentType.ReadOnly<Owner>(),
                        ComponentType.ReadOnly<CurvePosition>(),
                        ComponentType.ReadOnly<VacantLot>(),
                    },
                    Any = new ComponentType[0],
                    None = new ComponentType[]
                    {
                        ComponentType.ReadWrite<Temp>(),
                        ComponentType.ReadWrite<Deleted>(),
                    },
                },
            });
            this.m_UpdatedEdgesQuery = base.GetEntityQuery(new EntityQueryDesc[]
            {
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Edge>(),
                        ComponentType.ReadOnly<SubBlock>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    },
                    None = new ComponentType[] { ComponentType.ReadOnly<Temp>() }
                }
            });

            m_Log.Info("Loaded TestSystem!");

            AddBinding(new TriggerBinding<string>(Mod.Id, "dostuff", (args) => this.Dostuff(args)));

            // Update rules
            base.RequireForUpdate(this.m_BuildingQuery);
            base.RequireForUpdate(this.m_UpdatedEdgesQuery);
        }

        /// <inheritdoc/>
        [Preserve]
        protected override void OnUpdate() {
            this.m_CommandBuffer = m_Barrier.CreateCommandBuffer();
            this.m_BlockCommandBuffer = m_Barrier.CreateCommandBuffer();

            if (!executed) {
                NativeArray<Entity> entities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
                NativeArray<SpawnableBuildingData> spawnableBuildingDataArray = m_BuildingQuery.ToComponentDataArray<SpawnableBuildingData>(Allocator.Temp);
                NativeArray<BuildingData> buildings = m_BuildingQuery.ToComponentDataArray<BuildingData>(Allocator.Temp);
                this.m_CachedBuildingEntity = entities[0];
                m_Log.Info($"Selected a building! {entities[0].ToString()}");

                // Cache an edge...
                NativeArray<Entity> edges = m_UpdatedEdgesQuery.ToEntityArray(Allocator.Temp);
                this.m_CachedEdgeEntity = edges[0];
                m_Log.Info($"Selected an edge! {edges[0].ToString()}");

                this.executed = true;
            }
        }

        /// <inheritdoc/>
        [Preserve]
        protected override void OnDestroy() {
            base.OnDestroy();
        }

        private void Dostuff(string method) {
            m_Log.Info("Doing Stuff!");
            m_Log.Info(method);

            if (method == "parcel") {
                this.SpawnParcel();
            }
            if (method == "building") {
                this.SpawnBuilding();
            }
            if (method == "block") {
                this.SpawnBlock();
            }
        }
        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            PrefabManager.Install();
        }
        private void SpawnParcel() {
            // Duplicate a "zone" type prefab


            //Game.Objects.Transform transform = default(Game.Objects.Transform);
            //transform.m_Position.x = 725.2779f;
            //transform.m_Position.y = 879.4377f;
            //transform.m_Position.z = 250.3719f;
            //transform.m_Rotation = new quaternion(0f, 0.3931616f, 0f, 0.91946936f);

            //Parcel parcel = default(Parcel);
            //parcel.m_Position = transform.m_Position;
            //parcel.m_Direction = 0;
            //parcel.m_Size.x = 2;
            //parcel.m_Size.y = 2;

            //Block block = default(Block);
            //block.m_Position = transform.m_Position;
            //block.m_Direction = 0;
            //block.m_Size.x = 2;
            //block.m_Size.y = 2;

            //var e = EntityManager.CreateEntity(new ComponentType[] {
            //    ComponentType.ReadOnly<Parcel>(),
            //    ComponentType.ReadOnly<Block>(),
            //});

            //EntityManager.SetComponentData<Parcel>(e, parcel);
            //EntityManager.SetComponentData<Block>(e, block);
        }

        private void SpawnBlock() {
            // Try to get all required data
            if (!EntityManager.TryGetComponent<Composition>(this.m_CachedEdgeEntity, out Composition composition)) {
                m_Log.Info("No Composition!");
                return;
            }

            if (!EntityManager.TryGetComponent<RoadComposition>(composition.m_Edge, out RoadComposition componentData)) {
                m_Log.Info("No RoadComposition!");
                return;
            }

            Entity blockPrefabEntity = componentData.m_ZoneBlockPrefab;

            if (!EntityManager.TryGetComponent<ZoneBlockData>(blockPrefabEntity, out ZoneBlockData zoneBlockData)) {
                m_Log.Info("No ZoneBlockData!");
                return;
            }

            // Some sample data
            Block block = default(Block);
            block.m_Position = 0;
            block.m_Position.xz = 0;
            block.m_Direction = 0;
            block.m_Size.x = 0;
            block.m_Size.y = 0;
            CurvePosition curvePos = default(CurvePosition);
            curvePos.m_CurvePosition = 0;
            Game.Zones.BuildOrder buildOrder = default(Game.Zones.BuildOrder);
            buildOrder.m_Order = 0;

            // Add the block and cells
            Entity e = this.m_BlockCommandBuffer.CreateEntity(zoneBlockData.m_Archetype);

            this.m_BlockCommandBuffer.SetComponent<PrefabRef>(e, new PrefabRef(blockPrefabEntity));

            this.m_BlockCommandBuffer.SetComponent<Game.Zones.Block>(e, block);

            this.m_BlockCommandBuffer.SetComponent<CurvePosition>(e, curvePos);

            this.m_BlockCommandBuffer.SetComponent<Game.Zones.BuildOrder>(e, buildOrder);

            DynamicBuffer<Cell> dynamicBuffer2 = this.m_BlockCommandBuffer.SetBuffer<Cell>(e);

            int cellCount = block.m_Size.x * block.m_Size.y;

            for (int l = 0; l < cellCount; l++) {
                dynamicBuffer2.Add(default(Cell));
            }

            this.m_BlockCommandBuffer.AddComponent<Owner>(e, new Owner {
                m_Owner = this.m_CachedEdgeEntity
            });
        }

        private void SpawnBuilding() {
            Unity.Mathematics.Random random = m_Random.GetRandom(0);

            if (!EntityManager.TryGetComponent<BuildingData>(this.m_CachedBuildingEntity, out BuildingData buildingData)) {
                m_Log.Info("No BuildingData!");
                return;
            }

            // Example data, taken from game, until we get position logic in
            Game.Objects.Transform transform = default(Game.Objects.Transform);
            transform.m_Position.x = 725.2779f;
            transform.m_Position.y = 879.4377f;
            transform.m_Position.z = 250.3719f;
            transform.m_Rotation = new quaternion(0f, 0.3931616f, 0f, 0.91946936f);

            // Creation Definition
            CreationDefinition creationDef_Building = default(CreationDefinition);
            creationDef_Building.m_Prefab = this.m_CachedBuildingEntity;
            creationDef_Building.m_Flags |= CreationFlags.Permanent | CreationFlags.Construction;
            creationDef_Building.m_RandomSeed = random.NextInt();

            // Object Definition
            ObjectDefinition objectDef_Building = default(ObjectDefinition);
            objectDef_Building.m_ParentMesh = -1;
            objectDef_Building.m_Position = transform.m_Position;
            objectDef_Building.m_Rotation = transform.m_Rotation;
            objectDef_Building.m_LocalPosition = transform.m_Position;
            objectDef_Building.m_LocalRotation = transform.m_Rotation;

            // Send to Buffer
            Entity defArchetypeEntity = this.m_CommandBuffer.CreateEntity(this.m_DefinitionArchetype);
            this.m_CommandBuffer.SetComponent<CreationDefinition>(defArchetypeEntity, creationDef_Building);
            this.m_CommandBuffer.SetComponent<ObjectDefinition>(defArchetypeEntity, objectDef_Building);

            // Try making a lot
            //DynamicBuffer<VacantLot> dynamicBuffer_VLots = default(DynamicBuffer<VacantLot>);
            //if (!dynamicBuffer_VLots.IsCreated) {
            //    dynamicBuffer_VLots = this.m_CommandBuffer.AddBuffer<VacantLot>(entity);
            //}
            //this.m_CommandBuffer.Add(new VacantLot(min, max, cell.m_Zone, height3, lotFlags));
        }

    }
}
