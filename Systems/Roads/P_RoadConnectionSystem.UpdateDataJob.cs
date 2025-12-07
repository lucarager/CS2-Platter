// <copyright file="P_RoadConnectionSystem.UpdateDataJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Collections;
    using Components;
    using Game;
    using Game.Common;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct UpdateDataJob : IJob {
            [ReadOnly] public required NativeList<UpdateData>        m_ParcelEntitiesList;
            [ReadOnly] public required ComponentLookup<Created>      m_CreatedComponentLookup;
            [ReadOnly] public required ComponentLookup<Temp>         m_TempComponentLookup;
            [ReadOnly] public required TrafficConfigurationData      m_TrafficConfigurationData;
            public required            ComponentLookup<Parcel>       m_ParcelComponentLookup;
            public required            EntityCommandBuffer           m_CommandBuffer;
            public required            IconCommandBuffer             m_IconCommandBuffer;
            public required            BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            /// <inheritdoc/>
            public void Execute() {
                if (m_ParcelEntitiesList.Length == 0) {
                    return;
                }

                foreach (var updateData in m_ParcelEntitiesList) {
                    var parcel = m_ParcelComponentLookup[updateData.m_Parcel];

                    // A few utility bools
                    var parcelHasCreatedComponent = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHasTempComponent    = m_TempComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHadRoad             = parcel.m_RoadEdge    != Entity.Null;
                    var parcelHasNewRoad          = updateData.m_FrontRoad != Entity.Null;
                    var noRoad                    = parcel.m_RoadEdge == Entity.Null && updateData.m_FrontRoad == Entity.Null;
                    var roadChanged               = updateData.m_FrontRoad  != parcel.m_RoadEdge;
                    var parcelHasNewCurvePosition = updateData.m_FrontCurvePos != parcel.m_CurvePosition;

                    // Determine if we should perform an update
                    // Either the "new best road" we found is not the one the parcel has stored
                    // or it's a newly created parcel
                    if (roadChanged || parcelHasCreatedComponent) {
                        // Check if the parcel had a road and now doesn't, or vice versa, to update the icon status
                        if (parcelHadRoad != parcelHasNewRoad) {
                            if (parcelHasNewRoad) {
                                m_IconCommandBuffer.Remove(
                                    updateData.m_Parcel,
                                    m_TrafficConfigurationData.m_RoadConnectionNotification
                                );
                            } else if (!updateData.m_Deleted) {
                                m_IconCommandBuffer.Add(
                                    updateData.m_Parcel,
                                    m_TrafficConfigurationData.m_RoadConnectionNotification,
                                    updateData.m_FrontPos,
                                    IconPriority.Warning,
                                    IconClusterLayer.Default,
                                    IconFlags.OnTop
                                );
                            }
                        }

                        // Handle TEMP parcels and exit early
                        if (roadChanged && parcelHasTempComponent) {
                            parcel.m_RoadEdge                            = updateData.m_FrontRoad;
                            parcel.m_CurvePosition                       = updateData.m_FrontCurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
                            return;
                        }

                        // Remove old ConnectedParcel
                        if (parcelHadRoad && m_ConnectedParcelsBufferLookup.HasBuffer(parcel.m_RoadEdge)) {
                            CollectionUtils.RemoveValue(m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge], new ConnectedParcel(updateData.m_Parcel));
                        }

                        // Update data
                        parcel.m_RoadEdge                            = updateData.m_FrontRoad;
                        parcel.m_CurvePosition                       = updateData.m_FrontCurvePos;
                        m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                        m_CommandBuffer.AddComponent(updateData.m_Parcel, new Updated());

                        if (parcelHasNewRoad) {
                            m_ConnectedParcelsBufferLookup[updateData.m_FrontRoad].Add(new ConnectedParcel(updateData.m_Parcel));
                        }
                    } else if (parcelHasNewCurvePosition) {
                        parcel.m_CurvePosition                       = updateData.m_FrontCurvePos;
                        m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
                        m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, default);
                    }

                    if (noRoad && !updateData.m_Deleted) {
                        // Mark parcel as updated
                        m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, default);

                        // Make sure we have the icons pop up when no roads are present
                        m_IconCommandBuffer.Add(
                            updateData.m_Parcel,
                            m_TrafficConfigurationData.m_RoadConnectionNotification,
                            updateData.m_FrontPos,
                            IconPriority.Warning,
                            IconClusterLayer.Default,
                            IconFlags.OnTop
                        );
                    }
                }
            }
        }
    }
}