// <copyright file="P_RoadConnectionSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine.Scripting;
    using Utils;
    using SearchSystem = Game.Net.SearchSystem;

    #endregion

    /// <summary>
    /// System responsible for connecting parcels to roads.
    /// </summary>
    public partial class P_RoadConnectionSystem : GameSystemBase {
        private const float MaxDistanceFront = 8.4f;
        private const float MaxDistanceSides = 7.8f;

        // Queries
        private EntityQuery           m_ModificationQuery;
        private EntityQuery           m_TrafficConfigQuery;
        private EntityQuery           m_UpdatedNetQuery;
        private IconCommandSystem     m_IconCommandSystem;
        private ModificationBarrier4B m_ModificationBarrier;

        // Systems & References
        private P_ParcelSearchSystem m_ParcelSearchSystem;

        // Logger
        private PrefixedLogger m_Log;
        private SearchSystem   m_NetSearchSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="P_RoadConnectionSystem"/> class.
        /// </summary>
        [Preserve]
        public P_RoadConnectionSystem() { }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(P_RoadConnectionSystem));
            m_Log.Debug("OnCreate()");

            // Reference Systems
            m_ParcelSearchSystem  = World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4B>();
            m_NetSearchSystem     = World.GetOrCreateSystemManaged<SearchSystem>();
            m_IconCommandSystem   = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Define Queries
            m_ModificationQuery = SystemAPI.QueryBuilder()
                                           .WithAll<Parcel>()
                                           .WithAny<Updated, Deleted>()
                                           //.WithNone<Temp>()
                                           .AddAdditionalQuery()
                                           .WithAll<Edge, ConnectedParcel>()
                                           .WithAny<Updated, Deleted>()
                                           .WithNone<Temp>()
                                           .Build();
            m_UpdatedNetQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Edge, ConnectedParcel>()
                                         .WithAny<Created, Updated>()
                                         .WithNone<Temp>()
                                         .Build();
            m_TrafficConfigQuery = SystemAPI.QueryBuilder()
                                            .WithAll<TrafficConfigurationData>()
                                            .Build();

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
            var parcelEntitiesList  = new NativeList<UpdateData>(Allocator.TempJob);

            // [CreateEntitiesQueueJob] Populate a queue with parcels to update
            var createEntitiesQueueJobHandle = new CreateEntitiesQueueJob {
                m_EntityTypeHandle                 = SystemAPI.GetEntityTypeHandle(),
                m_ParcelEntitiesQueue              = parcelEntitiesQueue.AsParallelWriter(),
                m_ConnectedParcelBufferTypeHandle  = SystemAPI.GetBufferTypeHandle<ConnectedParcel>(),
                m_PrefabRefComponentLookup         = SystemAPI.GetComponentLookup<PrefabRef>(),
                m_EdgeGeometryComponentLookup      = SystemAPI.GetComponentLookup<EdgeGeometry>(),
                m_StartNodeGeometryComponentLookup = SystemAPI.GetComponentLookup<StartNodeGeometry>(),
                m_EndNodeGeometryComponentLookup   = SystemAPI.GetComponentLookup<EndNodeGeometry>(),
                m_ParcelDataComponentLookup        = SystemAPI.GetComponentLookup<ParcelData>(),
                m_ParcelComponentLookup            = SystemAPI.GetComponentLookup<Parcel>(),
                m_TransformComponentLookup         = SystemAPI.GetComponentLookup<Transform>(),
                m_ParcelSearchTree                 = m_ParcelSearchSystem.GetStaticSearchTree(true, out var parcelSearchJobHandle),
                m_EdgeGeometryTypeHandle           = SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                m_StartNodeGeometryTypeHandle      = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>(),
                m_EndNodeGeometryTypeHandle        = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>(),
                m_DeletedTypeHandle                = SystemAPI.GetComponentTypeHandle<Deleted>(),
                m_TempTypeHandle                   = SystemAPI.GetComponentTypeHandle<Temp>(),
            }.ScheduleParallel(
                m_ModificationQuery,
                JobHandle.CombineDependencies(Dependency, parcelSearchJobHandle)
            );

            m_ParcelSearchSystem.AddSearchTreeReader(parcelSearchJobHandle);

            // [CreateUniqueEntitiesListJob] Dedupe the list
            var createUniqueParcelListJobHandle = new ProcessQueueIntoListJob {
                m_ParcelEntitiesQueue = parcelEntitiesQueue,
                m_ParcelEntittiesList = parcelEntitiesList,
            }.Schedule(
                createEntitiesQueueJobHandle
            );

            // [FindRoadConnectionJob] Find road connections for each parcel
            var updatedNetChunks = m_UpdatedNetQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var netQueryChunkListJob);
            var findRoadConnectionJobHandle = new FindRoadConnectionJob {
                m_ParcelEntitiesList                      = parcelEntitiesList.AsDeferredJobArray(),
                m_DeletedDataComponentLookup              = SystemAPI.GetComponentLookup<Deleted>(true),
                m_PrefabRefComponentLookup                = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_ParcelDataComponentLookup               = SystemAPI.GetComponentLookup<ParcelData>(true),
                m_TransformComponentLookup                = SystemAPI.GetComponentLookup<Transform>(true),
                m_ConnectedParcelsBufferLookup            = SystemAPI.GetBufferLookup<ConnectedParcel>(true),
                m_CurveDataComponentLookup                = SystemAPI.GetComponentLookup<Curve>(true),
                m_CompositionDataComponentLookup          = SystemAPI.GetComponentLookup<Composition>(true),
                m_EdgeGeometryDataComponentLookup         = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                m_StartNodeGeometryDataComponentLookup    = SystemAPI.GetComponentLookup<StartNodeGeometry>(true),
                m_EndNodeGeometryDataComponentLookup      = SystemAPI.GetComponentLookup<EndNodeGeometry>(true),
                m_PrefabNetCompositionDataComponentLookup = SystemAPI.GetComponentLookup<NetCompositionData>(true),
                m_NetSearchTree                           = m_NetSearchSystem.GetNetSearchTree(true, out var netSearchTreeJobHandle),
                m_UpdatedNetChunks                        = updatedNetChunks,
                m_EntityTypeHandle                        = SystemAPI.GetEntityTypeHandle(),
            }.Schedule(
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
            var replaceRoadConnectionJobHandle = new UpdateDataJob {
                m_ParcelComponentLookup        = SystemAPI.GetComponentLookup<Parcel>(),
                m_CreatedComponentLookup       = SystemAPI.GetComponentLookup<Created>(true),
                m_TempComponentLookup          = SystemAPI.GetComponentLookup<Temp>(true),
                m_ConnectedParcelsBufferLookup = SystemAPI.GetBufferLookup<ConnectedParcel>(),
                m_ParcelEntitiesList           = parcelEntitiesList,
                m_CommandBuffer                = m_ModificationBarrier.CreateCommandBuffer(),
                m_IconCommandBuffer            = m_IconCommandSystem.CreateCommandBuffer(),
                m_TrafficConfigurationData     = m_TrafficConfigQuery.GetSingleton<TrafficConfigurationData>(),
            }.Schedule(findRoadConnectionJobHandle);

            // Dispose of data
            parcelEntitiesQueue.Dispose(createUniqueParcelListJobHandle);
            updatedNetChunks.Dispose(findRoadConnectionJobHandle);

            // Set base dependencies
            Dependency = replaceRoadConnectionJobHandle;

            // Register depdencies with Barrier
            m_ModificationBarrier.AddJobHandleForProducer(Dependency);

            // Dispose of our list at the end
            parcelEntitiesList.Dispose(Dependency);
        }

        private static void CheckDistance(EdgeGeometry edgeGeometry, EdgeNodeGeometry startGeometry, EdgeNodeGeometry endGeometry, float3 position,
                                          bool         canBeOnRoad,  ref float        maxDistance) {
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
                    } else {
                        CheckDistance(startGeometry.m_Left.m_Left, startGeometry.m_Middle, position, ref maxDistance);
                    }

                    CheckDistance(startGeometry.m_Middle, startGeometry.m_Right.m_Right, position, ref maxDistance);
                }
            }

            if (!(MathUtils.DistanceSquared(endGeometry.m_Bounds.xz, position.xz) < maxDistance * maxDistance)) {
                return;
            }

            CheckDistance(endGeometry.m_Left.m_Left, position, ref maxDistance);
            CheckDistance(endGeometry.m_Right.m_Right, position, ref maxDistance);

            if (endGeometry.m_MiddleRadius > 0f) {
                CheckDistance(endGeometry.m_Left.m_Right, position, ref maxDistance);
                CheckDistance(endGeometry.m_Right.m_Left, position, ref maxDistance);
            }

            if (!canBeOnRoad) {
                return;
            }

            if (endGeometry.m_MiddleRadius > 0f) {
                CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Left.m_Right, position, ref maxDistance);
                CheckDistance(endGeometry.m_Right.m_Left, endGeometry.m_Middle, position, ref maxDistance);
                CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
                return;
            }

            CheckDistance(endGeometry.m_Left.m_Left, endGeometry.m_Middle, position, ref maxDistance);
            CheckDistance(endGeometry.m_Middle, endGeometry.m_Right.m_Right, position, ref maxDistance);
        }

        private static void CheckDistance(Bezier4x3 curve1, Bezier4x3 curve2, float3 position, ref float maxDistance) {
            if (!(MathUtils.DistanceSquared(MathUtils.Bounds(curve1.xz) | MathUtils.Bounds(curve2.xz), position.xz) < maxDistance * maxDistance)) {
                return;
            }

            _ = MathUtils.Distance(MathUtils.Lerp(curve1.xz, curve2.xz, 0.5f), position.xz, out var t);

            var x = MathUtils.Distance(new Line2.Segment(MathUtils.Position(curve1.xz, t), MathUtils.Position(curve2.xz, t)), position.xz, out _);
            maxDistance = math.min(x, maxDistance);
        }

        private static void CheckDistance(Bezier4x3 curve, float3 position, ref float maxDistance) {
            if (!(MathUtils.DistanceSquared(MathUtils.Bounds(curve.xz), position.xz) < maxDistance * maxDistance)) {
                return;
            }

            var x = MathUtils.Distance(curve.xz, position.xz, out _);
            maxDistance = math.min(x, maxDistance);
        }
    }
}