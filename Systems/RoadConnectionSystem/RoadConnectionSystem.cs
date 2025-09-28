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
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine.PlayerLoop;
    using UnityEngine.Scripting;
    using static Game.Prefabs.CompositionFlags;
    using static Unity.Collections.Unicode;

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
        private ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryTypeHandle;
        private ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryTypeHandle;
        private ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryTypeHandle;
        private ComponentTypeHandle<Deleted> m_DeletedTypeHandle;
        
        private BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingBufferTypeHandle;

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
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4B>();
            m_NetSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            m_AudioManager = World.GetOrCreateSystemManaged<AudioManager>();

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
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>()
                    }
                }
            });

            m_UpdatedNetQuery = GetEntityQuery(new EntityQueryDesc[] {
                new () {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Game.Net.Edge>(),
                        ComponentType.ReadOnly<ConnectedBuilding>()
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Updated>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>()
                    }
                }
            });

            m_TrafficConfigQuery = base.GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<TrafficConfigurationData>()
            });

            // Get TypeHandles
            m_EntityTypeHandle = GetEntityTypeHandle();
            m_EdgeGeometryTypeHandle = GetComponentTypeHandle<EdgeGeometry>();
            m_StartNodeGeometryTypeHandle = GetComponentTypeHandle<StartNodeGeometry>();
            m_EndNodeGeometryTypeHandle = GetComponentTypeHandle<EndNodeGeometry>();
            m_DeletedTypeHandle = GetComponentTypeHandle<Deleted>();
            m_ConnectedBuildingBufferTypeHandle = GetBufferTypeHandle<ConnectedBuilding>(false);

            RequireForUpdate(m_ModificationQuery);
        }

        /// <summary>
        /// General flow:
        /// 1. Runs when m_ModificationQuery runs(updated edge or parcel entities)
        /// 2. [CreateEntitiesQueueJob] Populate a queue with parcels to update
        /// 3. [CreateUniqueEntitiesListJob] Dedupe the list
        /// 4. [FindRoadConnectionJob] Find road connections for each parcel
        /// 5. [UpdateDataJob] Update parcel and road data.
        /// </summary>
        protected override void OnUpdate() {
            // Job to populate m_ParcelEntitiesQueue with parcels to update
            NativeQueue<Entity> parcelEntitiesQueue = new(Allocator.TempJob);
            NativeQueue<Entity> edgeEntitiesQueue = new(Allocator.TempJob);
            CreateEntitiesQueueJob createEntitiesQueueJobData = default;
            m_EntityTypeHandle.Update(this);
            createEntitiesQueueJobData.m_EntityTypeHandle = m_EntityTypeHandle;
            m_ConnectedBuildingBufferTypeHandle.Update(this);
            createEntitiesQueueJobData.m_ParcelEntitiesQueue = parcelEntitiesQueue.AsParallelWriter();
            createEntitiesQueueJobData.m_EdgeEntitiesQueue = edgeEntitiesQueue.AsParallelWriter();
            createEntitiesQueueJobData.m_ConnectedBuildingBufferTypeHandle = m_ConnectedBuildingBufferTypeHandle;
            createEntitiesQueueJobData.m_PrefabRefComponentLookup = GetComponentLookup<PrefabRef>();
            createEntitiesQueueJobData.m_EdgeGeometryComponentLookup = GetComponentLookup<EdgeGeometry>();
            createEntitiesQueueJobData.m_StartNodeGeometryComponentLookup = GetComponentLookup<StartNodeGeometry>();
            createEntitiesQueueJobData.m_EndNodeGeometryComponentLookup = GetComponentLookup<EndNodeGeometry>();
            createEntitiesQueueJobData.m_ParcelDataComponentLookup = GetComponentLookup<ParcelData>();
            createEntitiesQueueJobData.m_ParcelComponentLookup = GetComponentLookup<Parcel>();
            createEntitiesQueueJobData.m_TransformComponentLookup = GetComponentLookup<Transform>();
            createEntitiesQueueJobData.m_ObjectSearchTree = m_ObjectSearchSystem.GetStaticSearchTree(true, out var objectSearchTreeJobHandle);
            createEntitiesQueueJobData.m_EdgeGeometryTypeHandle = m_EdgeGeometryTypeHandle;
            createEntitiesQueueJobData.m_StartNodeGeometryTypeHandle = m_StartNodeGeometryTypeHandle;
            createEntitiesQueueJobData.m_EndNodeGeometryTypeHandle = m_EndNodeGeometryTypeHandle;
            createEntitiesQueueJobData.m_DeletedTypeHandle = m_DeletedTypeHandle;

            var createEntitiesQueueJobHandle = createEntitiesQueueJobData.ScheduleParallel(
                m_ModificationQuery,
                JobHandle.CombineDependencies(base.Dependency, objectSearchTreeJobHandle)
            );

            // Job to create a deduped list of data to handle later
            NativeList<ConnectionUpdateDataJob> parcelEntitiesList = new(Allocator.TempJob);
            CreateUniqueEntitiesListJob createUniqueParcelListJob = default;
            createUniqueParcelListJob.m_ParcelEntitiesQueue = parcelEntitiesQueue;
            createUniqueParcelListJob.m_ParcelEntittiesList = parcelEntitiesList;

            var createUniqueParcelListJobHandle = createUniqueParcelListJob.Schedule(
                createEntitiesQueueJobHandle
            );

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
            var updatedNetChunks = m_UpdatedNetQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var netQueryChunkListJob);
            findRoadConnectionJobData.m_UpdatedNetChunks = updatedNetChunks;
            findRoadConnectionJobData.m_NetSearchTree = m_NetSearchSystem.GetNetSearchTree(true, out var netSearchTreeJobHandle);
            findRoadConnectionJobData.m_ParcelEntitiesList = parcelEntitiesList.AsDeferredJobArray();

            var findRoadConnectionJobHandle = findRoadConnectionJobData.Schedule(
                parcelEntitiesList,
                1,
                JobHandle.CombineDependencies(
                    createUniqueParcelListJobHandle,
                    netQueryChunkListJob,
                    netSearchTreeJobHandle
                )
            );

            m_NetSearchSystem.AddNetSearchTreeReader(findRoadConnectionJobHandle);

            // Job to set data
            UpdateDataJob updateRoadAndParcelDataJobData = default;
            updateRoadAndParcelDataJobData.m_EdgeComponentLookup = GetComponentLookup<Edge>(false);
            updateRoadAndParcelDataJobData.m_NodeComponentLookup = GetComponentLookup<Node>(true);
            updateRoadAndParcelDataJobData.m_NodeGeoComponentLookup = GetComponentLookup<NodeGeometry>(true);
            updateRoadAndParcelDataJobData.m_AggregatedComponentLookup = GetComponentLookup<Aggregated>(true);
            updateRoadAndParcelDataJobData.m_ParcelComponentLookup = GetComponentLookup<Parcel>(false);
            updateRoadAndParcelDataJobData.m_CreatedComponentLookup = GetComponentLookup<Created>(true);
            updateRoadAndParcelDataJobData.m_TempComponentLookup = GetComponentLookup<Temp>(true);
            updateRoadAndParcelDataJobData.m_SubBlockBufferLookup = GetBufferLookup<SubBlock>(false);
            updateRoadAndParcelDataJobData.m_ParcelEntitiesList = parcelEntitiesList;
            updateRoadAndParcelDataJobData.m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            updateRoadAndParcelDataJobData.m_IconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer();
            updateRoadAndParcelDataJobData.m_TrafficConfigurationData = m_TrafficConfigQuery.GetSingleton<TrafficConfigurationData>();

            var replaceRoadConnectionJobHandle = updateRoadAndParcelDataJobData.Schedule(findRoadConnectionJobHandle);

            // Dispose of queue and chunks
            parcelEntitiesQueue.Dispose(createUniqueParcelListJobHandle);
            updatedNetChunks.Dispose(findRoadConnectionJobHandle);

            // Set base dependencies
            base.Dependency = replaceRoadConnectionJobHandle;

            // Register depdencies with Barrier
            m_ModificationBarrier.AddJobHandleForProducer(base.Dependency);

            // Dispose of our List
            parcelEntitiesList.Dispose(base.Dependency);
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
                _ = MathUtils.Distance(MathUtils.Lerp(curve1.xz, curve2.xz, 0.5f), position.xz, out var t);

                var x = MathUtils.Distance(new Line2.Segment(MathUtils.Position(curve1.xz, t), MathUtils.Position(curve2.xz, t)), position.xz, out _);
                maxDistance = math.min(x, maxDistance);
            }
        }

        private static void CheckDistance(Bezier4x3 curve, float3 position, ref float maxDistance) {
            if (MathUtils.DistanceSquared(MathUtils.Bounds(curve.xz), position.xz) < maxDistance * maxDistance) {
                var x = MathUtils.Distance(curve.xz, position.xz, out _);
                maxDistance = math.min(x, maxDistance);
            }
        }
    }
}
