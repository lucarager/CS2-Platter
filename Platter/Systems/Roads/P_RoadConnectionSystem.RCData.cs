// <copyright file="P_RoadConnectionSystem.UpdateData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Game;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    public partial class P_RoadConnectionSystem : PlatterGameSystemBase {
        /// <summary>
        /// Struct containing data for a replacement job.
        /// </summary>
        public struct RCData : IComparable<RCData> {
            /// <summary>
            /// Parcel to update.
            /// </summary>
            public Entity m_Parcel;

            /// <summary>
            /// Road connected at the front access.
            /// </summary>
            public Entity m_FrontRoad;

            /// <summary>
            /// Position of the front access in relation to the edge.
            /// </summary>
            public float3 m_FrontPos;

            /// <summary>
            /// Curve position of the front access in relation to the edge.
            /// </summary>
            public float m_FrontCurvePos;

            /// <summary>
            /// Road connected at the left access.
            /// </summary>
            public Entity m_LeftRoad;

            /// <summary>
            /// Position of the left access in relation to the edge.
            /// </summary>
            public float3 m_LeftPos;

            /// <summary>
            /// Curve position of the left access in relation to the edge.
            /// </summary>
            public float m_LeftCurvePos;

            /// <summary>
            /// Road connected at the right access.
            /// </summary>
            public Entity m_RightRoad;

            /// <summary>
            /// Position of the right access in relation to the edge.
            /// </summary>
            public float3 m_RightPos;

            /// <summary>
            /// Curve position of the right access in relation to the edge.
            /// </summary>
            public float m_RightCurvePos;

            /// <summary>
            /// Whether it's a deletion update.
            /// </summary>
            public bool m_Deleted;

            /// <summary>
            /// Initializes a new instance of the <see cref="RCData"/> struct.
            /// </summary>
            /// <param name="parcel">A Parcel to update.</param>
            public RCData(Entity parcel) {
                m_Parcel        = parcel;
                m_FrontRoad     = Entity.Null;
                m_FrontPos      = default;
                m_FrontCurvePos = 0f;
                m_LeftRoad      = Entity.Null;
                m_LeftPos       = default;
                m_LeftCurvePos  = 0f;
                m_RightRoad     = Entity.Null;
                m_RightPos      = default;
                m_RightCurvePos = 0f;
                m_Deleted       = false;
            }

            /// <inheritdoc/>
            public int CompareTo(RCData other) { return m_Parcel.Index - other.m_Parcel.Index; }
        }
    }
}