// <copyright file="P_BuildingTransformCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Transform = Game.Objects.Transform;

    /// <summary>
    /// System responsible for connecting buildings to their parcels.
    /// </summary>
    public partial class P_BuildingTransformCheckSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_UpdatedQuery;

        // Systems
        private ModificationBarrier1 m_ModificationBarrier1;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingTransformCheckSystem));
            m_Log.Debug("OnCreate()");

            // Systems
            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            // Queries
            m_UpdatedQuery = SystemAPI.QueryBuilder()
                                      .WithAll<Building, LinkedParcel, GrowableBuilding>()
                                      .WithAny<Updated, BatchesUpdated>()
                                      .WithNone<Temp>()
                                      .Build();

            // Update Cycle
            RequireForUpdate(m_UpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug("OnUpdate()");

            var updateJobHandle = new UpdateCachedTransformJob(
                entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
                cachedTransformTypeHandle: SystemAPI.GetComponentTypeHandle<CachedTransform>(),
                transformTypeHandle: SystemAPI.GetComponentTypeHandle<Transform>(),
                commandBuffer: m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter()
            ).ScheduleParallel(m_UpdatedQuery, base.Dependency);

            m_ModificationBarrier1.AddJobHandleForProducer(updateJobHandle);
            base.Dependency = updateJobHandle;
        }

#if BURST
        [BurstCompile]
#endif
        private struct UpdateCachedTransformJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                     m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Transform>       m_TransformTypeHandle;
            private            ComponentTypeHandle<CachedTransform> m_CachedTransformTypeHandle;
            private            EntityCommandBuffer.ParallelWriter   m_CommandBuffer;

            public UpdateCachedTransformJob(EntityTypeHandle entityTypeHandle, ComponentTypeHandle<CachedTransform> cachedTransformTypeHandle, ComponentTypeHandle<Transform> transformTypeHandle, EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle = entityTypeHandle;
                m_CachedTransformTypeHandle = cachedTransformTypeHandle;
                m_TransformTypeHandle = transformTypeHandle;
                m_CommandBuffer = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray          = chunk.GetNativeArray(m_EntityTypeHandle);
                var cachedTransformArray = chunk.GetNativeArray(ref m_CachedTransformTypeHandle);
                var transformArray       = chunk.GetNativeArray(ref m_TransformTypeHandle);

                for (var i = 0; i < chunk.Count; i++) {
                    var             currentEntity     = entityArray[i];
                    var             originalTransform = transformArray[i];

                    if (!chunk.Has(ref m_CachedTransformTypeHandle)) {
                        m_CommandBuffer.AddComponent(unfilteredChunkIndex, currentEntity, new CachedTransform {
                            m_Position = originalTransform.m_Position,
                            m_Rotation = originalTransform.m_Rotation,
                        });
                    } else {
                        var cachedTransform = cachedTransformArray[i];

                        if (Equals(cachedTransform, originalTransform)) {
                            continue;
                        }

                        cachedTransform.m_Position = originalTransform.m_Position;
                        cachedTransform.m_Rotation = originalTransform.m_Rotation;
                        m_CommandBuffer.SetComponent(unfilteredChunkIndex, currentEntity, cachedTransform);
                        m_CommandBuffer.AddComponent<TransformUpdated>(unfilteredChunkIndex, currentEntity, default);
                    }
                }
            }

            private static bool Equals(CachedTransform record, Transform original) {
                if (!UnityEngine.Mathf.Approximately(record.m_Position.x, original.m_Position.x) ||
                    !UnityEngine.Mathf.Approximately(record.m_Position.y, original.m_Position.y) ||
                    !UnityEngine.Mathf.Approximately(record.m_Position.z, original.m_Position.z)) {
                    return false;
                }

                return UnityEngine.Mathf.Approximately(record.m_Rotation.value.x, original.m_Rotation.value.x) &&
                       UnityEngine.Mathf.Approximately(record.m_Rotation.value.y, original.m_Rotation.value.y) &&
                       UnityEngine.Mathf.Approximately(record.m_Rotation.value.z, original.m_Rotation.value.z) &&
                       UnityEngine.Mathf.Approximately(record.m_Rotation.value.w, original.m_Rotation.value.w);
            }
        }
    }
}