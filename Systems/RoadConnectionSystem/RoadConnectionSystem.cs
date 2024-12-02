namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Game;
    using Game.Audio;
    using Game.Buildings;
    using Game.Common;
    using Game.Effects;
    using Game.Modding.Toolchain.Dependencies;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Prefabs;
    using Platter.Utils;
    using System;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine.Scripting;

    public partial class RoadConnectionSystem : GameSystemBase {
        // Static vars
        public static float maxDistance = 8.4f;

        // Logging
        private PrefixedLogger m_Log;

        // Systems
        private Game.Objects.SearchSystem m_ObjectSearchSystem;
        private ModificationBarrier4B m_ModificationBarrier;
        private Game.Net.SearchSystem m_NetSearchSystem;
        private IconCommandSystem m_IconCommandSystem;
        private AudioManager m_AudioManager;

        // Queries
        private EntityQuery m_ModificationQuery;
        private EntityQuery m_UpdatedNetQuery;

        // Typehandles        
        public EntityTypeHandle m_EntityTypeHandle;
        public ComponentTypeHandle<Deleted> m_DeletedTypeHandle;
        public ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryTypeHandle;
        public ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryTypeHandle;
        public ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryTypeHandle;
        public ComponentTypeHandle<Game.Objects.SpawnLocation> m_SpawnLocationTypeHandle;
        public BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingTypeHandle;

        [Preserve]
        public RoadConnectionSystem() {
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(RoadConnectionSystem));

            // Reference Systems
            m_ObjectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier4B>();
            m_NetSearchSystem = base.World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ObjectSearchSystem = base.World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_IconCommandSystem = base.World.GetOrCreateSystemManaged<IconCommandSystem>();
            m_AudioManager = base.World.GetOrCreateSystemManaged<AudioManager>();

            // Define Queries
            m_ModificationQuery = GetEntityQuery(new EntityQueryDesc[] {
                new () {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>(),
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    }
                }
            });
            m_UpdatedNetQuery = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<Game.Net.Edge>(),
                ComponentType.ReadOnly<ConnectedBuilding>(),
                ComponentType.ReadOnly<Updated>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });

            // Get TypeHandles
            m_DeletedTypeHandle = GetComponentTypeHandle<Deleted>(false);
            m_EntityTypeHandle = GetEntityTypeHandle();
            m_DeletedTypeHandle = GetComponentTypeHandle<Deleted>(false);
            m_EdgeGeometryTypeHandle = GetComponentTypeHandle<EdgeGeometry>(false);
            m_StartNodeGeometryTypeHandle = GetComponentTypeHandle<StartNodeGeometry>(false);
            m_EndNodeGeometryTypeHandle = GetComponentTypeHandle<EndNodeGeometry>(false);
            m_SpawnLocationTypeHandle = GetComponentTypeHandle<Game.Objects.SpawnLocation>(false);
            m_ConnectedBuildingTypeHandle = GetBufferTypeHandle<ConnectedBuilding>(false);

            m_Log.Debug($"Loaded System.");
            base.RequireForUpdate(this.m_ModificationQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Job to populate m_EntitiesToUpdateQueue with parcels to update
            var entitiesToUpdateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var createEntitiesQueueJobData = default(RoadConnectionJobs.CreateEntitiesQueue);
            m_EntityTypeHandle.Update(this);
            createEntitiesQueueJobData.m_EntityTypeHandle = m_EntityTypeHandle;
            createEntitiesQueueJobData.m_EntitiesToUpdateQueue = entitiesToUpdateQueue.AsParallelWriter();

            // Job to create a deduped list of data to handle later
            var entitiesToUpdateList = new NativeList<ConnectionUpdateData>(Allocator.TempJob);
            var createUniqueUpdatesListFromQueueJobData = default(RoadConnectionJobs.CreateUniqueEntitiesList);
            createUniqueUpdatesListFromQueueJobData.m_EntitiesToUpdateQueue = entitiesToUpdateQueue;
            createUniqueUpdatesListFromQueueJobData.m_ConnectionUpdateDataList = entitiesToUpdateList;

            // Job to find the "best eligible road" for a given entity in the list
            var findRoadConnectionJobData = default(RoadConnectionJobs.FindRoadConnection);
            findRoadConnectionJobData.m_DeletedDataComponentLookup = GetComponentLookup<Deleted>();
            findRoadConnectionJobData.m_PrefabRefComponentLookup = GetComponentLookup<PrefabRef>();
            findRoadConnectionJobData.m_ParcelDataComponentLookup = GetComponentLookup<ParcelData>();
            findRoadConnectionJobData.m_TransformComponentLookup = GetComponentLookup<Transform>();
            findRoadConnectionJobData.m_ConnectedBuildingsBufferLookup = GetBufferLookup<ConnectedBuilding>();
            findRoadConnectionJobData.m_CurveDataComponentLookup = GetComponentLookup<Curve>();
            findRoadConnectionJobData.m_CompositionDataComponentLookup = GetComponentLookup<Composition>();
            findRoadConnectionJobData.m_EdgeGeometryDataComponentLookup = GetComponentLookup<EdgeGeometry>();
            findRoadConnectionJobData.m_StartNodeGeometryDataComponentLookup = GetComponentLookup<StartNodeGeometry>();
            findRoadConnectionJobData.m_EndNodeGeometryDataComponentLookup = GetComponentLookup<EndNodeGeometry>();
            findRoadConnectionJobData.m_PrefabNetCompositionDataComponentLookup = GetComponentLookup<NetCompositionData>();
            var updatedNetChunks = this.m_UpdatedNetQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var netQueryChunkListJob);
            findRoadConnectionJobData.m_UpdatedNetChunks = updatedNetChunks;
            m_EntityTypeHandle.Update(this);
            createEntitiesQueueJobData.m_EntityTypeHandle = m_EntityTypeHandle;
            findRoadConnectionJobData.m_NetSearchTree = this.m_NetSearchSystem.GetNetSearchTree(true, out var netSearchSystemJob);
            findRoadConnectionJobData.m_ConnectionUpdateDataList = entitiesToUpdateList.AsDeferredJobArray();

            // Job to set data
            var updateRoadAndParcelDataJobData = default(RoadConnectionJobs.UpdateParcelData);
            updateRoadAndParcelDataJobData.m_ParcelComponentLookup = GetComponentLookup<Parcel>();
            updateRoadAndParcelDataJobData.m_CreatedComponentLookup = GetComponentLookup<Created>();
            updateRoadAndParcelDataJobData.m_TempComponentLookup = GetComponentLookup<Temp>();
            updateRoadAndParcelDataJobData.m_ConnectionUpdateDataList = entitiesToUpdateList;

            // Job Scheduling

            // 1. Create a queue
            JobHandle createEntitiesQueueJobHandle = createEntitiesQueueJobData.ScheduleParallel(
                m_ModificationQuery,
                base.Dependency
            );

            // 2. Create the unique list from the queue
            JobHandle createUniqueUpdatesListFromQueueJobHandle = createUniqueUpdatesListFromQueueJobData.Schedule(
                createEntitiesQueueJobHandle
            );

            // 3. Find road connections for each parcel in the list
            JobHandle findRoadConnectionJobHandle = findRoadConnectionJobData.Schedule(
                entitiesToUpdateList,
                1,
                JobHandle.CombineDependencies(
                    createUniqueUpdatesListFromQueueJobHandle,
                    netQueryChunkListJob,
                    netSearchSystemJob
                )
            );

            // 4. Update road data for each parcel
            JobHandle replaceRoadConnectionJobHandle = updateRoadAndParcelDataJobData.Schedule(findRoadConnectionJobHandle);

            // 5? @todo secondary lanes

            // Dispose of queue and chunks
            entitiesToUpdateQueue.Dispose(createUniqueUpdatesListFromQueueJobHandle);
            updatedNetChunks.Dispose(findRoadConnectionJobHandle);

            // Register Tree Readers
            //this.m_ObjectSearchSystem.AddStaticSearchTreeReader(createEntitiesQueueJobHandle);
            m_NetSearchSystem.AddNetSearchTreeReader(findRoadConnectionJobHandle);

            // Set base dependencies
            base.Dependency = replaceRoadConnectionJobHandle;

            // Register depdencies with Barrier
            m_ModificationBarrier.AddJobHandleForProducer(base.Dependency);

            // Dispose of our List
            entitiesToUpdateList.Dispose(base.Dependency);
        }

        /// <summary>
        /// Struct containing data for a replacement job (building, road, etc.)
        /// </summary>
        public struct ConnectionUpdateData : IComparable<ConnectionUpdateData> {
            public Entity m_Parcel;
            public Entity m_NewRoad;
            public float3 m_FrontPos;
            public float m_CurvePos;
            public bool m_Deleted;

            public ConnectionUpdateData(Entity parcel) {
                this.m_Parcel = parcel;
                this.m_NewRoad = Entity.Null;
                this.m_FrontPos = default(float3);
                this.m_CurvePos = 0f;
                this.m_Deleted = false;
            }

            public int CompareTo(RoadConnectionSystem.ConnectionUpdateData other) {
                return this.m_Parcel.Index - other.m_Parcel.Index;
            }
        }

        public static void CheckDistance(EdgeGeometry edgeGeometry, EdgeNodeGeometry startGeometry, EdgeNodeGeometry endGeometry, float3 position, bool canBeOnRoad, ref float maxDistance) {
            if (MathUtils.DistanceSquared(edgeGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance) {
                RoadConnectionSystem.CheckDistance(edgeGeometry.m_Start.m_Left, position, ref maxDistance);
                RoadConnectionSystem.CheckDistance(edgeGeometry.m_Start.m_Right, position, ref maxDistance);
                RoadConnectionSystem.CheckDistance(edgeGeometry.m_End.m_Left, position, ref maxDistance);
                RoadConnectionSystem.CheckDistance(edgeGeometry.m_End.m_Right, position, ref maxDistance);
                if (canBeOnRoad) {
                    RoadConnectionSystem.CheckDistance(edgeGeometry.m_Start.m_Left, edgeGeometry.m_Start.m_Right, position, ref maxDistance);
                    RoadConnectionSystem.CheckDistance(edgeGeometry.m_End.m_Left, edgeGeometry.m_End.m_Right, position, ref maxDistance);
                }
            }
            if (MathUtils.DistanceSquared(startGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance) {
                RoadConnectionSystem.CheckDistance(startGeometry.m_Left.m_Left, position, ref maxDistance);
                RoadConnectionSystem.CheckDistance(startGeometry.m_Right.m_Right, position, ref maxDistance);
                if (startGeometry.m_MiddleRadius > 0f) {
                    RoadConnectionSystem.CheckDistance(startGeometry.m_Left.m_Right, position, ref maxDistance);
                    RoadConnectionSystem.CheckDistance(startGeometry.m_Right.m_Left, position, ref maxDistance);
                }
                if (canBeOnRoad) {
                    if (startGeometry.m_MiddleRadius > 0f) {
                        RoadConnectionSystem.CheckDistance(startGeometry.m_Left.m_Left, startGeometry.m_Left.m_Right, position, ref maxDistance);
                        RoadConnectionSystem.CheckDistance(startGeometry.m_Right.m_Left, startGeometry.m_Middle, position, ref maxDistance);
                        RoadConnectionSystem.CheckDistance(startGeometry.m_Middle, startGeometry.m_Right.m_Right, position, ref maxDistance);
                    } else {
                        RoadConnectionSystem.CheckDistance(startGeometry.m_Left.m_Left, startGeometry.m_Middle, position, ref maxDistance);
                        RoadConnectionSystem.CheckDistance(startGeometry.m_Middle, startGeometry.m_Right.m_Right, position, ref maxDistance);
                    }
                }
            }
            if (MathUtils.DistanceSquared(endGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance) {
                RoadConnectionSystem.CheckDistance(endGeometry.m_Left.m_Left, position, ref maxDistance);
                RoadConnectionSystem.CheckDistance(endGeometry.m_Right.m_Right, position, ref maxDistance);
                if (endGeometry.m_MiddleRadius > 0f) {
                    RoadConnectionSystem.CheckDistance(endGeometry.m_Left.m_Right, position, ref maxDistance);
                    RoadConnectionSystem.CheckDistance(endGeometry.m_Right.m_Left, position, ref maxDistance);
                }
                if (canBeOnRoad) {
                    if (endGeometry.m_MiddleRadius > 0f) {
                        RoadConnectionSystem.CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Left.m_Right, position, ref maxDistance);
                        RoadConnectionSystem.CheckDistance(endGeometry.m_Right.m_Left, endGeometry.m_Middle, position, ref maxDistance);
                        RoadConnectionSystem.CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
                        return;
                    }
                    RoadConnectionSystem.CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Middle, position, ref maxDistance);
                    RoadConnectionSystem.CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
                }
            }
        }

        public static void CheckDistance(Bezier4x3 curve1, Bezier4x3 curve2, float3 position, ref float maxDistance) {
            if (MathUtils.DistanceSquared(MathUtils.Bounds(curve1.xz) | MathUtils.Bounds(curve2.xz), position.xz) < maxDistance * maxDistance) {
                float t;
                MathUtils.Distance(MathUtils.Lerp(curve1.xz, curve2.xz, 0.5f), position.xz, out t);
                float t2;
                float x = MathUtils.Distance(new Line2.Segment(MathUtils.Position(curve1.xz, t), MathUtils.Position(curve2.xz, t)), position.xz, out t2);
                maxDistance = math.min(x, maxDistance);
            }
        }

        public static void CheckDistance(Bezier4x3 curve, float3 position, ref float maxDistance) {
            if (MathUtils.DistanceSquared(MathUtils.Bounds(curve.xz), position.xz) < maxDistance * maxDistance) {
                float t;
                float x = MathUtils.Distance(curve.xz, position.xz, out t);
                maxDistance = math.min(x, maxDistance);
            }
        }
    }
}
