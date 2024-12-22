// <copyright file="ParcelGeometry.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using Colossal.Mathematics;
    using Platter.Constants;
    using Unity.Mathematics;

    internal class ParcelGeometry {
        private Bounds3 m_ParcelBounds;
        private float3 m_ParcelSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelGeometry"/> class.
        /// </summary>
        /// <param name="parcelData"></param>
        public ParcelGeometry(int2 lotSize) {
            m_ParcelSize = GetParcelSize(lotSize);
            m_ParcelBounds = GetParcelBounds(m_ParcelSize);
        }

        public float3 Center => GetCenter(m_ParcelBounds);

        public Bounds3 Bounds => m_ParcelBounds;

        public float3 Size => m_ParcelSize;

        public float3 FrontNode => GetParcelNode(ParcelUtils.ParcelNode.Front);

        public float3 BackNode => GetParcelNode(ParcelUtils.ParcelNode.Back);

        public float3x4 CornerNodes => GetParcelCorners();

        public float3 Pivot => GetPivot();

        public static float3 GetParcelSize(int2 lotSize) {
            return new float3(
                lotSize.x * DimensionConstants.CellSize,
                DimensionConstants.ParcelHeight,
                lotSize.y * DimensionConstants.CellSize
            );
        }

        /// <summary>
        /// The bounds determine where the object sits in relation to its pivot
        /// In our case, we want the edge of the depth (z) axis to be on the pivot, which will be the "front".
        /// </summary>
        /// <param name="parcelSize"></param>
        /// <returns></returns>
        public static Bounds3 GetParcelBounds(float3 parcelSize) {
            return new Bounds3(
                new float3(-parcelSize.x / 2, -parcelSize.y / 2, -parcelSize.z),
                new float3(parcelSize.x / 2, parcelSize.y / 2, 0)
            );
        }

        public static float3 GetCenter(Bounds3 bounds) {
            return MathUtils.Center(bounds);
        }

        public float3 GetParcelNode(ParcelUtils.ParcelNode node) {
            return ParcelUtils.NodeMult(node) * m_ParcelSize;
        }

        public float3 GetPivot() {
            return new float3(0f, m_ParcelSize.y, 0f);
        }

        /// <summary>
        /// Returns corner vectors for a parcel.
        /// </summary>
        /// <param name="parcelData">Parcel Data.</param>
        /// <returns>
        ///     Four float3 vectors representing the corners in clockwise direction. <br/>
        ///      c2 ┌┐ c3. <br/>
        ///      c1 └┘ c0. <br/>
        /// </returns>
        public float3x4 GetParcelCorners() {
            return new float3x4(
                GetParcelNode(ParcelUtils.ParcelNode.CornerRightFront),
                GetParcelNode(ParcelUtils.ParcelNode.CornerLeftFront),
                GetParcelNode(ParcelUtils.ParcelNode.CornerLeftBack),
                GetParcelNode(ParcelUtils.ParcelNode.CornerRightBack)
            );
        }
    }
}
