// <copyright file="RoadConnectionSystem.CreateEntitiesQueue.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
        public struct CreateEntitiesQueueJob : IJobChunk {
            /// <summary>
            /// todo.
            /// </summary>
            public NativeQueue<Entity>.ParallelWriter m_EntitiesToUpdateQueue;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly]
            public BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingBufferTypeHandle;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                BufferAccessor<ConnectedBuilding> connectedBuildingsBufferAccessor = chunk.GetBufferAccessor<ConnectedBuilding>(ref m_ConnectedBuildingBufferTypeHandle);

                if (connectedBuildingsBufferAccessor.Length != 0) {
                    // todo do stuff with edges...
                } else {
                    NativeArray<Entity> parcelEntityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                    for (int m = 0; m < parcelEntityArray.Length; m++) {
                        m_EntitiesToUpdateQueue.Enqueue(parcelEntityArray[m]);
                    }
                }
            }
        }
    }
}
