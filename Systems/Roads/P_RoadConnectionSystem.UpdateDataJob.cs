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
                foreach (var updateData in m_ParcelEntitiesList) {
                    ProcessParcel(updateData);
                }
            }

            /// <summary>
            /// Processes a single parcel update, handling road state flags, connection icons,
            /// buffer updates, and marking the parcel for downstream system processing.
            /// </summary>
            /// <param name="updateData">The update data containing the parcel entity and its new road connections.</param>
            private void ProcessParcel(UpdateData updateData) {
                var parcel    = m_ParcelComponentLookup[updateData.m_Parcel];
                var isCreated = m_CreatedComponentLookup.HasComponent(updateData.m_Parcel);
                var isTemp    = m_TempComponentLookup.HasComponent(updateData.m_Parcel);

                // Update road state flags
                UpdateRoadStateFlags(ref parcel, updateData);

                // Check what changed
                var roadChanged          = updateData.m_FrontRoad     != parcel.m_RoadEdge;
                var curvePositionChanged = !Mathf.Approximately(updateData.m_FrontCurvePos, parcel.m_CurvePosition);
                var needsUpdate          = roadChanged || isCreated || curvePositionChanged;

                // Handle road connection icons
                UpdateRoadConnectionIcon(parcel, updateData, isCreated);

                if (!needsUpdate) {
                    return;
                }

                // For temp parcels, just update the data without modifying buffers
                if (isTemp) {
                    parcel.m_RoadEdge      = updateData.m_FrontRoad;
                    parcel.m_CurvePosition = updateData.m_FrontCurvePos;
                    m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
                    return;
                }

                // Update ConnectedParcel buffers if road changed
                if (roadChanged) {
                    UpdateConnectedParcelBuffers(parcel, updateData);
                }

                // Apply the new road connection data
                parcel.m_RoadEdge      = updateData.m_FrontRoad;
                parcel.m_CurvePosition = updateData.m_FrontCurvePos;
                m_ParcelComponentLookup[updateData.m_Parcel] = parcel;

                // Mark for downstream systems to process
                m_CommandBuffer.AddComponent<Updated>(updateData.m_Parcel, default);
            }

            /// <summary>
            /// Updates the road state flags on a parcel based on the detected road connections
            /// at the left, right, and front sides of the parcel.
            /// </summary>
            /// <param name="parcel">Reference to the parcel component to update.</param>
            /// <param name="updateData">The update data containing detected road connections.</param>
            private void UpdateRoadStateFlags(ref Parcel parcel, UpdateData updateData) {
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadLeft, updateData.m_LeftRoad   != Entity.Null);
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadRight, updateData.m_RightRoad != Entity.Null);
                parcel.m_State = SetFlag(parcel.m_State, ParcelState.RoadFront, updateData.m_FrontRoad != Entity.Null);

                m_ParcelComponentLookup[updateData.m_Parcel] = parcel;
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
            /// <param name="updateData">The update data containing new road connection information.</param>
            /// <param name="isCreated">Whether the parcel was just created this frame.</param>
            private void UpdateRoadConnectionIcon(Parcel parcel, UpdateData updateData, bool isCreated) {
                var hadNoRoad = parcel.m_RoadEdge == Entity.Null;
                var hasRoad   = updateData.m_FrontRoad != Entity.Null;

                if (hasRoad || updateData.m_Deleted) {
                    // Has a road or being deleted - ensure no warning icon
                    m_IconCommandBuffer.Remove(
                        updateData.m_Parcel,
                        m_TrafficConfigurationData.m_RoadConnectionNotification
                    );
                } else if (hadNoRoad) {
                    // No road and either newly created or just lost road - add warning icon
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

            /// <summary>
            /// Updates the ConnectedParcel buffers on road entities when a parcel's road connection changes.
            /// Removes the parcel from the old road's buffer and adds it to the new road's buffer.
            /// </summary>
            /// <param name="parcel">The parcel component with the current (old) road connection.</param>
            /// <param name="updateData">The update data containing the new road connection.</param>
            private void UpdateConnectedParcelBuffers(Parcel parcel, UpdateData updateData) {
                // Remove from old road's connected parcels
                if (parcel.m_RoadEdge != Entity.Null &&
                    m_ConnectedParcelsBufferLookup.HasBuffer(parcel.m_RoadEdge)) {
                    CollectionUtils.RemoveValue(
                        m_ConnectedParcelsBufferLookup[parcel.m_RoadEdge],
                        new ConnectedParcel(updateData.m_Parcel)
                    );
                }

                // Add to new road's connected parcels
                if (updateData.m_FrontRoad != Entity.Null &&
                    m_ConnectedParcelsBufferLookup.HasBuffer(updateData.m_FrontRoad)) {
                    m_ConnectedParcelsBufferLookup[updateData.m_FrontRoad].Add(
                        new ConnectedParcel(updateData.m_Parcel)
                    );
                }
            }
        }
    }
}