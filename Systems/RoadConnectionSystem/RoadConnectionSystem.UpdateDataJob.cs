// <copyright file="RoadConnectionSystem.UpdateDataJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
        public struct UpdateDataJob : IJob {
            /// <summary>
            /// todo.
            /// </summary>
            public ComponentLookup<Parcel> m_ParcelComponentLookup;

            /// <summary>
            /// todo.
            /// </summary>
            [ReadOnly] public NativeList<RoadConnectionSystem.ConnectionUpdateDataJob> m_ConnectionUpdateDataList;

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
            [ReadOnly] public TrafficConfigurationData m_TrafficConfigurationData;

            /// <summary>
            /// todo.
            /// </summary>
            public EntityCommandBuffer m_CommandBuffer;

            /// <summary>
            /// todo.
            /// </summary>
            public IconCommandBuffer m_IconCommandBuffer;

            /// <inheritdoc/>
            public void Execute() {
                if (m_ConnectionUpdateDataList.Length == 0) {
                    return;
                }

                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Processing {m_ConnectionUpdateDataList.Length} entries.");

                for (int i = 0; i < m_ConnectionUpdateDataList.Length; i++) {
                    ConnectionUpdateDataJob updateData = m_ConnectionUpdateDataList[i];
                    Parcel parcel = m_ParcelComponentLookup[updateData.m_Parcel];

                    // A few utility bools
                    bool parcelHasCreatedComponent = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                    bool parcelHasTempComponent = m_TempComponentLookup.HasComponent(updateData.m_Parcel);
                    bool parcelHadRoad = parcel.m_RoadEdge != Entity.Null;
                    bool parcelHasNewRoad = updateData.m_NewRoad != Entity.Null;
                    bool noRoad = parcel.m_RoadEdge == Entity.Null && updateData.m_NewRoad == Entity.Null;
                    bool roadChanged = updateData.m_NewRoad != parcel.m_RoadEdge;
                    bool parcelHasNewCurvePosition = updateData.m_CurvePos != parcel.m_CurvePosition;

                    // Determine if we should perform an update
                    // Either the "new best road" we found is not the one the building has stored
                    // or it's a newly created building
                    if (roadChanged || parcelHasCreatedComponent) {
                        if (roadChanged) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updating parcel {updateData.m_Parcel} road. {parcel.m_RoadEdge} -> {updateData.m_NewRoad}...");
                        }

                        if (parcelHasCreatedComponent) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updating CREATED parcel {updateData.m_Parcel} with road {updateData.m_NewRoad}...");
                        }

                        // If this is a TEMP entity
                        if (parcelHasTempComponent) {
                            // Update the parcel's data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updated TEMP parcel {parcel}.");
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
                                        IconFlags.IgnoreTarget,
                                        default,
                                        false,
                                        false,
                                        false,
                                        0f
                                     );
                                }
                            }

                            // If the parcel had a road before, remove the parcel from that road's buffer
                            // @todo add a parcel buffer to roads
                            // @todo notify road that parcel is present to recalculate blocks
                            if (parcelHadRoad) {
                                // var connectedBuildingBuffer = m_ConnectedBuildings[parcel.m_RoadEdge];
                                // CollectionUtils.RemoveValue<ConnectedBuilding>(connectedBuildingBuffer, new ConnectedBuilding(updateData.m_Building));
                            }

                            // Update data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Updated parcel {parcel}.");

                            // If the parcel has a new road, add the parcel to that road's buffer
                            if (parcelHasNewRoad) {
                                // Looking through the code (CellCheckSystem), we might need to update all of these to trigger a recalc:
                                // this.m_ZoneUpdateCollectSystem.isUpdated &&
                                // this.m_ObjectUpdateCollectSystem.isUpdated &&
                                // this.m_NetUpdateCollectSystem.netsUpdated &&
                                // this.m_AreaUpdateCollectSystem.lotsUpdated &&
                                // this.m_AreaUpdateCollectSystem.mapTilesUpdated
                                m_CommandBuffer.AddComponent<Updated>(updateData.m_NewRoad, default);

                                // m_ConnectedBuildings[updateData.m_NewRoad].Add(new ConnectedBuilding(updateData.m_Building));
                            }
                        }
                    }

                    // Alternatively, we could just be updating the building's curve position
                    else if (parcelHasNewCurvePosition) {
                        PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateDataJob() -- Giving parcel updated curvepos...");

                        parcel.m_CurvePosition = updateData.m_CurvePos;
                        m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
                    }

                    // Make sure we have the icons pop up when no roads are present
                    if (noRoad && !updateData.m_Deleted) {
                        m_IconCommandBuffer.Add(
                            updateData.m_Parcel,
                            m_TrafficConfigurationData.m_RoadConnectionNotification,
                            updateData.m_FrontPos,
                            IconPriority.Warning,
                            IconClusterLayer.Default,
                            IconFlags.IgnoreTarget,
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
