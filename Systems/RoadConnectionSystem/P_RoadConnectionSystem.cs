// <copyright file="P_RoadConnectionSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
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
    /// System responsible for connecting parcels to roads.
    /// </summary>
    public partial class P_RoadConnectionSystem : GameSystemBase {
        private const float MaxDistance = 8.4f;

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
                        ComponentType.ReadOnly<Deleted>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                },
                new () {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Game.Net.Edge>(),
                        ComponentType.ReadOnly<ConnectedParcel>(),
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                },
            });

            m_UpdatedNetQuery = GetEntityQuery(new EntityQueryDesc[] {
                new () {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Game.Net.Edge>(),
                        ComponentType.ReadOnly<ConnectedParcel>(),
                    },
                    Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Created>(),
                        ComponentType.ReadOnly<Updated>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                },
            });

            m_TrafficConfigQuery = base.GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<TrafficConfigurationData>(),
            });

            RequireForUpdate(m_ModificationQuery);
        }

        /// <summary>
        /// General flow:
        /// 1. Runs when m_ModificationQuery runs (updated edge or parcel entities)
        /// 2. [CreateEntitiesQueueJob] Populate a queue with parcels to update
        /// 3. [CreateUniqueEntitiesListJob] Dedupe the list
        /// 4. [FindRoadConnectionJob] Find road connections for each parcel
        /// 5. [UpdateDataJob] Update parcel and road data.
        /// </summary>
        protected override void OnUpdate() {
            // Data structures
            var parcelEntitiesQueue = new NativeQueue<Entity>(Allocator.TempJob);
            var parcelEntitiesList = new NativeList<UpdateData>(Allocator.TempJob);

            // [CreateEntitiesQueueJob] Populate a queue with parcels to update
            var createEntitiesQueueJobHandle = new CreateEntitiesQueueJob(
                entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
                parcelEntitiesQueue: parcelEntitiesQueue.AsParallelWriter(),
                connectedParcelBufferTypeHandle: SystemAPI.GetBufferTypeHandle<ConnectedParcel>(),
                prefabRefComponentLookup: SystemAPI.GetComponentLookup<PrefabRef>(),
                edgeGeometryComponentLookup: SystemAPI.GetComponentLookup<EdgeGeometry>(),
                startNodeGeometryComponentLookup: SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                endNodeGeometryComponentLookup: SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                parcelDataComponentLookup: SystemAPI.GetComponentLookup<ParcelData>(),
                parcelComponentLookup: SystemAPI.GetComponentLookup<Parcel>(),
                transformComponentLookup: SystemAPI.GetComponentLookup<Transform>(),
                parcelSearchTree: m_ParcelSearchSystem.GetStaticSearchTree(true, out var objectSearchTreeJobHandle),
                edgeGeometryTypeHandle: SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                startNodeGeometryTypeHandle: SystemAPI.GetComponentTypeHandle<StartNodeGeometry>(),
                endNodeGeometryTypeHandle: SystemAPI.GetComponentTypeHandle<EndNodeGeometry>(),
                deletedTypeHandle: SystemAPI.GetComponentTypeHandle<Deleted>()
            ).ScheduleParallel(
                m_ModificationQuery,
                JobHandle.CombineDependencies(base.Dependency, objectSearchTreeJobHandle)
            );

            // [CreateUniqueEntitiesListJob] Dedupe the list
            var createUniqueParcelListJobHandle = new CreateUniqueEntitiesListJob(
                parcelEntitiesQueue: parcelEntitiesQueue,
                parcelEntittiesList: parcelEntitiesList
            ).Schedule(
                createEntitiesQueueJobHandle
            );

            // [FindRoadConnectionJob] Find road connections for each parcel
            var updatedNetChunks = m_UpdatedNetQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var netQueryChunkListJob);
            var findRoadConnectionJobHandle = new FindRoadConnectionJob(
                entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
                deletedDataComponentLookup: SystemAPI.GetComponentLookup<Deleted>(),
                prefabRefComponentLookup: SystemAPI.GetComponentLookup<PrefabRef>(),
                parcelDataComponentLookup: SystemAPI.GetComponentLookup<ParcelData>(),
                transformComponentLookup: SystemAPI.GetComponentLookup<Game.Objects.Transform>(),
                connectedParcelsBufferLookup: SystemAPI.GetBufferLookup<ConnectedParcel>(),
                curveDataComponentLookup: SystemAPI.GetComponentLookup<Curve>(),
                compositionDataComponentLookup: SystemAPI.GetComponentLookup<Composition>(),
                edgeGeometryDataComponentLookup: SystemAPI.GetComponentLookup<EdgeGeometry>(),
                startNodeGeometryDataComponentLookup: SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                endNodeGeometryDataComponentLookup: SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                prefabNetCompositionDataComponentLookup: SystemAPI.GetComponentLookup<NetCompositionData>(),
                updatedNetChunks: updatedNetChunks,
                netSearchTree: m_NetSearchSystem.GetNetSearchTree(true, out var netSearchTreeJobHandle),
                parcelEntitiesList: parcelEntitiesList.AsDeferredJobArray()
            ).Schedule(
                parcelEntitiesList,
                1,
                JobHandle.CombineDependencies(
                    createUniqueParcelListJobHandle,
                    netQueryChunkListJob,
                    netSearchTreeJobHandle
                )
            );
            m_NetSearchSystem.AddNetSearchTreeReader(findRoadConnectionJobHandle);

            // [UpdateDataJob] Update parcel and road data.
            var replaceRoadConnectionJobHandle = new UpdateDataJob(
                edgeComponentLookup: SystemAPI.GetComponentLookup<Edge>(false),
                nodeGeoComponentLookup: SystemAPI.GetComponentLookup<NodeGeometry>(true),
                aggregatedComponentLookup: SystemAPI.GetComponentLookup<Aggregated>(true),
                parcelComponentLookup: SystemAPI.GetComponentLookup<Parcel>(false),
                createdComponentLookup: SystemAPI.GetComponentLookup<Created>(true),
                tempComponentLookup: SystemAPI.GetComponentLookup<Temp>(true),
                subBlockBufferLookup: SystemAPI.GetBufferLookup<ParcelSubBlock>(false),
                connectedParcelsBufferLookup: SystemAPI.GetBufferLookup<ConnectedParcel>(),
                parcelEntitiesList: parcelEntitiesList,
                commandBuffer: m_ModificationBarrier.CreateCommandBuffer(),
                iconCommandBuffer: m_IconCommandSystem.CreateCommandBuffer(),
                trafficConfigurationData: m_TrafficConfigQuery.GetSingleton<TrafficConfigurationData>()
            ).Schedule(findRoadConnectionJobHandle);

            // Dispose of data
            parcelEntitiesQueue.Dispose(createUniqueParcelListJobHandle);
            updatedNetChunks.Dispose(findRoadConnectionJobHandle);

            // Set base dependencies
            base.Dependency = replaceRoadConnectionJobHandle;

            // Register depdencies with Barrier
            m_ModificationBarrier.AddJobHandleForProducer(base.Dependency);

            // Dispose of our list at the end
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
