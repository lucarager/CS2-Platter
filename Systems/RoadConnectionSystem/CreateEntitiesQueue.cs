namespace Platter.Systems {
    using Colossal.Collections;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    public static partial class RoadConnectionJobs {
        /// <summary>
        /// Find eligible entities and add them to a queue.
        /// </summary>
        public struct CreateEntitiesQueue : IJobChunk {
            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;
            public NativeQueue<Entity>.ParallelWriter m_EntitiesToUpdateQueue;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                NativeArray<Entity> parcelEntityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                for (int m = 0; m < parcelEntityArray.Length; m++) {
                    m_EntitiesToUpdateQueue.Enqueue(parcelEntityArray[m]);
                }

                Mod.Instance.Log.Debug($"[RoadConnectionJobs->CreateEntitiesQueue] Created a queue");
            }

        }
    }
}
