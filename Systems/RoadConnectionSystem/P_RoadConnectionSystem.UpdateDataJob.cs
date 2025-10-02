// <copyright file="RoadConnectionSystem.UpdateDataJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct UpdateDataJob : IJob {
            /// <summary>
            /// todo.
            /// </summary>
            public ComponentLookup<Parcel> m_ParcelComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public NativeList<P_RoadConnectionSystem.ConnectionUpdateDataJob> m_ParcelEntitiesList;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Created> m_CreatedComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Temp> m_TempComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public BufferLookup<SubBlock> m_SubBlockBufferLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public TrafficConfigurationData m_TrafficConfigurationData;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public ComponentLookup<Edge> m_EdgeComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            public ComponentLookup<Node> m_NodeComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            public ComponentLookup<NodeGeometry> m_NodeGeoComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            public ComponentLookup<Aggregated> m_AggregatedComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            public EntityCommandBuffer m_CommandBuffer;

            /// <summary>
            /// todo.
            /// </summary>
            public IconCommandBuffer m_IconCommandBuffer;

            /// <summary>
            /// todo.
            /// </summary>
            public BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            /// <inheritdoc/>
            public void Execute() {
                if (m_ParcelEntitiesList.Length == 0) {
                    return;
                }
#if !USE_BURST
                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Processing {m_ParcelEntitiesList.Length} entries.");
#endif

                for (var i = 0; i < m_ParcelEntitiesList.Length; i++) {
                    var updateData = m_ParcelEntitiesList[i];
                    var parcel = m_ParcelComponentLookup[updateData.m_Parcel];

                    // A few utility bools
                    var parcelHasCreatedComponent = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHasTempComponent = m_TempComponentLookup.HasComponent(updateData.m_Parcel);
                    var parcelHadRoad = parcel.m_RoadEdge != Entity.Null;
                    var parcelHasNewRoad = updateData.m_NewRoad != Entity.Null;
                    var noRoad = parcel.m_RoadEdge == Entity.Null && updateData.m_NewRoad == Entity.Null;
                    var roadChanged = updateData.m_NewRoad != parcel.m_RoadEdge;
                    var parcelHasNewCurvePosition = updateData.m_CurvePos != parcel.m_CurvePosition;

                    // Determine if we should perform an update
                    // Either the "new best road" we found is not the one the parcel has stored
                    // or it's a newly created parcel
                    if (roadChanged || parcelHasCreatedComponent) {
#if !USE_BURST
                        if (roadChanged) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updating parcel {updateData.m_Parcel} road. {parcel.m_RoadEdge} -> {updateData.m_NewRoad}...");
                        }

                        if (parcelHasCreatedComponent) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updating CREATED parcel {updateData.m_Parcel} with road {updateData.m_NewRoad}...");
                        }
#endif

                        // If this is a TEMP entity
                        if (parcelHasTempComponent) {
                            // Update the parcel's data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
#if !USE_BURST
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updated TEMP parcel {parcel}.");
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
                                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Parcel had a road - removing {updateData.m_Parcel} from road {parcel.m_RoadEdge}'s ConnectedParcels buffer.");
#endif
                                CollectionUtils.RemoveValue<ConnectedParcel>(m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge], new ConnectedParcel(updateData.m_Parcel));
                            }

                            // Update data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, new());

                            if (parcelHasNewRoad) {
#if !USE_BURST
                                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Parcel has new road - adding {updateData.m_Parcel} to road {updateData.m_NewRoad}'s ConnectedParcels buffer.");
#endif
                                m_ConnectedParcelsBufferLookup[updateData.m_NewRoad].Add(new ConnectedParcel(updateData.m_Parcel));
                            }
#if !USE_BURST
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updated parcel {parcel}.");
#endif
                        }
                    }

                    // Alternatively, we could just be updating the parcel's curve position
                    else if (parcelHasNewCurvePosition) {
#if !USE_BURST
                        PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Giving parcel updated curvepos...");
#endif

                        parcel.m_CurvePosition = updateData.m_CurvePos;
                        m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                        m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, new());
                    }

                    if (noRoad && !updateData.m_Deleted) {
                        // Mark parcel as updated
                        m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, new());

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
