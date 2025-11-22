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

            if (firstLoad) {
                m_Log.Debug("OnUpdate() -- First load.");
            }

            if (entityQuery.IsEmptyIgnoreFilter) {
                return;
            }

            var updateSearchTreeJob = new UpdateSearchTreeJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<Owner>(),
                SystemAPI.GetComponentTypeHandle<Transform>(),
                SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                SystemAPI.GetComponentTypeHandle<Created>(),
                SystemAPI.GetComponentTypeHandle<Deleted>(),
                SystemAPI.GetComponentTypeHandle<Overridden>(),
                SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                m_ToolSystem.actionMode.IsEditor(),
                firstLoad,
                GetStaticSearchTree(false, out var updateSearchTreeJobHandle)
            );

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
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly]
            public ComponentTypeHandle<Transform> m_TransformType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

            [ReadOnly]
            public ComponentTypeHandle<Created> m_CreatedType;

            [ReadOnly]
            public ComponentTypeHandle<Deleted> m_DeletedType;

            [ReadOnly]
            public ComponentTypeHandle<Overridden> m_OverriddenType;

            [ReadOnly]
            public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

            [ReadOnly]
            public bool m_EditorMode;

            [ReadOnly]
            public bool m_FirstLoad;

            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;

            public UpdateSearchTreeJob(EntityTypeHandle entityType, ComponentTypeHandle<Owner> ownerType, ComponentTypeHandle<Transform> transformType,
                                       ComponentTypeHandle<PrefabRef> prefabRefType, ComponentTypeHandle<Created> createdType,
                                       ComponentTypeHandle<Deleted> deletedType, ComponentTypeHandle<Overridden> overriddenType,
                                       ComponentLookup<ObjectGeometryData> prefabObjectGeometryData, bool editorMode, bool firstLoad,
                                       NativeQuadTree<Entity, QuadTreeBoundsXZ> searchTree) {
                m_EntityType               = entityType;
                m_OwnerType                = ownerType;
                m_TransformType            = transformType;
                m_PrefabRefType            = prefabRefType;
                m_CreatedType              = createdType;
                m_DeletedType              = deletedType;
                m_OverriddenType           = overriddenType;
                m_PrefabObjectGeometryData = prefabObjectGeometryData;
                m_EditorMode               = editorMode;
                m_FirstLoad                = firstLoad;
                m_SearchTree               = searchTree;
            }

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