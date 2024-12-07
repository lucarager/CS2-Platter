namespace Platter.Systems {
    using Game;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Convert the queue of parcels to a list of ConnectionUpdateData structs.
        /// </summary>
        public struct CreateUniqueEntitiesList : IJob {
            public NativeQueue<Entity> m_EntitiesToUpdateQueue;
            public NativeList<RoadConnectionSystem.ConnectionUpdateData> m_ConnectionUpdateDataList;

            public void Execute() {
                // Create a list of ConnectionUpdateData structs from our quuee
                int parcelsToUpdate = m_EntitiesToUpdateQueue.Count;
                m_ConnectionUpdateDataList.ResizeUninitialized(parcelsToUpdate);

                for (int i = 0; i < parcelsToUpdate; i++) {
                    m_ConnectionUpdateDataList[i] = new RoadConnectionSystem.ConnectionUpdateData(m_EntitiesToUpdateQueue.Dequeue());
                }

                // Sort the list (by parcel index) so that we can easily dedupe
                m_ConnectionUpdateDataList.Sort<RoadConnectionSystem.ConnectionUpdateData>();

                // Deduplicate the list
                Entity currentBuildingEntity = Entity.Null;
                int readIndex = 0;
                int writeIndex = 0;
                while (readIndex < m_ConnectionUpdateDataList.Length) {
                    RoadConnectionSystem.ConnectionUpdateData currentBuildingData = m_ConnectionUpdateDataList[readIndex++];
                    // If the current data's parcel entity is not what we have stored previously,
                    // we can store it back into the list at the current valid write index
                    if (currentBuildingData.m_Parcel != currentBuildingEntity) {
                        m_ConnectionUpdateDataList[writeIndex++] = currentBuildingData;
                        currentBuildingEntity = currentBuildingData.m_Parcel;
                    }
                }

                // If the deduplication mechanism reduced our list, shorten it
                if (writeIndex < m_ConnectionUpdateDataList.Length) {
                    m_ConnectionUpdateDataList.RemoveRange(writeIndex, this.m_ConnectionUpdateDataList.Length - writeIndex);
                }
            }
        }
    }
}
