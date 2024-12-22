// <copyright file="RoadConnectionSystem.CreateUniqueEntitiesListJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Convert the queue of parcels to a list of ConnectionUpdateDataJob structs.
        /// </summary>
        public struct CreateUniqueEntitiesListJob : IJob {
            /// <summary>
            /// todo.
            /// </summary>
            public NativeQueue<Entity> m_EntitiesToUpdateQueue;

            /// <summary>
            /// todo.
            /// </summary>
            public NativeList<RoadConnectionSystem.ConnectionUpdateDataJob> m_ConnectionUpdateDataList;

            /// <inheritdoc/>
            public void Execute() {
                // Point a list of ConnectionUpdateDataJob structs from our quuee
                int parcelsToUpdate = m_EntitiesToUpdateQueue.Count;
                m_ConnectionUpdateDataList.ResizeUninitialized(parcelsToUpdate);

                for (int i = 0; i < parcelsToUpdate; i++) {
                    m_ConnectionUpdateDataList[i] = new RoadConnectionSystem.ConnectionUpdateDataJob(m_EntitiesToUpdateQueue.Dequeue());
                }

                // Sort the list (by parcel index) so that we can easily dedupe
                m_ConnectionUpdateDataList.Sort<RoadConnectionSystem.ConnectionUpdateDataJob>();

                // Deduplicate the list
                Entity currentBuildingEntity = Entity.Null;
                int readIndex = 0;
                int writeIndex = 0;
                while (readIndex < m_ConnectionUpdateDataList.Length) {
                    RoadConnectionSystem.ConnectionUpdateDataJob currentBuildingData = m_ConnectionUpdateDataList[readIndex++];

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
