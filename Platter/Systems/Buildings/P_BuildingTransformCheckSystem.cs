// <copyright file="P_BuildingTransformCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
    using Utils;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// System responsible for connecting buildings to their parcels.
    /// </summary>
    public partial class P_BuildingTransformCheckSystem : PlatterGameSystemBase {
        private EntityQuery m_UpdatedQuery;
        private ModificationBarrier1 m_ModificationBarrier1;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ModificationBarrier1 = World.GetOrCreateSystemManaged<ModificationBarrier1>();

            // Queries
            m_UpdatedQuery = SystemAPI.QueryBuilder()
                                      .WithAll<Building, Transform, LinkedParcel, GrowableBuilding>()
                                      .WithAny<Updated, BatchesUpdated>()
                                      .WithNone<Temp, Hidden>()
                                      .Build();
            m_UpdatedQuery.SetChangedVersionFilter(typeof(Transform));

            // Update Cycle
            RequireForUpdate(m_UpdatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_UpdatedQuery.IsEmpty) {
                return;
            }

            m_Log.Debug("OnUpdate()");

            var updateJobHandle = new UpdateCachedTransformJob {
                m_EntityTypeHandle          = SystemAPI.GetEntityTypeHandle(),
                m_CachedTransformTypeHandle = SystemAPI.GetComponentTypeHandle<CachedTransform>(),
                m_TransformTypeHandle       = SystemAPI.GetComponentTypeHandle<Transform>(),
                m_CommandBuffer             = m_ModificationBarrier1.CreateCommandBuffer().AsParallelWriter(),
            }.ScheduleParallel(m_UpdatedQuery, Dependency);

            m_ModificationBarrier1.AddJobHandleForProducer(updateJobHandle);
            Dependency = updateJobHandle;
        }

#if USE_BURST
        [BurstCompile]
#endif
        private struct UpdateCachedTransformJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                     m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Transform>       m_TransformTypeHandle;
            public required            ComponentTypeHandle<CachedTransform> m_CachedTransformTypeHandle;
            public required            EntityCommandBuffer.ParallelWriter   m_CommandBuffer;

            public UpdateCachedTransformJob(EntityTypeHandle               entityTypeHandle,    ComponentTypeHandle<CachedTransform> cachedTransformTypeHandle,
                                            ComponentTypeHandle<Transform> transformTypeHandle, EntityCommandBuffer.ParallelWriter   commandBuffer) {
                m_EntityTypeHandle          = entityTypeHandle;
                m_CachedTransformTypeHandle = cachedTransformTypeHandle;
                m_TransformTypeHandle       = transformTypeHandle;
                m_CommandBuffer             = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entityArray          = chunk.GetNativeArray(m_EntityTypeHandle);
                var cachedTransformArray = chunk.GetNativeArray(ref m_CachedTransformTypeHandle);
                var transformArray       = chunk.GetNativeArray(ref m_TransformTypeHandle);

                for (var i = 0; i < chunk.Count; i++) {
                    var currentEntity     = entityArray[i];
                    var originalTransform = transformArray[i];

                    // BurstLogger.Debug("[P_BuildingTransformCheckSystem]",
                                      $"Checking Building {currentEntity}");

                    if (!chunk.Has(ref m_CachedTransformTypeHandle)) {
                        // BurstLogger.Debug("[P_BuildingTransformCheckSystem]", 
                                          $"Adding CachedTransform for Building {currentEntity}");
                        m_CommandBuffer.AddComponent(
                            unfilteredChunkIndex,
                            currentEntity,
                            new CachedTransform {
                                m_Position = originalTransform.m_Position,
                                m_Rotation = originalTransform.m_Rotation,
                            });
                    } else {
                        var cachedTransform = cachedTransformArray[i];

                        if (Equals(cachedTransform, originalTransform)) {
                            continue;
                        }

                        // BurstLogger.Debug("[P_BuildingTransformCheckSystem]", 
                                          $"Updating CachedTransform for Building {currentEntity}");

                        cachedTransform.m_Position = originalTransform.m_Position;
                        cachedTransform.m_Rotation = originalTransform.m_Rotation;
                        m_CommandBuffer.SetComponent(unfilteredChunkIndex, currentEntity, cachedTransform);
                        m_CommandBuffer.AddComponent<TransformUpdated>(unfilteredChunkIndex, currentEntity, default);
                    }
                }
            }

            private static bool Equals(CachedTransform record, Transform original) {
                if (!Mathf.Approximately(record.m_Position.x, original.m_Position.x) ||
                    !Mathf.Approximately(record.m_Position.y, original.m_Position.y) ||
                    !Mathf.Approximately(record.m_Position.z, original.m_Position.z)) {
                    return false;
                }

                return Mathf.Approximately(record.m_Rotation.value.x, original.m_Rotation.value.x) &&
                       Mathf.Approximately(record.m_Rotation.value.y, original.m_Rotation.value.y) &&
                       Mathf.Approximately(record.m_Rotation.value.z, original.m_Rotation.value.z) &&
                       Mathf.Approximately(record.m_Rotation.value.w, original.m_Rotation.value.w);
            }
        }
    }
}