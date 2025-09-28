// <copyright file="RoadConnectionSystem.ConnectionUpdateDataJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using System;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadConnectionSystem : GameSystemBase {
        /// <summary>
        /// Struct containing data for a replacement job (building, road, etc.)
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        public struct ConnectionUpdateDataJob : IComparable<ConnectionUpdateDataJob> {
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
            /// Initializes a new instance of the <see cref="ConnectionUpdateDataJob"/> struct.
            /// </summary>
            /// <param name="parcel">A Parcel to update.</param>
            public ConnectionUpdateDataJob(Entity parcel) {
                this.m_Parcel = parcel;
                this.m_NewRoad = Entity.Null;
                this.m_FrontPos = default;
                this.m_CurvePos = 0f;
                this.m_Deleted = false;
            }

            /// <inheritdoc/>
            public int CompareTo(RoadConnectionSystem.ConnectionUpdateDataJob other) {
                return this.m_Parcel.Index - other.m_Parcel.Index;
            }
        }
    }
}
