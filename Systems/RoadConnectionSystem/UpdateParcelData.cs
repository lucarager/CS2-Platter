namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class RoadConnectionSystem : GameSystemBase {

        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
        public struct UpdateParcelData : IJob {
            public ComponentLookup<Parcel> m_ParcelComponentLookup;
            [ReadOnly]
            public NativeList<RoadConnectionSystem.ConnectionUpdateData> m_ConnectionUpdateDataList;
            [ReadOnly]
            public ComponentLookup<Created> m_CreatedComponentLookup;
            [ReadOnly]
            public ComponentLookup<Temp> m_TempComponentLookup;
            [ReadOnly]
            public TrafficConfigurationData m_TrafficConfigurationData;
            public IconCommandBuffer m_IconCommandBuffer;

            public void Execute() {
                PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData()");

                for (int i = 0; i < m_ConnectionUpdateDataList.Length; i++) {
                    var updateData = m_ConnectionUpdateDataList[i];
                    var parcel = m_ParcelComponentLookup[updateData.m_Parcel];

                    // A few utility bools
                    bool parcelHasCreatedComponent = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                    bool parcelHasTempComponent = m_TempComponentLookup.HasComponent(updateData.m_Parcel);
                    bool parcelHadRoad = parcel.m_RoadEdge != Entity.Null;
                    bool parcelHasNewRoad = updateData.m_NewRoad != Entity.Null;
                    bool roadChanged = updateData.m_NewRoad != parcel.m_RoadEdge;
                    bool parcelHasNewCurvePosition = updateData.m_CurvePos != parcel.m_CurvePosition;

                    // Determine if we should perform an update
                    // Either the "new best road" we found is not the one the building has stored
                    // or it's a newly created building
                    if (roadChanged || parcelHasCreatedComponent) {
                        if (roadChanged) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData() -- Updating parcel {updateData.m_Parcel} road. {parcel.m_RoadEdge} -> {updateData.m_NewRoad}...");
                        }
                        if (parcelHasCreatedComponent) {
                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData() -- Updating CREATED parcel {updateData.m_Parcel} with road {updateData.m_NewRoad}...");
                        }

                        // If this is a TEMP entity
                        if (parcelHasTempComponent) {
                            // Update the parcel's data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            this.m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData() -- Updated TEMP parcel {parcel}.");
                        } else {

                            // Check if the parcel had a road and now doesn't, or vice versa, to update the icon status
                            if (parcelHadRoad != parcelHasNewRoad) {
                                if (parcelHasNewRoad) {
                                    this.m_IconCommandBuffer.Remove(
                                        updateData.m_Parcel,
                                        this.m_TrafficConfigurationData.m_RoadConnectionNotification,
                                        default(Entity),
                                        (IconFlags)0
                                    );
                                } else if (!updateData.m_Deleted) {
                                    this.m_IconCommandBuffer.Add(
                                        updateData.m_Parcel,
                                        this.m_TrafficConfigurationData.m_RoadConnectionNotification,
                                        updateData.m_FrontPos,
                                        IconPriority.Warning,
                                        IconClusterLayer.Default,
                                        IconFlags.IgnoreTarget,
                                        default(Entity),
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
                                //var connectedBuildingBuffer = this.m_ConnectedBuildings[parcel.m_RoadEdge];
                                //CollectionUtils.RemoveValue<ConnectedBuilding>(connectedBuildingBuffer, new ConnectedBuilding(updateData.m_Building));
                            }

                            // Update data
                            parcel.m_RoadEdge = updateData.m_NewRoad;
                            parcel.m_CurvePosition = updateData.m_CurvePos;
                            this.m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                            PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData() -- Updated parcel {parcel}.");

                            // If the parcel has a new road, add the parcel to that road's buffer
                            if (parcelHasNewRoad) {
                                //this.m_ConnectedBuildings[updateData.m_NewRoad].Add(new ConnectedBuilding(updateData.m_Building));
                            }
                        }
                    }

                    // Alternatively, we could just be updating the building's curve position
                    else if (parcelHasNewCurvePosition) {
                        PlatterMod.Instance.Log.Debug($"[RoadConnectionSystem] UpdateParcelData() -- Giving parcel updated curvepos...");

                        parcel.m_CurvePosition = updateData.m_CurvePos;
                        this.m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
                    }
                }
            }
        }
    }
}
