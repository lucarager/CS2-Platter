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
    using UnityEngine;

    #endregion

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Perform updates to roads and parcels.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct UpdateDataJob : IJob {
            [ReadOnly] public required NativeList<RCData>            m_ParcelEntitiesList;
            [ReadOnly] public required ComponentLookup<Created>      m_CreatedComponentLookup;
            [ReadOnly] public required ComponentLookup<Temp>         m_TempComponentLookup;
            [ReadOnly] public required ComponentLookup<Hidden>       m_HiddenComponentLookup;
            [ReadOnly] public required TrafficConfigurationData      m_TrafficConfigurationData;
            [ReadOnly] public required BufferLookup<IconElement>     m_IconElementsBufferLookup;
            public required            ComponentLookup<Parcel>       m_ParcelComponentLookup;
            public required            EntityCommandBuffer           m_CommandBuffer;
            public required            IconCommandBuffer             m_IconCommandBuffer;
            public required            BufferLookup<ConnectedParcel> m_ConnectedParcelsBufferLookup;

            /// <inheritdoc/>
            public void Execute() {
                foreach (var updateData in m_ParcelEntitiesList) {
                    ProcessParcel(updateData);
                }
            }

            /// <summary>
            /// Processes a single parcel update, handling road state flags, connection icons,
            /// buffer updates, and marking the parcel for downstream system processing.
            /// </summary>
            /// <param name="rcData">The update data containing the parcel entity and its new road connections.</param>
            private void ProcessParcel(RCData rcData) {
                var parcel    = m_ParcelComponentLookup[rcData.m_Parcel];
                var isCreated = m_CreatedComponentLookup.HasComponent(rcData.m_Parcel);
                var isTemp    = m_TempComponentLookup.HasComponent(rcData.m_Parcel);

                // Update road state flags
                UpdateRoadStateFlags(ref parcel, rcData);

                // Check what changed
                var roadChanged          = rcData.m_FrontRoad     != parcel.m_RoadEdge;
                var curvePositionChanged = !Mathf.Approximately(rcData.m_FrontCurvePos, parcel.m_CurvePosition);
                var needsUpdate          = roadChanged || isCreated || curvePositionChanged;

                // Handle road connection icons
                UpdateRoadConnectionIcon(parcel, rcData, isCreated);

                if (!needsUpdate) {
                    return;
                }

                // For temp parcels, just update the data without modifying buffers
                if (isTemp) {
                    parcel.m_RoadEdge      = rcData.m_FrontRoad;
                    parcel.m_CurvePosition = rcData.m_FrontCurvePos;
                    m_ParcelComponentLookup[rcData.m_Parcel] = parcel;
                    return;
                }

                // Update ConnectedParcel buffers if road changed
                if (roadChanged) {
                    UpdateConnectedParcelBuffers(parcel, rcData);
                }

                // Apply the new road connection data
                parcel.m_RoadEdge      = rcData.m_FrontRoad;
                parcel.m_CurvePosition = rcData.m_FrontCurvePos;
                m_ParcelComponentLookup[rcData.m_Parcel] = parcel;

                // Mark for downstream systems to process
                m_CommandBuffer.AddComponent<Updated>(rcData.m_Parcel, default);
            }

            /// <summary>
            /// Updates the road state flags on a parcel based on the detected road connections
            /// at the left, right, and front sides of the parcel.
            /// </summary>
            /// <param name="parcel">Reference to the parcel component to update.</param>
            /// <param name="rcData">The update data containing detected road connections.</param>
            private void UpdateRoadStateFlags(ref Parcel parcel, RCData rcData) {
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadLeft, rcData.m_LeftRoad   != Entity.Null);
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadRight, rcData.m_RightRoad != Entity.Null);
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadFront, rcData.m_FrontRoad != Entity.Null);

                m_ParcelComponentLookup[rcData.m_Parcel] = parcel;
            }

            /// <summary>
            /// Sets or clears a flag on the parcel state based on the specified value.
            /// </summary>
            /// <param name="state">The current parcel state.</param>
            /// <param name="flag">The flag to set or clear.</param>
            /// <param name="value">True to set the flag, false to clear it.</param>
            /// <returns>The updated parcel state with the flag set or cleared.</returns>
            private static ParcelState SetFlag(ParcelState state, ParcelState flag, bool value) {
                return value ? state | flag : state & ~flag;
            }

            /// <summary>
            /// Updates the road connection warning icon for a parcel. Adds a warning icon when the parcel
            /// has no road connection (either newly created without a road or just lost its road connection).
            /// Removes the warning icon when the parcel gains a road connection or is being deleted.
            /// </summary>
            /// <param name="parcel">The parcel component with current road connection state.</param>
            /// <param name="rcData">The update data containing new road connection information.</param>
            /// <param name="isCreated">Whether the parcel was just created this frame.</param>
            private void UpdateRoadConnectionIcon(Parcel parcel, RCData rcData, bool isCreated) {
                var hasRoad = rcData.m_FrontRoad != Entity.Null;
                var isTemp  = m_TempComponentLookup.HasComponent(rcData.m_Parcel);
                var isHidden  = m_HiddenComponentLookup.HasComponent(rcData.m_Parcel);

                if (hasRoad) {
                    m_IconCommandBuffer.Remove(
                        rcData.m_Parcel,
                        m_TrafficConfigurationData.m_RoadConnectionNotification
                    );
                } else if (!rcData.m_Deleted && !isTemp) {
                    m_IconCommandBuffer.Add(
                        rcData.m_Parcel,
                        m_TrafficConfigurationData.m_RoadConnectionNotification,
                        rcData.m_FrontPos,
                        IconPriority.Warning,
                        IconClusterLayer.Default,
                        IconFlags.IgnoreTarget,
                        default(Entity)
                        //isTemp
                        //isHidden
                    );
                }
            }

            /// <summary>
            /// Updates the ConnectedParcel buffers on road entities when a parcel's road connection changes.
            /// Removes the parcel from the old road's buffer and adds it to the new road's buffer.
            /// </summary>
            /// <param name="parcel">The parcel component with the current (old) road connection.</param>
            /// <param name="rcData">The update data containing the new road connection.</param>
            private void UpdateConnectedParcelBuffers(Parcel parcel, RCData rcData) {
                // Remove from old road's connected parcels
                if (parcel.m_RoadEdge != Entity.Null &&
                    m_ConnectedParcelsBufferLookup.HasBuffer(parcel.m_RoadEdge)) {
                    CollectionUtils.RemoveValue(
                        m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge],
                        new ConnectedParcel(rcData.m_Parcel)
                    );
                }

                // Add to new road's connected parcels
                if (rcData.m_FrontRoad != Entity.Null &&
                    m_ConnectedParcelsBufferLookup.HasBuffer(rcData.m_FrontRoad)) {
                    m_ConnectedParcelsBufferLookup[rcData.m_FrontRoad].Add(
                        new ConnectedParcel(rcData.m_Parcel)
                    );
                }
            }
        }
    }
}