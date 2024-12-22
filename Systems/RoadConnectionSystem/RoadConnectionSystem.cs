// <copyright file="RoadConnectionSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Game;
    using Game.Audio;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine.Scripting;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// todo.
        /// </summary>
        private static readonly float MaxDistance = 8.4f;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ModificationQuery;
        private EntityQuery m_UpdatedNetQuery;
        private EntityQuery m_TrafficConfigQuery;

        // Systems & References
        private Game.Objects.SearchSystem m_ObjectSearchSystem;
        private ModificationBarrier4B m_ModificationBarrier;
        private Game.Net.SearchSystem m_NetSearchSystem;
        private IconCommandSystem m_IconCommandSystem;
        private AudioManager m_AudioManager;

        // Typehandles
        private EntityTypeHandle m_EntityTypeHandle;
        private BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingBufferTypeHandle;
        private ComponentTypeHandle<Deleted> m_DeletedTypeHandle;
        private ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryTypeHandle;
        private ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryTypeHandle;
        private ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryTypeHandle;
        private ComponentTypeHandle<Game.Objects.SpawnLocation> m_SpawnLocationTypeHandle;
        private BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingTypeHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoadConnectionSystem"/> class.
        /// </summary>
        [Preserve]
        public RoadConnectionSystem() {
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(RoadConnectionSystem));
            m_Log.Debug($"OnCreate()");

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
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>()
                    }
                },
                new () {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Game.Net.Edge>(),
                        ComponentType.ReadOnly<ConnectedBuilding>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    },
                    None = new ComponentType[] { ComponentType.ReadOnly<Temp>() }
                }
            });
            m_UpdatedNetQuery = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<Game.Net.Edge>(),
                ComponentType.ReadOnly<ConnectedBuilding>(),
                ComponentType.ReadOnly<Updated>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });
            this.m_TrafficConfigQuery = base.GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<TrafficConfigurationData>()
            });

            // Get TypeHandles
            m_EntityTypeHandle = GetEntityTypeHandle();
            m_DeletedTypeHandle = GetComponentTypeHandle<Deleted>(false);
            m_ConnectedBuildingBufferTypeHandle = GetBufferTypeHandle<ConnectedBuilding>(false);
            m_DeletedTypeHandle = GetComponentTypeHandle<Deleted>(false);
            m_EdgeGeometryTypeHandle = GetComponentTypeHandle<EdgeGeometry>(false);
            m_StartNodeGeometryTypeHandle = GetComponentTypeHandle<StartNodeGeometry>(false);
            m_EndNodeGeometryTypeHandle = GetComponentTypeHandle<EndNodeGeometry>(false);
            m_SpawnLocationTypeHandle = GetComponentTypeHandle<Game.Objects.SpawnLocation>(false);
            m_ConnectedBuildingTypeHandle = GetBufferTypeHandle<ConnectedBuilding>(false);

            base.RequireForUpdate(this.m_ModificationQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Job to populate m_EntitiesToUpdateQueue with parcels to update
            NativeQueue<Entity> entitiesToUpdateQueue = new(Allocator.TempJob);
            CreateEntitiesQueueJob createEntitiesQueueJobData = default;
            m_EntityTypeHandle.Update(this);
            createEntitiesQueueJobData.m_EntityTypeHandle = m_EntityTypeHandle;
            m_ConnectedBuildingBufferTypeHandle.Update(this);
            createEntitiesQueueJobData.m_ConnectedBuildingBufferTypeHandle = m_ConnectedBuildingBufferTypeHandle;
            createEntitiesQueueJobData.m_EntitiesToUpdateQueue = entitiesToUpdateQueue.AsParallelWriter();

            // Job to create a deduped list of data to handle later
            NativeList<ConnectionUpdateDataJob> entitiesToUpdateList = new(Allocator.TempJob);
            CreateUniqueEntitiesListJob createUniqueUpdatesListFromQueueJobData = default;
            createUniqueUpdatesListFromQueueJobData.m_EntitiesToUpdateQueue = entitiesToUpdateQueue;
            createUniqueUpdatesListFromQueueJobData.m_ConnectionUpdateDataList = entitiesToUpdateList;

            // Job to find the "best eligible road" for a given entity in the list
            FindRoadConnectionJob findRoadConnectionJobData = default;
            findRoadConnectionJobData.m_DeletedDataComponentLookup = GetComponentLookup<Deleted>();
            findRoadConnectionJobData.m_PrefabRefComponentLookup = GetComponentLookup<PrefabRef>();
            findRoadConnectionJobData.m_ParcelDataComponentLookup = GetComponentLookup<ParcelData>();
            findRoadConnectionJobData.m_TransformComponentLookup = GetComponentLookup<Game.Objects.Transform>();
            findRoadConnectionJobData.m_ConnectedBuildingsBufferLookup = GetBufferLookup<ConnectedBuilding>();
            findRoadConnectionJobData.m_CurveDataComponentLookup = GetComponentLookup<Curve>();
            findRoadConnectionJobData.m_CompositionDataComponentLookup = GetComponentLookup<Composition>();
            findRoadConnectionJobData.m_EdgeGeometryDataComponentLookup = GetComponentLookup<EdgeGeometry>();
            findRoadConnectionJobData.m_StartNodeGeometryDataComponentLookup = GetComponentLookup<StartNodeGeometry>();
            findRoadConnectionJobData.m_EndNodeGeometryDataComponentLookup = GetComponentLookup<EndNodeGeometry>();
            findRoadConnectionJobData.m_PrefabNetCompositionDataComponentLookup = GetComponentLookup<NetCompositionData>();
            NativeList<ArchetypeChunk> updatedNetChunks = this.m_UpdatedNetQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out JobHandle netQueryChunkListJob);
            findRoadConnectionJobData.m_UpdatedNetChunks = updatedNetChunks;
            m_EntityTypeHandle.Update(this);
            createEntitiesQueueJobData.m_EntityTypeHandle = m_EntityTypeHandle;
            findRoadConnectionJobData.m_NetSearchTree = this.m_NetSearchSystem.GetNetSearchTree(true, out JobHandle netSearchSystemJob);
            findRoadConnectionJobData.m_ConnectionUpdateDataList = entitiesToUpdateList.AsDeferredJobArray();

            // Job to set data
            UpdateDataJob updateRoadAndParcelDataJobData = default;
            updateRoadAndParcelDataJobData.m_ParcelComponentLookup = GetComponentLookup<Parcel>(false);
            updateRoadAndParcelDataJobData.m_CreatedComponentLookup = GetComponentLookup<Created>(true);
            updateRoadAndParcelDataJobData.m_TempComponentLookup = GetComponentLookup<Temp>(true);
            updateRoadAndParcelDataJobData.m_ConnectionUpdateDataList = entitiesToUpdateList;
            updateRoadAndParcelDataJobData.m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            updateRoadAndParcelDataJobData.m_IconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer();
            updateRoadAndParcelDataJobData.m_TrafficConfigurationData = m_TrafficConfigQuery.GetSingleton<TrafficConfigurationData>();

            // Job Scheduling

            // 1. Point a queue
            JobHandle createEntitiesQueueJobHandle = createEntitiesQueueJobData.ScheduleParallel(
                m_ModificationQuery,
                base.Dependency
            );

            // 2. Point the unique list from the queue
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
            _ = entitiesToUpdateQueue.Dispose(createUniqueUpdatesListFromQueueJobHandle);
            _ = updatedNetChunks.Dispose(findRoadConnectionJobHandle);

            // Register Tree Readers
            // this.m_ObjectSearchSystem.AddStaticSearchTreeReader(createEntitiesQueueJobHandle);
            m_NetSearchSystem.AddNetSearchTreeReader(findRoadConnectionJobHandle);

            // Set base dependencies
            base.Dependency = replaceRoadConnectionJobHandle;

            // Register depdencies with Barrier
            m_ModificationBarrier.AddJobHandleForProducer(base.Dependency);

            // Dispose of our List
            _ = entitiesToUpdateList.Dispose(base.Dependency);
        }

        private static void CheckDistance(EdgeGeometry edgeGeometry, EdgeNodeGeometry startGeometry, EdgeNodeGeometry endGeometry, float3 position, bool canBeOnRoad, ref float maxDistance) {
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

        private static void CheckDistance(Bezier4x3 curve1, Bezier4x3 curve2, float3 position, ref float maxDistance) {
            if (MathUtils.DistanceSquared(MathUtils.Bounds(curve1.xz) | MathUtils.Bounds(curve2.xz), position.xz) < maxDistance * maxDistance) {
                _ = MathUtils.Distance(MathUtils.Lerp(curve1.xz, curve2.xz, 0.5f), position.xz, out float t);

                float x = MathUtils.Distance(new Line2.Segment(MathUtils.Position(curve1.xz, t), MathUtils.Position(curve2.xz, t)), position.xz, out _);
                maxDistance = math.min(x, maxDistance);
            }
        }

        private static void CheckDistance(Bezier4x3 curve, float3 position, ref float maxDistance) {
            if (MathUtils.DistanceSquared(MathUtils.Bounds(curve.xz), position.xz) < maxDistance * maxDistance) {
                float x = MathUtils.Distance(curve.xz, position.xz, out _);
                maxDistance = math.min(x, maxDistance);
            }
        }
    }
}
