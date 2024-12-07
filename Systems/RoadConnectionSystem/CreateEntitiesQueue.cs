namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
        public struct CreateEntitiesQueue : IJobChunk {
            public NativeQueue<Entity>.ParallelWriter m_EntitiesToUpdateQueue;
            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<ConnectedBuilding> m_ConnectedBuildingBufferTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var connectedBuildingsBufferAccessor = chunk.GetBufferAccessor<ConnectedBuilding>(ref m_ConnectedBuildingBufferTypeHandle);

                if (connectedBuildingsBufferAccessor.Length != 0) {
                    // todo do stuff with edges...
                } else {
                    var parcelEntityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                    for (int m = 0; m < parcelEntityArray.Length; m++) {
                        m_EntitiesToUpdateQueue.Enqueue(parcelEntityArray[m]);
                    }
                }
            }
        }
    }
}
