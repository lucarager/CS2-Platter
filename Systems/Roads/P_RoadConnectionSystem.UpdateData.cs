// <copyright file="P_RoadConnectionSystem.UpdateData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using Game;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class P_RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Struct containing data for a replacement job.
        /// </summary>
        public struct UpdateData : IComparable<UpdateData> {
            /// <summary>
            /// Parcel to update.
            /// </summary>
            public Entity m_Parcel;

            /// <summary>
            /// Road to reference.
            /// </summary>
            public Entity m_NewRoad;

            /// <summary>
            /// Position of the "front" of the parcel in relation to the edge.
            /// </summary>
            public float3 m_FrontPos;

            /// <summary>
            /// Curveposition of the parcel in relation to the edge.
            /// </summary>
            public float m_CurvePos;

            /// <summary>
            /// Whether it's a deletion update.
            /// </summary>
            public bool m_Deleted;

            /// <summary>
            /// Initializes a new instance of the <see cref="UpdateData"/> struct.
            /// </summary>
            /// <param name="parcel">A Parcel to update.</param>
            public UpdateData(Entity parcel) {
                m_Parcel = parcel;
                m_NewRoad = Entity.Null;
                m_FrontPos = default;
                m_CurvePos = 0f;
                m_Deleted = false;
            }

            /// <inheritdoc/>
            public int CompareTo(P_RoadConnectionSystem.UpdateData other) {
                return m_Parcel.Index - other.m_Parcel.Index;
            }
        }
    }
}
