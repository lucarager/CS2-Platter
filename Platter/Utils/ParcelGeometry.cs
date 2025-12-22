// <copyright file="ParcelGeometry.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Stateful wrapper providing cached parcel geometry for a specific lot size.
    /// Uses <see cref="ParcelGeometryUtils"/> for all calculations.
    /// </summary>
    internal struct ParcelGeometry {
        private Bounds3 m_BlockBounds;
        private Bounds3 m_ParcelBounds;
        private float3  m_BlockSize;
        private float3  m_ParcelSize;

        public Bounds3 Bounds => m_ParcelBounds;

        public float3 BackNode => GetParcelNode(ParcelGeometryUtils.ParcelNode.BackAccess);
        public float3 LeftNode => GetParcelNode(ParcelGeometryUtils.ParcelNode.LeftAccess);
        public float3 RightNode => GetParcelNode(ParcelGeometryUtils.ParcelNode.RightAccess);

        public float3 BlockCenter => ParcelGeometryUtils.GetCenter(m_BlockBounds);

        public float3 Center => ParcelGeometryUtils.GetCenter(m_ParcelBounds);

        public float3 FrontNode => GetParcelNode(ParcelGeometryUtils.ParcelNode.FrontAccess);

        public float3 Pivot => ParcelGeometryUtils.GetCenter(m_ParcelBounds);

        public float3 Size => m_ParcelSize;

        public float3x4 CornerNodes => GetParcelCorners();

        public float3x4 CornerNodesRelativeToGeometryCenter {
            get {
                var corners = GetParcelCorners();
                corners.c0.z -= m_ParcelSize.z / 2;
                corners.c1.z -= m_ParcelSize.z / 2;
                corners.c2.z -= m_ParcelSize.z / 2;
                corners.c3.z -= m_ParcelSize.z / 2;
                return corners;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelGeometry"/> struct.
        /// </summary>
        /// <param name="lotSize">The lot size in cells (width x depth).</param>
        public ParcelGeometry(int2 lotSize) {
            m_ParcelSize   = ParcelGeometryUtils.GetParcelSize(lotSize);
            m_BlockSize    = ParcelGeometryUtils.GetBlockSize(lotSize);
            m_ParcelBounds = ParcelGeometryUtils.GetParcelBounds(m_ParcelSize);
            m_BlockBounds  = ParcelGeometryUtils.GetBlockBounds(m_ParcelSize, m_BlockSize);
        }

        /// <summary>
        /// Gets the local-space position of a parcel node.
        /// </summary>
        /// <param name="node">The parcel node.</param>
        /// <returns>The local-space position.</returns>
        public float3 GetParcelNode(ParcelGeometryUtils.ParcelNode node) {
            return ParcelGeometryUtils.NodeMult(node) * m_ParcelSize;
        }

        /// <summary>
        /// Returns corner vectors for a parcel.
        /// </summary>
        /// <returns>
        ///     Four float3 vectors representing the corners in clockwise direction. <br/>
        ///      c2 ┌┐ c3. <br/>
        ///      c1 └┘ c0. <br/>
        /// </returns>
        public float3x4 GetParcelCorners() {
            return new float3x4(
                GetParcelNode(ParcelGeometryUtils.ParcelNode.CornerRightFront),
                GetParcelNode(ParcelGeometryUtils.ParcelNode.CornerLeftFront),
                GetParcelNode(ParcelGeometryUtils.ParcelNode.CornerLeftBack),
                GetParcelNode(ParcelGeometryUtils.ParcelNode.CornerRightBack)
            );
        }
    }
}