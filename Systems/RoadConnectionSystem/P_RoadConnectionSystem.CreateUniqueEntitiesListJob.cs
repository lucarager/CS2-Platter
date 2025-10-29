// <copyright file="P_RoadConnectionSystem.CreateUniqueEntitiesListJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Unity.Burst;

namespace Platter.Systems {
    using Game;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Convert the queue of parcels to a list of UpdateData structs.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct CreateUniqueEntitiesListJob : IJob {
            private NativeQueue<Entity> m_ParcelEntitiesQueue;
            private NativeList<UpdateData> m_ParcelEntittiesList;

            public CreateUniqueEntitiesListJob(NativeQueue<Entity> parcelEntitiesQueue, NativeList<UpdateData> parcelEntittiesList) {
                m_ParcelEntitiesQueue = parcelEntitiesQueue;
                m_ParcelEntittiesList = parcelEntittiesList;
            }

            /// <inheritdoc/>
            public void Execute() {
                // Point a list of UpdateData structs from our quuee
                var parcels = m_ParcelEntitiesQueue.Count;
                m_ParcelEntittiesList.ResizeUninitialized(parcels);

                for (var i = 0; i < parcels; i++) {
                    m_ParcelEntittiesList[i] = new UpdateData(m_ParcelEntitiesQueue.Dequeue());
                }

                // Sort the list (by parcel index) so that we can easily dedupe
                m_ParcelEntittiesList.Sort<UpdateData>();

                // Deduplicate the list
                var currentBuildingEntity = Entity.Null;
                var readIndex = 0;
                var writeIndex = 0;
                while (readIndex < m_ParcelEntittiesList.Length) {
                    var currentBuildingData = m_ParcelEntittiesList[readIndex++];

                    // If the current data's parcel entity is not what we have stored previously,
                    // we can store it back into the list at the current valid write index
                    if (currentBuildingData.m_Parcel == currentBuildingEntity) {
                        continue;
                    }

                    m_ParcelEntittiesList[writeIndex++] = currentBuildingData;
                    currentBuildingEntity               = currentBuildingData.m_Parcel;
                }

                // If the deduplication mechanism reduced our list, shorten it
                if (writeIndex < m_ParcelEntittiesList.Length) {
                    m_ParcelEntittiesList.RemoveRange(writeIndex, m_ParcelEntittiesList.Length - writeIndex);
                }
            }
        }
    }
}
