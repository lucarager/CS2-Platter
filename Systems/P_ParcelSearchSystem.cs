// <copyright file="ParcelSearchSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Serialization;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using GeometryFlags = Game.Objects.GeometryFlags;

    public partial class P_ParcelSearchSystem : GameSystemBase, IPreDeserialize {
        // Logger
        private PrefixedLogger m_Log;

        private ToolSystem m_ToolSystem;
        private EntityQuery m_UpdatedQuery;
        private EntityQuery m_AllQuery;
        private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_StaticSearchTree;
        private NativeQuadTree<Entity, QuadTreeBoundsXZ> m_MovingSearchTree;
        private JobHandle m_StaticReadDependencies;
        private JobHandle m_StaticWriteDependencies;
        private JobHandle m_MovingReadDependencies;
        private JobHandle m_MovingWriteDependencies;
        private bool m_NeedFirstLoad;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelSearchSystem));
            m_Log.Debug($"OnCreate()");

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // Queries
            m_UpdatedQuery = GetEntityQuery(new EntityQueryDesc[]
            {
                new() {
                    All = new ComponentType[]
                    {
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
                }
            });
            m_AllQuery = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Parcel>(),
                ComponentType.Exclude<Temp>()
            });
            m_StaticSearchTree = new NativeQuadTree<Entity, QuadTreeBoundsXZ>(1f, Allocator.Persistent);
            m_MovingSearchTree = new NativeQuadTree<Entity, QuadTreeBoundsXZ>(1f, Allocator.Persistent);
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_StaticReadDependencies.Complete();
            m_StaticWriteDependencies.Complete();
            m_StaticSearchTree.Dispose();
            m_MovingReadDependencies.Complete();
            m_MovingWriteDependencies.Complete();
            m_MovingSearchTree.Dispose();
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
                m_Log.Debug($"OnUpdate() -- First load.");
            }

            if (entityQuery.IsEmptyIgnoreFilter) {
                return;
            }

            var updateSearchTreeJob = default(P_ParcelSearchSystem.UpdateSearchTreeJob);
            updateSearchTreeJob.m_EntityType = SystemAPI.GetEntityTypeHandle();
            updateSearchTreeJob.m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>();
            updateSearchTreeJob.m_TransformType = SystemAPI.GetComponentTypeHandle<Game.Objects.Transform>();
            updateSearchTreeJob.m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>();
            updateSearchTreeJob.m_CreatedType = SystemAPI.GetComponentTypeHandle<Created>();
            updateSearchTreeJob.m_DeletedType = SystemAPI.GetComponentTypeHandle<Deleted>();
            updateSearchTreeJob.m_OverriddenType = SystemAPI.GetComponentTypeHandle<Overridden>();
            updateSearchTreeJob.m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>();
            updateSearchTreeJob.m_EditorMode = m_ToolSystem.actionMode.IsEditor();
            updateSearchTreeJob.m_FirstLoad = firstLoad;
            updateSearchTreeJob.m_SearchTree = GetStaticSearchTree(false, out var updateSearchTreeJobHandle);

            base.Dependency = updateSearchTreeJob.Schedule(entityQuery, JobHandle.CombineDependencies(base.Dependency, updateSearchTreeJobHandle));

            AddStaticSearchTreeWriter(base.Dependency);
        }

        public NativeQuadTree<Entity, QuadTreeBoundsXZ> GetStaticSearchTree(bool readOnly, out JobHandle dependencies) {
            dependencies = readOnly ? m_StaticWriteDependencies : JobHandle.CombineDependencies(m_StaticReadDependencies, m_StaticWriteDependencies);
            return m_StaticSearchTree;
        }

        public NativeQuadTree<Entity, QuadTreeBoundsXZ> GetMovingSearchTree(bool readOnly, out JobHandle dependencies) {
            dependencies = readOnly ? m_MovingWriteDependencies : JobHandle.CombineDependencies(m_MovingReadDependencies, m_MovingWriteDependencies);
            return m_MovingSearchTree;
        }

        public void AddStaticSearchTreeReader(JobHandle jobHandle) {
            m_StaticReadDependencies = JobHandle.CombineDependencies(m_StaticReadDependencies, jobHandle);
        }

        public void AddStaticSearchTreeWriter(JobHandle jobHandle) {
            m_StaticWriteDependencies = jobHandle;
        }

        public void AddMovingSearchTreeReader(JobHandle jobHandle) {
            m_MovingReadDependencies = JobHandle.CombineDependencies(m_MovingReadDependencies, jobHandle);
        }

        public void AddMovingSearchTreeWriter(JobHandle jobHandle) {
            m_MovingWriteDependencies = jobHandle;
        }

        /// <inheritdoc/>
        public void PreDeserialize(Context context) {
            m_Log.Debug($"PreDeserialize()");

            var staticSearchTree = GetStaticSearchTree(false, out var jobHandle);
            var movingSearchTree = GetMovingSearchTree(false, out var jobHandle2);
            jobHandle.Complete();
            jobHandle2.Complete();
            staticSearchTree.Clear();
            movingSearchTree.Clear();
            m_NeedFirstLoad = true;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateSearchTreeJob : IJobChunk {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType;
            [ReadOnly] public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
            [ReadOnly] public ComponentTypeHandle<Created> m_CreatedType;
            [ReadOnly] public ComponentTypeHandle<Deleted> m_DeletedType;
            [ReadOnly] public ComponentTypeHandle<Overridden> m_OverriddenType;
            [ReadOnly] public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
            [ReadOnly] public bool m_EditorMode;
            [ReadOnly] public bool m_FirstLoad;
            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;

            public void Execute(in ArchetypeChunk chunk) {
                var entityArray = chunk.GetNativeArray(m_EntityType);

                if (chunk.Has<Deleted>(ref m_DeletedType)) {
                    for (var i = 0; i < entityArray.Length; i++) {
                        var entity = entityArray[i];
                        m_SearchTree.TryRemove(entity);
                    }

                    return;
                }

                var prefabRefArray = chunk.GetNativeArray<PrefabRef>(ref m_PrefabRefType);
                var transformArray = chunk.GetNativeArray<Game.Objects.Transform>(ref m_TransformType);
                for (var j = 0; j < entityArray.Length; j++) {
                    var parcelEntity = entityArray[j];
                    var prefabRef = prefabRefArray[j];
                    var transform = transformArray[j];

                    if (m_PrefabObjectGeometryData.TryGetComponent(prefabRef.m_Prefab, out var objectGeometryData)) {
                        var bounds = ObjectUtils.CalculateBounds(transform.m_Position, transform.m_Rotation, objectGeometryData);
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

                        if (m_FirstLoad || chunk.Has<Created>(ref m_CreatedType)) {
                            m_SearchTree.Add(parcelEntity, new QuadTreeBoundsXZ(bounds, boundsMask, objectGeometryData.m_MinLod));
                        } else {
                            m_SearchTree.Update(parcelEntity, new QuadTreeBoundsXZ(bounds, boundsMask, objectGeometryData.m_MinLod));
                        }
                    }
                }
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                Execute(in chunk);
            }
        }
    }
}
