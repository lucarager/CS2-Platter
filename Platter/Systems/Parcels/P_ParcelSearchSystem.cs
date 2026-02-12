// <copyright file="P_ParcelSearchSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Collections;
    using Colossal.Serialization.Entities;
    using Components;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Serialization;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using GeometryFlags = Game.Objects.GeometryFlags;

    #endregion

    /// <summary>
    /// System responsible for maintaing a QuadSearchTree for parcels.
    /// </summary>
    public partial class P_ParcelSearchSystem : GameSystemBase, IPreDeserialize {
        private bool        m_NeedFirstLoad;
        private EntityQuery m_AllQuery;

        // Queries
        private EntityQuery m_UpdatedQuery;
        private JobHandle   m_MovingReadDependencies;
        private JobHandle   m_MovingWriteDependencies;
        private JobHandle   m_StaticReadDependencies;
        private JobHandle   m_StaticWriteDependencies;

        // Data & Jobs
        private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;

        // Logger
        private PrefixedLogger m_Log;

        // Systems
        private ToolSystem m_ToolSystem;
        public  bool       Loaded => !m_NeedFirstLoad;

        /// <inheritdoc/>
        public void PreDeserialize(Context context) {
            m_Log.Debug("PreDeserialize()");

            var staticSearchTree = GetStaticSearchTree(false, out var searchTreeJobHandle);
            searchTreeJobHandle.Complete();
            staticSearchTree.Clear();
            m_NeedFirstLoad = true;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelSearchSystem));
            m_Log.Debug("OnCreate()");

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // Queries
            m_UpdatedQuery = SystemAPI.QueryBuilder()
                                      .WithAll<Parcel, Initialized>()
                                      .WithAny<Updated, Deleted>()
                                      .WithNone<Temp>()
                                      .Build();
            m_AllQuery = SystemAPI.QueryBuilder()
                                  .WithAll<Parcel, Initialized>()
                                  .WithNone<Temp>()
                                  .Build();

            m_SearchTree = new NativeQuadTree<Entity, QuadTreeBoundsXZ>(1f, Allocator.Persistent);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_StaticReadDependencies.Complete();
            m_StaticWriteDependencies.Complete();
            m_SearchTree.Dispose();
            m_MovingReadDependencies.Complete();
            m_MovingWriteDependencies.Complete();
            base.OnDestroy();
        }

        private bool GetIfFirstLoad() {
            if (m_NeedFirstLoad) {
                m_NeedFirstLoad = false;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var firstLoad = GetIfFirstLoad();

            var entityQuery = firstLoad ? m_AllQuery : m_UpdatedQuery;

            if (entityQuery.IsEmptyIgnoreFilter) {
                return;
            }

            if (firstLoad) {
                m_Log.Debug("OnUpdate() -- First load, initializing search tree.");
            } else {
                m_Log.Debug("OnUpdate() -- Reloading search tree.");
            }

            var updateSearchTreeJob = new UpdateSearchTreeJob() {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(),
                m_TransformType = SystemAPI.GetComponentTypeHandle<Transform>(),
                m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                m_CreatedType = SystemAPI.GetComponentTypeHandle<Created>(),
                m_DeletedType = SystemAPI.GetComponentTypeHandle<Deleted>(),
                m_OverriddenType = SystemAPI.GetComponentTypeHandle<Overridden>(),
                m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                m_EditorMode = m_ToolSystem.actionMode.IsEditor(),
                m_FirstLoad = firstLoad,
                m_SearchTree = GetStaticSearchTree(false, out var updateSearchTreeJobHandle)
            };

            Dependency = updateSearchTreeJob.Schedule(entityQuery, JobHandle.CombineDependencies(Dependency, updateSearchTreeJobHandle));

            AddSearchTreeWriter(Dependency);
        }

        public NativeQuadTree<Entity, QuadTreeBoundsXZ> GetStaticSearchTree(bool readOnly, out JobHandle dependencies) {
            dependencies = readOnly ? m_StaticWriteDependencies : JobHandle.CombineDependencies(m_StaticReadDependencies, m_StaticWriteDependencies);
            return m_SearchTree;
        }

        public void AddSearchTreeReader(JobHandle jobHandle) { m_StaticReadDependencies = JobHandle.CombineDependencies(m_StaticReadDependencies, jobHandle); }

        public void AddSearchTreeWriter(JobHandle jobHandle) { m_StaticWriteDependencies = jobHandle; }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateSearchTreeJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle m_EntityType;
            [ReadOnly] public required ComponentTypeHandle<Owner> m_OwnerType;
            [ReadOnly] public required ComponentTypeHandle<Transform> m_TransformType;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef> m_PrefabRefType;
            [ReadOnly] public required ComponentTypeHandle<Created> m_CreatedType;
            [ReadOnly] public required ComponentTypeHandle<Deleted> m_DeletedType;
            [ReadOnly] public required ComponentTypeHandle<Overridden> m_OverriddenType;
            [ReadOnly] public required ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
            [ReadOnly] public required bool m_EditorMode;
            [ReadOnly] public required bool m_FirstLoad;
            public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;
            
            public void Execute(in ArchetypeChunk chunk) {
                var entityArray = chunk.GetNativeArray(m_EntityType);

                if (chunk.Has(ref m_DeletedType)) {
                    for (var i = 0; i < entityArray.Length; i++) {
                        var entity = entityArray[i];
                        m_SearchTree.TryRemove(entity);
                    }

                    return;
                }

                var prefabRefArray = chunk.GetNativeArray(ref m_PrefabRefType);
                var transformArray = chunk.GetNativeArray(ref m_TransformType);

                for (var j = 0; j < entityArray.Length; j++) {
                    var parcelEntity = entityArray[j];
                    var prefabRef    = prefabRefArray[j];
                    var transform    = transformArray[j];

                    if (m_PrefabObjectGeometryData.TryGetComponent(prefabRef.m_Prefab, out var objectGeometryData)) {
                        var bounds     = ObjectUtils.CalculateBounds(transform.m_Position, transform.m_Rotation, objectGeometryData);
                        var boundsMask = BoundsMask.Debug;

                        if ((objectGeometryData.m_Flags & GeometryFlags.OccupyZone) != GeometryFlags.None) {
                            boundsMask |= BoundsMask.OccupyZone;
                        }

                        if ((objectGeometryData.m_Flags & GeometryFlags.WalkThrough) == GeometryFlags.None) {
                            boundsMask |= BoundsMask.NotWalkThrough;
                        }

                        if ((objectGeometryData.m_Flags & GeometryFlags.HasLot) != GeometryFlags.None) {
                            boundsMask |= BoundsMask.HasLot;
                        }

                        if (m_FirstLoad || chunk.Has(ref m_CreatedType)) {
                            m_SearchTree.Add(parcelEntity, new QuadTreeBoundsXZ(bounds, boundsMask, objectGeometryData.m_MinLod));
                        } else {
                            m_SearchTree.Update(parcelEntity, new QuadTreeBoundsXZ(bounds, boundsMask, objectGeometryData.m_MinLod));
                        }
                    }
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) { Execute(in chunk); }
        }
    }
}