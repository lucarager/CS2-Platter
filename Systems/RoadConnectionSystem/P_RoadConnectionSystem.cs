// <copyright file="RoadConnectionSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Game;
    using Game.Audio;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
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
    public partial class P_RoadConnectionSystem : GameSystemBase {
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
        private P_ParcelSearchSystem m_ParcelSearchSystem;
        private ModificationBarrier4B m_ModificationBarrier;
        private Game.Net.SearchSystem m_NetSearchSystem;
        private IconCommandSystem m_IconCommandSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="P_RoadConnectionSystem"/> class.
        /// </summary>
        [Preserve]
        public P_RoadConnectionSystem() {
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(P_RoadConnectionSystem));
            m_Log.Debug($"OnCreate()");

            // Reference Systems
            m_ParcelSearchSystem = World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4B>();
            m_NetSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

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
                        ComponentType.ReadOnly<ConnectedParcel>()
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
                        ComponentType.ReadOnly<ConnectedParcel>()
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
            createEntitiesQueueJobData.m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle();
            createEntitiesQueueJobData.m_ParcelEntitiesQueue = parcelEntitiesQueue.AsParallelWriter();
            createEntitiesQueueJobData.m_EdgeEntitiesQueue = edgeEntitiesQueue.AsParallelWriter();
            createEntitiesQueueJobData.m_ConnectedParcelBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ConnectedParcel>();
            createEntitiesQueueJobData.m_PrefabRefComponentLookup = SystemAPI.GetComponentLookup<PrefabRef>();
            createEntitiesQueueJobData.m_EdgeGeometryComponentLookup = SystemAPI.GetComponentLookup<EdgeGeometry>();
            createEntitiesQueueJobData.m_StartNodeGeometryComponentLookup = SystemAPI.GetComponentLookup<StartNodeGeometry>();
            createEntitiesQueueJobData.m_EndNodeGeometryComponentLookup = SystemAPI.GetComponentLookup<EndNodeGeometry>();
            createEntitiesQueueJobData.m_ParcelDataComponentLookup = SystemAPI.GetComponentLookup<ParcelData>();
            createEntitiesQueueJobData.m_ParcelComponentLookup = SystemAPI.GetComponentLookup<Parcel>();
            createEntitiesQueueJobData.m_TransformComponentLookup = SystemAPI.GetComponentLookup<Transform>();
            createEntitiesQueueJobData.m_ObjectSearchTree = m_ParcelSearchSystem.GetStaticSearchTree(true, out var objectSearchTreeJobHandle);
            createEntitiesQueueJobData.m_EdgeGeometryTypeHandle = SystemAPI.GetComponentTypeHandle<EdgeGeometry>();
            createEntitiesQueueJobData.m_StartNodeGeometryTypeHandle = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>();
            createEntitiesQueueJobData.m_EndNodeGeometryTypeHandle = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>();
            createEntitiesQueueJobData.m_DeletedTypeHandle = SystemAPI.GetComponentTypeHandle<Deleted>();

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
            findRoadConnectionJobData.m_DeletedDataComponentLookup = SystemAPI.GetComponentLookup<Deleted>();
            findRoadConnectionJobData.m_PrefabRefComponentLookup = SystemAPI.GetComponentLookup<PrefabRef>();
            findRoadConnectionJobData.m_ParcelDataComponentLookup = SystemAPI.GetComponentLookup<ParcelData>();
            findRoadConnectionJobData.m_TransformComponentLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>();
            findRoadConnectionJobData.m_ConnectedParcelsBufferLookup = GetBufferLookup<ConnectedParcel>();
            findRoadConnectionJobData.m_CurveDataComponentLookup = SystemAPI.GetComponentLookup<Curve>();
            findRoadConnectionJobData.m_CompositionDataComponentLookup = SystemAPI.GetComponentLookup<Composition>();
            findRoadConnectionJobData.m_EdgeGeometryDataComponentLookup = SystemAPI.GetComponentLookup<EdgeGeometry>();
            findRoadConnectionJobData.m_StartNodeGeometryDataComponentLookup = SystemAPI.GetComponentLookup<StartNodeGeometry>();
            findRoadConnectionJobData.m_EndNodeGeometryDataComponentLookup = SystemAPI.GetComponentLookup<EndNodeGeometry>();
            findRoadConnectionJobData.m_PrefabNetCompositionDataComponentLookup = SystemAPI.GetComponentLookup<NetCompositionData>();
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
            UpdateDataJob updateDataJobData = default;
            updateDataJobData.m_EdgeComponentLookup = SystemAPI.GetComponentLookup<Edge>(false);
            updateDataJobData.m_NodeComponentLookup = SystemAPI.GetComponentLookup<Node>(true);
            updateDataJobData.m_NodeGeoComponentLookup = SystemAPI.GetComponentLookup<NodeGeometry>(true);
            updateDataJobData.m_AggregatedComponentLookup = SystemAPI.GetComponentLookup<Aggregated>(true);
            updateDataJobData.m_ParcelComponentLookup = SystemAPI.GetComponentLookup<Parcel>(false);
            updateDataJobData.m_CreatedComponentLookup = SystemAPI.GetComponentLookup<Created>(true);
            updateDataJobData.m_TempComponentLookup = SystemAPI.GetComponentLookup<Temp>(true);
            updateDataJobData.m_SubBlockBufferLookup = GetBufferLookup<SubBlock>(false);
            updateDataJobData.m_ConnectedParcelsBufferLookup = GetBufferLookup<ConnectedParcel>();
            updateDataJobData.m_ParcelEntitiesList = parcelEntitiesList;
            updateDataJobData.m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            updateDataJobData.m_IconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer();
            updateDataJobData.m_TrafficConfigurationData = m_TrafficConfigQuery.GetSingleton<TrafficConfigurationData>();

            var replaceRoadConnectionJobHandle = updateDataJobData.Schedule(findRoadConnectionJobHandle);

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
                CheckDistance(edgeGeometry.m_Start.m_Left, position, ref maxDistance);
                CheckDistance(edgeGeometry.m_Start.m_Right, position, ref maxDistance);
                CheckDistance(edgeGeometry.m_End.m_Left, position, ref maxDistance);
                CheckDistance(edgeGeometry.m_End.m_Right, position, ref maxDistance);
                if (canBeOnRoad) {
                    CheckDistance(edgeGeometry.m_Start.m_Left, edgeGeometry.m_Start.m_Right, position, ref maxDistance);
                    CheckDistance(edgeGeometry.m_End.m_Left, edgeGeometry.m_End.m_Right, position, ref maxDistance);
                }
            }

            if (MathUtils.DistanceSquared(startGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance) {
                CheckDistance(startGeometry.m_Left.m_Left, position, ref maxDistance);
                CheckDistance(startGeometry.m_Right.m_Right, position, ref maxDistance);
                if (startGeometry.m_MiddleRadius > 0f) {
                    CheckDistance(startGeometry.m_Left.m_Right, position, ref maxDistance);
                    CheckDistance(startGeometry.m_Right.m_Left, position, ref maxDistance);
                }

                if (canBeOnRoad) {
                    if (startGeometry.m_MiddleRadius > 0f) {
                        CheckDistance(startGeometry.m_Left.m_Left, startGeometry.m_Left.m_Right, position, ref maxDistance);
                        CheckDistance(startGeometry.m_Right.m_Left, startGeometry.m_Middle, position, ref maxDistance);
                        CheckDistance(startGeometry.m_Middle, startGeometry.m_Right.m_Right, position, ref maxDistance);
                    } else {
                        CheckDistance(startGeometry.m_Left.m_Left, startGeometry.m_Middle, position, ref maxDistance);
                        CheckDistance(startGeometry.m_Middle, startGeometry.m_Right.m_Right, position, ref maxDistance);
                    }
                }
            }

            if (MathUtils.DistanceSquared(endGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance) {
                CheckDistance(endGeometry.m_Left.m_Left, position, ref maxDistance);
                CheckDistance(endGeometry.m_Right.m_Right, position, ref maxDistance);
                if (endGeometry.m_MiddleRadius > 0f) {
                    CheckDistance(endGeometry.m_Left.m_Right, position, ref maxDistance);
                    CheckDistance(endGeometry.m_Right.m_Left, position, ref maxDistance);
                }

                if (canBeOnRoad) {
                    if (endGeometry.m_MiddleRadius > 0f) {
                        CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Left.m_Right, position, ref maxDistance);
                        CheckDistance(endGeometry.m_Right.m_Left, endGeometry.m_Middle, position, ref maxDistance);
                        CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
                        return;
                    }

                    CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Middle, position, ref maxDistance);
                    CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
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
