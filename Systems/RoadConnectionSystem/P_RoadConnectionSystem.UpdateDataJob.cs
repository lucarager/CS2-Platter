// <copyright file="P_RoadConnectionSystem.UpdateDataJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Unity.Burst;

namespace Platter.Systems {
    using Colossal.Collections;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct UpdateDataJob : IJob {
            [ReadOnly] private NativeList<P_RoadConnectionSystem.UpdateData> m_ParcelEntitiesList;
            [ReadOnly] private ComponentLookup<Created> m_CreatedComponentLookup;
            [ReadOnly] private ComponentLookup<Temp> m_TempComponentLookup;
            [ReadOnly] private TrafficConfigurationData m_TrafficConfigurationData;
            private ComponentLookup<Parcel> m_ParcelComponentLookup;
            private EntityCommandBuffer m_CommandBuffer;
            private IconCommandBuffer m_IconCommandBuffer;
            private BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            public UpdateDataJob(ComponentLookup<Parcel> parcelComponentLookup, NativeList<UpdateData> parcelEntitiesList, ComponentLookup<Created> createdComponentLookup, ComponentLookup<Temp> tempComponentLookup, TrafficConfigurationData trafficConfigurationData, EntityCommandBuffer commandBuffer, IconCommandBuffer iconCommandBuffer, BufferLookup<ConnectedParcel> connectedParcelsBufferLookup) {
                m_ParcelComponentLookup = parcelComponentLookup;
                m_ParcelEntitiesList = parcelEntitiesList;
                m_CreatedComponentLookup = createdComponentLookup;
                m_TempComponentLookup = tempComponentLookup;
                m_TrafficConfigurationData = trafficConfigurationData;
                m_CommandBuffer = commandBuffer;
                m_IconCommandBuffer = iconCommandBuffer;
                m_ConnectedParcelsBufferLookup = connectedParcelsBufferLookup;
            }

            /// <inheritdoc/>
            public void Execute() {
                if (m_ParcelEntitiesList.Length == 0) {
                    return;
                }
#if !USE_BURST
                PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Processing {m_ParcelEntitiesList.Length} entries.");
#endif

                foreach (var updateData in m_ParcelEntitiesList) {
                    var parcel = m_ParcelComponentLookup[updateData.m_Parcel];

                    // A few utility bools
                    var parcelHasCreatedComponent = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHasTempComponent    = m_TempComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHadRoad             = parcel.m_RoadEdge    != Entity.Null;
                    var parcelHasNewRoad          = updateData.m_NewRoad != Entity.Null;
                    var noRoad                    = parcel.m_RoadEdge == Entity.Null && updateData.m_NewRoad == Entity.Null;
                    var roadChanged               = updateData.m_NewRoad  != parcel.m_RoadEdge;
                    var parcelHasNewCurvePosition = updateData.m_CurvePos != parcel.m_CurvePosition;

                    // Determine if we should perform an update
                    // Either the "new best road" we found is not the one the parcel has stored
                    // or it's a newly created parcel
                    if (roadChanged || parcelHasCreatedComponent) {
#if !USE_BURST
                        if (roadChanged) {
                            PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Updating parcel {updateData.m_Parcel} road. {parcel.m_RoadEdge} -> {updateData.m_NewRoad}...");
                        }

                        if (parcelHasCreatedComponent) {
                            PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Updating CREATED parcel {updateData.m_Parcel} with road {updateData.m_NewRoad}...");
                        }
#endif

                        // If this is a TEMP entity
                        if (parcelHasTempComponent) {
                            // Update the parcel's data
                            parcel.m_RoadEdge                            = updateData.m_NewRoad;
                            parcel.m_CurvePosition                       = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
#if !USE_BURST
                            PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Updated TEMP parcel {parcel}.");
#endif
                        } else {
                            // Check if the parcel had a road and now doesn't, or vice versa, to update the icon status
                            if (parcelHadRoad != parcelHasNewRoad) {
                                if (parcelHasNewRoad) {
                                    m_IconCommandBuffer.Remove(
                                        updateData.m_Parcel,
                                        m_TrafficConfigurationData.m_RoadConnectionNotification,
                                        default,
                                        0
                                    );
                                } else if (!updateData.m_Deleted) {
                                    m_IconCommandBuffer.Add(
                                        updateData.m_Parcel,
                                        m_TrafficConfigurationData.m_RoadConnectionNotification,
                                        updateData.m_FrontPos,
                                        IconPriority.Warning,
                                        IconClusterLayer.Default,
                                        IconFlags.OnTop,
                                        default,
                                        false,
                                        false,
                                        false,
                                        0f
                                    );
                                }
                            }

                            // Remove old ConnectedParcel
                            if (parcelHadRoad && m_ConnectedParcelsBufferLookup.HasBuffer(parcel.m_RoadEdge)) {
#if !USE_BURST
                                PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Parcel had a road - removing {updateData.m_Parcel} from road {parcel.m_RoadEdge}'s ConnectedParcels buffer.");
#endif
                                CollectionUtils.RemoveValue<ConnectedParcel>(m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge], new ConnectedParcel(updateData.m_Parcel));
                            }

                            // Update data
                            parcel.m_RoadEdge                            = updateData.m_NewRoad;
                            parcel.m_CurvePosition                       = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, new());

                            if (parcelHasNewRoad) {
#if !USE_BURST
                                PlatterMod.Instance.Log.Debug($"[P_RoadConnectionSystem] UpdateDataJob() -- Parcel has new road - adding {updateData.m_Parcel} to road {updateData.m_NewRoad}'s ConnectedParcels buffer.");
#endif
                                m_ConnectedParcelsBufferLookup[updateData.m_NewRoad].Add(new ConnectedParcel(updateData.m_Parcel));
                            }
                        }
                    }

                    // Alternatively, we could just be updating the parcel's curve position
                    else if (parcelHasNewCurvePosition) {
                        parcel.m_CurvePosition                       = updateData.m_CurvePos;
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
                            IconFlags.OnTop,
                            default,
                            false,
                            false,
                            false,
                            0f
                        );
                    }
                }
            }
        }
    }
}
